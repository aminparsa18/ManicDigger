# ModDrawTerrain

Client-side terrain rendering mod for Manic Digger. Responsible for everything
between "block data exists in memory" and "triangles appear on screen" — dirty-chunk
detection, worker-pool dispatch, and per-frame draw calls.

`ModDrawTerrain` is now a **thin dispatcher**. It detects which chunks need rebuilding,
enqueues them into the lighting worker pool, and calls the batcher each frame. All
lighting, tessellation, and GPU-upload logic lives inside the worker pipeline —
`ChunkLightingDispatcher`, `ChunkTessellationDispatcher`, and `IMeshBatcher`.

---

## Overview

The world is divided into **16×16×16 block chunks**. Each frame, `ModDrawTerrain`:

1. Scans the view-distance window for dirty chunks.
2. Enqueues up to `ChunkWorkerPool.DefaultWorkerCount` `LightingChunkWorkItem`s per frame.
3. The worker pool handles lighting → tessellation → pending-upload queue entirely off the
   render thread.
4. On the render thread, `FlushPendingUploads` drains the upload queue, then `Draw` issues
   the actual GPU draw calls.

```
Server streams chunks
        │
        ▼
  Rendered.Dirty = true           ← chunk flagged for rebuild
        │
        ▼  (OnFrame — up to DefaultWorkerCount per tick)
  NearestDirty()                  ← find closest dirty chunk, mark not-dirty
        │
        ▼
  lightingQueue.EnqueueAsync()    ← hand off to worker pool
        │
        ▼  (ChunkLightingDispatcher — worker thread)
  LightBase + LightBetweenChunks  ← per-chunk + cross-chunk propagation
        │
        ▼  (ChunkTessellationDispatcher — worker thread)
  TerrainChunkTesselator.MakeChunk()
        │
        ▼  (pending-upload queue)
  meshBatcher.FlushPendingUploads() ← render thread, once per frame
        │
        ▼
  meshBatcher.Draw()              ← render thread, once per frame
```

---

## Threading Model

Three execution contexts are involved, each with clearly separated responsibilities.

**Game-update thread** (`OnFrame`)
- Calls `RedrawChunksAroundLastPlacedBlock` to mark newly dirty chunks.
- Calls `NearestDirty` up to `ChunkWorkerPool.DefaultWorkerCount` times, claiming the
  nearest dirty chunks and clearing their `Dirty` flag atomically.
- Enqueues a `LightingChunkWorkItem` into
  `ILightingWorkQueue` for each claimed chunk.

**Worker pool** (`ChunkLightingDispatcher` / `ChunkTessellationDispatcher`)
- Drains `ILightingWorkQueue`, computes base light and cross-chunk propagation.
- Hands results to `ChunkTessellationDispatcher`, which runs
  `TerrainChunkTesselator.MakeChunk` and pushes finished geometry into the
  batcher's pending-upload queue.
- `ModDrawTerrain` has no direct involvement once work is enqueued.

**Render thread** (`OnRender3d`)
- Checks `ShouldRedrawAllBlocks` and triggers a full redirty sweep if set.
- Calls `meshBatcher.FlushPendingUploads()` — uploads any completed geometry to the GPU.
- Calls `meshBatcher.Draw()` — issues all draw calls for the current frame.
- Updates HUD performance counters once per second.

The render thread never touches chunk data directly. The worker pool never touches
the GPU. `ModDrawTerrain` never touches tessellated geometry — the `ArrayPool` lifecycle
for mesh buffers is managed entirely inside the worker pipeline.

---

## Chunk Dirty Tracking

A chunk is marked dirty by setting `Rendered.Dirty = true`. Each `OnFrame` tick,
`NearestDirty` scans the **view-distance window** around the player and returns the
closest dirty chunk:

```
window = [px ± half, py ± half, pz ± half]
where half = ViewDistance / CHUNK_SIZE
```

The scan is O(V³) over the view volume. The full pre-allocated map array is never
iterated — only the window relevant to the player's current position.

Up to `ChunkWorkerPool.DefaultWorkerCount` chunks are claimed and enqueued per `OnFrame`
tick, rather than the single chunk of the previous implementation. This saturates the
worker pool without requiring any per-chunk thread management in `ModDrawTerrain`.

