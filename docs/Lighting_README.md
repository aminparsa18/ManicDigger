# Chunk Lighting

> Computes per-block light levels for a 16×16×16 chunk and its neighbours,
> producing the 18×18×18 `Rendered.Light` buffer consumed by the tessellator.
> Five classes share the responsibility across two distinct pipelines.

---

## Table of Contents

1. [Overview](#overview)
2. [Where It Fits in the Pipeline](#where-it-fits-in-the-pipeline)
3. [Two Lighting Pipelines](#two-lighting-pipelines)
4. [LightManager — Entry Point](#lightmanager--entry-point)
   - [Chunk Load Path](#chunk-load-path)
   - [Block Change Routing](#block-change-routing)
5. [Full Relight Pipeline](#full-relight-pipeline)
   - [ChunkLightingDispatcher](#chunklightingdispatcher)
   - [Race Elimination — BaseLight Snapshots](#race-elimination--baselight-snapshots)
   - [LightBase](#lightbase)
   - [LightBetweenChunks](#lightbetweenchunks)
   - [SnapshotAndEnqueue](#snapshotandenqueue)
6. [Incremental Relight Pipeline](#incremental-relight-pipeline)
   - [IncrementalLightBFS](#incrementallightbfs)
   - [HandleRelightBetweenChunks](#handlerelightbetweenchunks)
7. [LightFlood — Shared BFS Engine](#lightflood--shared-bfs-engine)
8. [Per-Thread Context and Cache Invalidation](#per-thread-context-and-cache-invalidation)
9. [Key Data Structures](#key-data-structures)
10. [Performance Notes](#performance-notes)
11. [External References](#external-references)

---

## Overview

Lighting is handled by five classes with distinct roles:

| Class | Role |
|---|---|
| `LightManager` | Entry point. Routes chunk loads and block changes to the correct pipeline. |
| `ChunkLightingDispatcher` | Worker-thread orchestrator. Runs LightBase + LightBetweenChunks, manages snapshots, enqueues tessellation. |
| `LightBase` | Per-chunk base lighting. Sunlight seeding from the heightmap, emissive block propagation. |
| `LightBetweenChunks` | Cross-chunk propagation. Floods light across the 3×3×3 neighbourhood boundary, writes `Rendered.Light`. |
| `LightFlood` | Shared BFS engine used by both `LightBase` and `LightBetweenChunks`. |
| `IncrementalLightBFS` | Runtime block-change lighting. Updates `chunk.BaseLight` directly without calling `LightBase`. |

---

## Where It Fits in the Pipeline

```
LightManager                   ChunkLightingDispatcher        ChunkTessellationDispatcher
────────────                   ───────────────────────        ───────────────────────────
OnChunkLoaded()          ──▶   HandleFullRelight()
OnBlockChanged()               │  LightBase (27 neighbours)
  │  IncrementalLightBFS  ──▶  │  Snapshot BaseLight ×27
  │  DirtyChunks          ──▶  │  LightBetweenChunks
                               │  Snapshot Rendered.Light
                               └─▶ TessellationChunkWorkItem  ──▶ MakeChunk()
                         ──▶   HandleRelightBetweenChunks()
                                  RefreshRenderedLight()
                               └─▶ TessellationChunkWorkItem  ──▶ MakeChunk()
```

Both pipelines terminate by enqueuing a `TessellationChunkWorkItem` carrying the
18³ `Rendered.Light` snapshot as a `ShadowBuffer`. The tessellator never touches
lighting state directly.

---

## Two Lighting Pipelines

The system routes every lighting event into one of two pipelines:

**Full relight** — used when `chunk.BaseLightDirty` must be recomputed. This covers
chunk loads and block changes that open or close a sunlight column (transparency change
at or above the heightmap). Runs `LightBase` for the 3×3×3 neighbourhood, then
`LightBetweenChunks` to propagate across boundaries.

**Incremental relight** — used for runtime block changes that do not affect sunlight.
`IncrementalLightBFS` updates `chunk.BaseLight` directly across chunk boundaries using
a two-queue remove/add algorithm. `LightBase` is never called. Only
`LightBetweenChunks.RefreshRenderedLight` (Input → Output, no flood) is needed to
refresh `Rendered.Light` from the already-correct `BaseLight`.

```
Block change
    │
    ├── affects sunlight column? ──▶ EnqueueFullRelight → LightingChunkWorkItem
    │                                                     (LightBase + LightBetweenChunks)
    │
    └── emissive / transparency change only ──▶ IncrementalLightBFS.Update()
                                                → RelightBetweenChunksWorkItem per dirty chunk
                                                  (LightBetweenChunks.RefreshRenderedLight only)
```

---

## LightManager — Entry Point

`LightManager` implements `ILightManager` and is the only class that `ModDrawTerrain`
and the networking layer interact with for lighting. It owns the block-type lookup
caches (`_lightRadius[]`, `_transparent[]`) and subscribes to `BlockChangedEvent` via
MessagePipe.

### Chunk Load Path

```csharp
void OnChunkLoaded(int cx, int cy, int cz, Chunk chunk)
```

Sets `chunk.BaseLightDirty = true` and enqueues a `LightingChunkWorkItem`. The worker
pool handles the rest.

### Block Change Routing

`OnBlockChanged` is called on every `BlockChangedEvent`. It first checks whether the
change is lighting-relevant (transparency or emission changed). If not, it returns
immediately — no lighting work is enqueued.

For lighting-relevant changes it calls `AffectsSunlight`:

```csharp
private bool AffectsSunlight(int wx, int wy, int wz, bool wasTransparent, bool isTransparent)
{
    if (wasTransparent == isTransparent) return false;   // emission-only change
    int height = _voxelMap.Heightmap.GetBlock(wx, wy);
    return wz >= height;                                  // at or above the lit column
}
```

If the change is at or above the heightmap and changes transparency, the sunlight column
is affected and a full relight is required. Otherwise `IncrementalLightBFS.Update` runs,
and `RelightBetweenChunksWorkItem`s are enqueued for each chunk in `DirtyChunks`.

---

## Full Relight Pipeline

### ChunkLightingDispatcher

`ChunkLightingDispatcher` implements `IChunkWorkDispatcher` for the lighting stage. Each
worker thread gets its own `LightingThreadContext` (containing `LightBase`,
`LightBetweenChunks`, and the block-type caches) via `ThreadLocal<T>`.

`HandleFullRelight` runs in three phases:

**Phase 1 — LightBase for dirty neighbours.** Walks the 3×3×3 window around the target
chunk. For each neighbour whose `BaseLightDirty` flag is still set, calls
`TryClaimBaseLightDirty()` — an atomic test-and-clear that ensures only one worker runs
`LightBase` on any given chunk even when multiple lighting workers process overlapping
windows simultaneously.

**Phase 2 — BaseLight snapshots.** After this worker's LightBase writes are complete,
takes an `ArrayPool<byte>`-rented snapshot of every neighbour's `BaseLight` (27 × 16³
= 27 × 4096 bytes). Out-of-bounds neighbours are filled with zero. These snapshots are
immutable from the moment they are taken — see Race Elimination below.

**Phase 3 — `SnapshotAndEnqueue`.** Calls `LightBetweenChunks` from snapshots, returns
the 27 BaseLight buffers to the pool, takes a final snapshot of `Rendered.Light`, and
enqueues a `TessellationChunkWorkItem`.

### Race Elimination — BaseLight Snapshots

When multiple lighting workers run concurrently, two workers serving adjacent chunks can
have overlapping 3×3×3 windows. Without protection, worker A could read a neighbour's
`BaseLight` while worker B is still writing it, producing corrupt lighting.

The fix (Option B) is to decouple the read from the live arrays:

```
Worker A: LightBase(c1) completes → snapshot BaseLight(c1) → LightBetweenChunks reads snapshot
Worker B: LightBase(c1) still running                        (no race: B is writing, A reads snapshot)
```

A snapshot taken while another worker is mid-write captures data that is at worst one
frame stale — acceptable for a voxel lighting approximation and far better than reading
partially-written data. `TryClaimBaseLightDirty` further reduces the stale-snapshot
probability by ensuring most chunks complete their LightBase before any snapshot is taken.

### LightBase

`LightBase.CalculateChunkBaseLight` computes `chunk.BaseLight` for a single 16³ chunk
in three steps:

**Sunlight seeding.** Reads the heightmap column to find where sunlight enters each
vertical column. Every position at or above the entry height is filled with level 15.

**Single multi-source BFS.** After seeding, one `FloodLightAll` call propagates all
lit positions simultaneously. This replaces the original per-pair double-flood loop
that was O(n × BFS) — the new approach is a single O(BFS) pass that visits each cell
at most once.

**Emissive block seeding.** Scans all 4096 positions for blocks with `LightRadius > 0`
and runs `FloodLight` from each emitter. The fast x/y/z decode (`pos & 15`,
`(pos >> 4) & 15`, `(pos >> 8) & 15`) avoids division in the inner loop.

Results are written into `chunk.BaseLight` — a 4096-byte flat array that persists
between frames and is recomputed only when `BaseLightDirty` is set.

### LightBetweenChunks

`LightBetweenChunks` takes the 27-chunk neighbourhood's `BaseLight` arrays and produces
the 18×18×18 `Rendered.Light` buffer. It exposes three entry points:

```csharp
// Full relight from live chunk data (single-worker path only)
void CalculateLightBetweenChunks(cx, cy, cz, lightRadius, transparent)

// Full relight from immutable snapshots (multi-worker safe)
void CalculateLightBetweenChunks(cx, cy, cz, lightRadius, transparent, byte[][] snapshots)

// Fast refresh: Input → Output only, no flood (incremental path)
void RefreshRenderedLight(cx, cy, cz)
```

The two-pass `FloodBetweenChunks` works as follows:

Each pass iterates all six face pairs, copying improved light across boundaries using
`CopyFaceX/Y/Z`. Each helper records which destination positions actually received a
higher value into the chunk's `_seeds[]` array. After all face copies, only chunks that
received new boundary light call `FloodLightSeeded` — chunks with no new seeds skip the
BFS entirely.

```
For each pass (2 total):
  Copy improved values across all 6 face pairs → record changed positions in seeds[]
  For each chunk with seedCount > 0:
    FloodLightSeeded(chunk, seeds, seedCount)   ← BFS only from changed positions
  Chunks with seedCount == 0 → skip
```

Maximum seeds per chunk per pass: `6 × 16 × 16 = 1536`. In practice, far fewer cells
improve per pass, especially for interior chunks far from the chunk boundary.

**Output** maps the 27-chunk flat arrays back into the 18×18×18 output layout by
translating each output position `(x, y, z)` in the 18³ grid to its source chunk slot
and block index.

### SnapshotAndEnqueue

After `LightBetweenChunks` completes, `SnapshotAndEnqueue`:

1. Returns all 27 BaseLight snapshot buffers to `ArrayPool<byte>`.
2. Rents a fresh 18³ buffer, copies `rendered.Light` into it — this becomes the
   `ShadowBuffer` for the tessellator.
3. Enqueues a `TessellationChunkWorkItem` carrying the snapshot.

The tessellator's `ShadowBuffer` is always a point-in-time copy — it cannot be mutated
by subsequent lighting work on the same chunk.

---

## Incremental Relight Pipeline

### IncrementalLightBFS

`IncrementalLightBFS.Update` handles a single block change at world position
`(wx, wy, wz)` using the standard two-queue incremental algorithm:

**Remove queue.** If the change makes the position darker (lower emission or transparency
closed), the old light value is recorded and the position is darkened. The queue
propagates darkness outward: for each neighbour, if the neighbour's light is less than
the propagated value, it was lit by the removed source and is darkened further. If the
neighbour's light is equal to or greater than the propagated value, it has an independent
source and is re-seeded into the add queue.

**Add queue.** Seeds new brightness from the changed position (if it gained emission),
from newly transparent paths (all neighbours that can now shine through), and from the
independent sources discovered during the remove phase. Propagates outward, decaying by
1 per step, respecting block transparency and emission.

`IncrementalLightBFS` operates directly on `chunk.BaseLight` across chunk boundaries via
`ReadBaseLight` / `WriteBaseLight`, which translate world coordinates to chunk flat
indices using bit-shifting:

```csharp
int cx = wx >> CsBits;   // CsBits = log2(CHUNK_SIZE) = 4
int lx = wx & CsMask;    // CsMask = CHUNK_SIZE - 1 = 15
```

Every `WriteBaseLight` call adds the affected chunk to `_dirtyChunks`. After `Update`
returns, `LightManager` iterates `DirtyChunks` and enqueues a
`RelightBetweenChunksWorkItem` for each.

`IncrementalLightBFS` does not handle sunlight (heightmap changes). That case is detected
by `LightManager.AffectsSunlight` before `Update` is called and routed to the full
relight pipeline instead.

### HandleRelightBetweenChunks

For each `RelightBetweenChunksWorkItem`, the dispatcher calls
`LightBetweenChunks.RefreshRenderedLight` — the Input → Output path with no flood step.
This is safe because `IncrementalLightBFS` has already propagated correct values into
`chunk.BaseLight` across all boundaries, so no cross-chunk flood is needed. The result
is snapshotted into a `ShadowBuffer` and enqueued as a `TessellationChunkWorkItem`.

---

## LightFlood — Shared BFS Engine

`LightFlood` is used by both `LightBase` (within a single chunk) and `LightBetweenChunks`
(within a single chunk's flat copy during the flood phase). It provides three entry points:

```csharp
// Single-source BFS — used by LightBase for emissive blocks
void FloodLight(chunk, light, startX, startY, startZ, lightRadius, transparent)

// Multi-source BFS seeded from all lit positions — used by LightBase after sunlight seeding
void FloodLightAll(chunk, light, lightRadius, transparent)

// Multi-source BFS seeded from an explicit list — used by LightBetweenChunks
void FloodLightSeeded(chunk, light, seeds, seedCount, lightRadius, transparent)
```

Two implementation details worth noting:

**Power-of-two ring buffer.** The BFS queue is a pre-allocated `int[]` ring buffer sized
at the next power of two. Modulo is replaced with a bitwise AND (`& _mask`), and the
buffer grows by doubling only if capacity is exceeded — which does not happen in normal
chunk sizes. No heap allocation occurs during a normal flood.

**Pre-computed neighbour mask.** `s_mask[4096]` encodes which of the 6 face-connected
directions are valid for each position (boundary positions cannot step out of the chunk).
The mask eliminates x/y/z coordinate decode on every dequeue — the 6-direction branch
reads a single byte lookup instead of computing coordinates.

---

## Per-Thread Context and Cache Invalidation

Each worker thread in the lighting pool has its own `LightingThreadContext`, created
lazily by `ThreadLocal<T>`:

```csharp
_context = new ThreadLocal<LightingThreadContext>(
    () => new LightingThreadContext(voxelMap),
    trackAllValues: false);
```

The context owns:
- `LightBase` instance (with its own `LightFlood` and working arrays)
- `LightBetweenChunks` instance (with its own 27-slot chunk arrays and seed lists)
- `ShadowLightRadius[]` and `ShadowIsTransparent[]` — per-thread block-type caches
- `BaseLightSnapshots[]` — reusable 27-slot array for the snapshot phase

Block type changes (e.g. during `RedrawAllBlocks`) call
`ChunkLightingDispatcher.InvalidateBlockTypeCache()`, which increments a shared
`volatile int _globalCacheVersion`. Each thread context compares its local version
on the next `DispatchAsync` call and rebuilds its block-type arrays if they differ.
Because `trackAllValues: false`, the `ThreadLocal` cannot enumerate all contexts
directly — the version counter is the only race-free signal that reaches all threads.

---

## Key Data Structures

| Field | Owner | Type | Purpose |
|---|---|---|---|
| `chunk.BaseLight` | Chunk | `byte[4096]` | Per-chunk flat lighting array, Z×Y×X order. Recomputed only when `BaseLightDirty`. |
| `chunk.BaseLightDirty` | Chunk | `bool` (atomic via `TryClaimBaseLightDirty`) | Gates `LightBase` recomputation; claimed by first worker to process this chunk. |
| `rendered.Light` | RenderedChunk | `byte[5832]` (18³, pool-rented) | Final light buffer consumed by tessellator. Written by `LightBetweenChunks`. |
| `ShadowBuffer` | TessellationChunkWorkItem | `byte[5832]` (pool-rented) | Point-in-time copy of `rendered.Light`, owned by the tessellator work item. |
| `ctx.BaseLightSnapshots` | LightingThreadContext | `byte[][27]` (pool-rented per slot) | Per-worker snapshot of 27 neighbours' BaseLight; immutable after capture. |
| `_dirtyChunks` | IncrementalLightBFS | `HashSet<(cx,cy,cz)>` | Chunks whose BaseLight was written during the last `Update` call. |
| `s_mask` | LightFlood | `byte[4096]` (static) | Pre-computed per-position valid-direction bitmask; eliminates coordinate decode in the BFS inner loop. |

---

## Performance Notes

| Aspect | Implementation | Notes |
|---|---|---|
| LightBase BFS | Single `FloodLightAll` after sunlight seeding | Replaces O(n × BFS) per-pair loop with one O(BFS) multi-source pass |
| LightBetweenChunks seeding | `FloodLightSeeded` from changed boundary cells only | Max 1536 seeds/chunk/pass vs 4096 in the old all-lit approach; interior chunks skip BFS entirely |
| Multi-worker race | BaseLight snapshots (27 × 4096 bytes per full relight) | Eliminates read/write races at the cost of one `ArrayPool<byte>.Rent` per neighbour per relight |
| Block-type cache | Per-thread arrays, rebuilt only on version bump | Avoids dictionary lookups in the BFS inner loop |
| IncrementalLightBFS | Two-queue remove/add, direct BaseLight writes | Skips LightBase entirely for non-sunlight block changes |
| Sunlight routing | `AffectsSunlight` check before any BFS | Emissive-only changes below the heightmap never touch LightBase |
| BFS queue | Power-of-two ring buffer, `& mask` modulo | No heap allocation during normal operation; grows by doubling only on overflow |
| Coordinate decode | `s_mask[pos]` bitmask per position | Eliminates 6 boundary checks and coordinate arithmetic per BFS dequeue |

---

## External References

| Topic | Link |
|---|---|
| Minecraft-style incremental lighting | [Seed of Andromeda — Voxel Light Propagation](https://www.seedofandromeda.com/blogs/29-fast-flood-fill-lighting-in-a-blocky-voxel-game-pt-1) |
| Two-queue light removal algorithm | [Seed of Andromeda — Pt 2](https://www.seedofandromeda.com/blogs/30-fast-flood-fill-lighting-in-a-blocky-voxel-game-pt-2) |
| Ambient occlusion and light levels | [0fps.net — AO for Minecraft-like worlds](https://0fps.net/2013/07/03/ambient-occlusion-for-minecraft-like-worlds/) |
| BFS ring-buffer queue pattern | [Game Programming Patterns — Data Locality](https://gameprogrammingpatterns.com/data-locality.html) |
