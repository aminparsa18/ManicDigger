# Worker Infrastructure

> Background service host for the Manic Digger client/server session.
> Five classes manage the lifetime and execution of all parallel work:
> chunk lighting, chunk tessellation, server simulation, and periodic maintenance tasks.

---

## Table of Contents

1. [Overview](#overview)
2. [WorkerHost — Lifecycle Manager](#workerhost--lifecycle-manager)
   - [Startup](#startup)
   - [Shutdown Ordering](#shutdown-ordering)
3. [ChunkWorkerPool — Parallel Work Queue](#chunkworkerpool--parallel-work-queue)
   - [Channel Design](#channel-design)
   - [Worker Loop](#worker-loop)
4. [SimulationLoop — Server Tick](#simulationloop--server-tick)
5. [ServerSimulationStep — Tick Logic](#serversimulationstep--tick-logic)
6. [PeriodicTaskScheduler — Maintenance Tasks](#periodictaskscheduler--maintenance-tasks)
7. [Key Constants](#key-constants)

---

## Overview

All background work for a game session runs under a single `WorkerHost`. It owns four
`BackgroundService` instances registered with the .NET hosting infrastructure:

| Service | Role |
|---|---|
| `ChunkWorkerPool` (lighting) | Reads `LightingChunkWorkItem` / `RelightBetweenChunksWorkItem` from a bounded channel; dispatches to `ChunkLightingDispatcher`. |
| `ChunkWorkerPool` (tessellation) | Reads `TessellationChunkWorkItem` from a bounded channel; dispatches to `ChunkTessellationDispatcher`. |
| `SimulationLoop` | Drives `ISimulationStep.Tick` at 20 Hz using `PeriodicTimer`. |
| `PeriodicTaskScheduler` | Fires registered `IScheduledTask` implementations on their own independent intervals. |

`WorkerHost` starts them in pipeline order and stops them in reverse, ensuring no new
work is produced after the consumer that would process it has shut down.

---

## WorkerHost — Lifecycle Manager

`WorkerHost` is the single point of control for all background workers in a session.
It is called from `Connect()` on session start and from the exit path on teardown.

### Startup

```
StartAsync()
  │
  ├─ SimulationLoop.StartAsync(ct)
  ├─ tessellationPool.StartAsync(ct)
  ├─ lightingPool.StartAsync(ct)
  ├─ periodicScheduler.StartAsync(ct)
  │
  └─ _allWorkers = Task.WhenAll(all four ExecuteTask)
     SinglePlayerServerLoaded = true
```

All four workers share the same `CancellationToken` from `ServerLifetime`. Cancelling
that token is the only signal needed to stop all workers simultaneously.

In `DEBUG` builds, `BaseLightRaceDetector.Init` is called before any workers start,
injecting diagnostic hooks that can detect concurrent writes to `chunk.BaseLight`.

### Shutdown Ordering

```
StopAsync()
  │
  ├─ _lifetime.SignalStop()          — cancels the shared CancellationToken
  │
  ├─ lightingPool.StopAsync()        — stop first: produces TessellationChunkWorkItems
  ├─ tessellationPool.StopAsync()    — stop second: consumes from lighting
  ├─ simulationLoop.StopAsync()
  ├─ periodicScheduler.StopAsync()
  │
  └─ await _allWorkers               — wait for all tasks to drain
     SinglePlayerServerLoaded = false
```

Lighting is stopped before tessellation because it is the upstream producer in the
pipeline. Stopping them in the wrong order would allow the lighting pool to enqueue new
`TessellationChunkWorkItem`s into an already-stopped tessellation pool. Cancellation
propagates through the channel: when the `CancellationToken` fires, each pool's
`stoppingToken.Register` callback calls `_channel.Writer.TryComplete()`, which signals
all `ReadAllAsync` iterators to exit cleanly after draining any remaining items.

---

## ChunkWorkerPool — Parallel Work Queue

`ChunkWorkerPool` is both a `BackgroundService` and an `IChunkWorkQueue`. The same class
is instantiated twice — once for the lighting stage and once for the tessellation stage —
with different `IChunkWorkDispatcher` implementations injected.

### Channel Design

```csharp
Channel.CreateBounded<ChunkWorkItem>(new BoundedChannelOptions(capacity: 512)
{
    SingleReader = false,
    SingleWriter = false,
    FullMode = BoundedChannelFullMode.DropOldest,
})
```

**Bounded capacity** prevents unbounded memory growth when the worker pool falls behind
the rate of dirty chunk production (e.g. on first load or `RedrawAllBlocks`).

**`DropOldest` overflow policy** silently discards the oldest unprocessed item when the
channel is full. For chunk work this is the correct trade-off: an older dirty chunk is
less likely to be in the player's immediate view than a newer one, and any dropped chunk
will be re-enqueued on the next `NearestDirty` scan when it is still dirty.

**`SingleReader/Writer = false`** — multiple producers (the game-update thread enqueues
up to `DefaultWorkerCount` items per frame) and multiple consumers (all worker tasks read
from the same channel) are both possible.

### Worker Loop

Each worker task runs `WorkerLoopAsync`, which consumes the channel with `await foreach`:

```
WorkerLoopAsync(workerId, ct)
  │
  └─ await foreach item in channel.ReadAllAsync(ct):
         await _dispatcher.DispatchAsync(item, ct)
         item.Completion?.TrySetResult()
       on OperationCanceledException:
         item.Completion?.TrySetCanceled(ct)
         break
       on Exception:
         log error, item.Completion?.TrySetException(ex)
         continue  ← one bad chunk does not kill the pool
```

`DefaultWorkerCount` is `max(1, ProcessorCount - 1)`, reserving one core for the render
thread. The count can be overridden at construction time for testing or resource-constrained
environments.

`item.Completion` is an optional `TaskCompletionSource` that callers can await to be
notified when a specific work item finishes. Normal terrain streaming does not use it;
it exists for deterministic integration tests and any synchronous flush operations.

---

## SimulationLoop — Server Tick

`SimulationLoop` replaces the original `ServerThreadStart` busy-wait:

```csharp
// Old
while (true) { server.Process(); Thread.Sleep(1); }

// New
using PeriodicTimer timer = new(_tickInterval);  // default: 50 ms = 20 Hz
while (await timer.WaitForNextTickAsync(stoppingToken))
{
    float dt = MeasureElapsed();
    _simulation.Tick(dt);
}
```

`PeriodicTimer` uses the OS high-resolution clock and does not drift — if a tick takes
longer than the interval, the next tick fires immediately rather than doubling the delay.
No busy-wait, no `Thread.Sleep`, accurate `dt` via `Stopwatch.GetTimestamp()`.

Unhandled exceptions inside `Tick` are logged and swallowed so that one bad server frame
does not terminate the loop. `OperationCanceledException` is not caught and propagates
normally to stop the loop cleanly.

---

## ServerSimulationStep — Tick Logic

`ServerSimulationStep` is the `ISimulationStep` implementation consumed by `SimulationLoop`.
It encapsulates exactly the logic that previously lived inline in `ServerThreadStart`:

```csharp
public void Tick(float dt)
{
    if (_gameExit.Exit)
    {
        _server.Stop();
        _gameExit.Exit = false;
        _lifetime.SignalStop();   // → CancellationToken cancelled → SimulationLoop exits
        return;
    }

    if (_singlePlayerService.SinglePlayerServerExit)
    {
        _server.Exit();
        _singlePlayerService.SinglePlayerServerExit = false;
        return;
    }

    _server.Process(dt);
}
```

`_lifetime.SignalStop()` on exit propagates the cancellation signal to all workers via
the shared `CancellationToken`, which means `WorkerHost.StopAsync` does not need to
be called explicitly from the exit path — cancellation flows naturally.

---

## PeriodicTaskScheduler — Maintenance Tasks

`PeriodicTaskScheduler` drives all `IScheduledTask` implementations from a single shared
`PeriodicTimer` that wakes once per second:

```
Every 1 s:
  for each task:
    if now >= task.NextRunAt:
      await task.ExecuteAsync(ct)
      task.NextRunAt = now + task.Interval
```

Each task tracks its own `NextRunAt` independently, so tasks with different intervals
coexist without spinning up separate threads or timers. A task that throws is logged and
rescheduled from the moment it failed — it does not stop firing, and it does not block
other tasks from running on the next tick.

The 1-second tick interval is intentionally coarser than any task's actual interval.
No registered task requires sub-second precision; the overhead of one timer wake per
second is negligible.

---

## Key Constants

| Constant | Value | Location | Meaning |
|---|---|---|---|
| `DefaultWorkerCount` | `max(1, ProcessorCount − 1)` | `ChunkWorkerPool` | Leaves one core for the render thread |
| `channelCapacity` | `512` (default) | `ChunkWorkerPool` | Maximum queued work items before `DropOldest` kicks in |
| `DefaultTickInterval` | `50 ms` (20 Hz) | `SimulationLoop` | Server simulation rate |
| `TickInterval` | `1 s` | `PeriodicTaskScheduler` | How often the scheduler checks for due tasks |