Chunks become dirty in three ways:

| Trigger | What happens |
|---|---|
| `ShouldRedrawAllBlocks` | All loaded chunks marked dirty + `BaseLightDirty`; `_lightingDispatcher.InvalidateBlockTypeCache()` called |
| Block placed/removed | The chunk + up to 6 face-adjacent neighbours marked dirty |
| Server streams new chunk | Chunk arrives with `Rendered == null`; scan picks it up on next tick |

---

## Render Thread Responsibilities

`OnRender3d` performs three jobs in order each frame:

1. **Full-redraw check** — if `Game.ShouldRedrawAllBlocks` is set, marks every loaded
   chunk dirty, sets `BaseLightDirty`, and calls
   `_lightingDispatcher.InvalidateBlockTypeCache()` so the per-block-type light-radius
   and transparency caches are rebuilt before the next lighting pass.

2. **`FlushPendingUploads`** — uploads any geometry that the worker pool has finished
   since the previous frame. This is the only point where GPU state is mutated by the
   terrain system.

3. **`Draw`** — calls `meshBatcher.Draw(playerX, playerY, playerZ)`, which issues the
   solid and transparent render passes. The batcher handles frustum culling internally
   using the bounding spheres registered at upload time.

---

## Initialisation

`StartTerrain` is called lazily on the first `ShouldRedrawAllBlocks` trigger. It
calls `_tessellationDispatcher.Start()`, which starts the worker threads. There is no
per-chunk initialisation step — chunks are picked up by `NearestDirty` as soon as their
`Rendered.Dirty` flag is set.

---

## Performance Tracking

`UpdatePerformanceInfo` runs once per second on the render thread and publishes two
values to the HUD:

- **chunk updates** — how many `LightingChunkWorkItem`s were enqueued in the last second.
  Note this counts dispatch, not completion; the worker pool may still be processing some.
- **triangles** — total triangle count currently registered in the batcher.

---

## Key Constants

| Constant | Value | Meaning |
|---|---|---|
| `MaxLight` | 15 | Maximum light level |
| `GameConstants.CHUNK_SIZE` | 16 | Chunk edge length in blocks |
| `NoChunk` | -1 | Sentinel for "no block placed" |
| `ChunkWorkerPool.DefaultWorkerCount` | (pool-defined) | Max chunks enqueued per `OnFrame` tick |

---

## Injected Dependencies

| Field | Interface / Type | Role |
|---|---|---|
| `_gameService` | `IGameService` | Platform services (timing, etc.) |
| `_voxelMap` | `IVoxelMap` | Block and chunk data access |
| `_meshBatcher` | `IMeshBatcher` | GPU upload, draw calls, pending-upload queue |
| `_lightingQueue` | `ILightingWorkQueue` | Thread-safe queue into the lighting worker pool |
| `_lightingDispatcher` | `ChunkLightingDispatcher` | Owns block-type cache, invalidation |
| `_tessellationDispatcher` | `ChunkTessellationDispatcher` | Starts/stops worker threads |

---

## Related Classes

| Class | Role |
|---|---|
| `LightBase` | Per-chunk base lighting (sunlight + emissive blocks) |
| `LightBetweenChunks` | Cross-chunk light propagation across 3×3×3 neighbourhood |
| `ChunkLightingDispatcher` | Worker-pool stage 1+2: lighting, block-type cache management |
| `ChunkTessellationDispatcher` | Worker-pool stage 3+4: tessellation, pending-upload queue |
| `LightingChunkWorkItem` | Immutable work item carrying chunk coords and ref |
| `ILightingWorkQueue` | Thread-safe priority queue between `OnFrame` and the worker pool |
| `TerrainChunkTesselator` | Converts block + light data into triangle meshes |
| `IMeshBatcher` | Batches models by texture, manages GPU lifetime, exposes `FlushPendingUploads` |
| `RenderedChunk` | Per-chunk render state: `Dirty` flag, light buffer, batcher IDs |
| `GameLoop` | Calls `OnFrame` (update) and `OnRender3d` (render) each tick |