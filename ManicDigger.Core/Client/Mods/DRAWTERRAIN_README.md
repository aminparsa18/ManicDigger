# ModDrawTerrain

Client-side terrain rendering mod for Manic Digger. Responsible for everything
between "block data exists in memory" and "triangles appear on screen" — chunk
tessellation, lighting, GPU upload, and draw dispatch.

---

## Overview

The world is divided into **16×16×16 block chunks**. `ModDrawTerrain` decides
which chunks need to be rebuilt, builds their geometry on a background thread,
and uploads the result to the GPU on the main thread. It also owns the lighting
pipeline that runs before tessellation.

```
Server streams chunks
        │
        ▼
  rendered.Dirty = true        ← chunk flagged for rebuild
        │
        ▼
  NearestDirty()               ← background thread picks closest chunk
        │
        ▼
  GetExtendedChunk()           ← read 18×18×18 block region
        │
        ▼
  CalculateShadows()           ← base light + cross-chunk propagation
        │
        ▼
  MakeChunk()                  ← tessellate: vertices, UVs, colours, indices
        │
        ▼
  _redrawQueue                 ← hand off to main thread
        │
        ▼
  MainThreadCommit()           ← Batcher.Add() → GPU
        │
        ▼
  DrawTerrain()                ← Batcher.Draw() every frame
```

---

## Threading Model

Two threads are involved and have clearly separated responsibilities.

**Background thread** (`OnReadOnlyBackgroundThread`)
- Finds the nearest dirty chunk via `NearestDirty()`
- Reads block data and computes lighting
- Tessellates geometry via `TerrainChunkTesselator.MakeChunk()`
- Clones mesh data into `ArrayPool`-rented buffers (required because the
  tessellator reuses its internal `toreturnatlas1d` buffers on every call)
- Pushes a `TerrainRendererRedraw` record onto `_redrawQueue`
- Queues `MainThreadCommit` via `game.QueueActionCommit`

**Main thread** (`OnNewFrameDraw3d`)
- Drains the commit queue (capped at 32 actions per frame by `GameLoop`)
- Calls `Batcher.Add()` for each submesh — this is the GPU upload
- Calls `Batcher.Draw()` every frame for frustum-culled rendering
- Checks `ShouldRedrawAllBlocks` and triggers a full redirty sweep if set

The two threads never touch the same data simultaneously. The background thread
owns `_currentChunk`, `_currentChunkShadows`, and `_redrawQueue` during
production. The main thread owns `_batcherIds` and `rendered.Ids` during commit.

---

## Chunk Dirty Tracking

A chunk is marked dirty via `rendered.Dirty = true`. The background thread scans
a **view-distance window** around the player each tick to find the nearest dirty
chunk:

```
window = [px ± half, py ± half, pz ± half]
where half = ViewDistance / chunksize
```

This is O(V³) where V = view distance in chunks — identical in scope to the
original implementation. The full pre-allocated map array (potentially millions
of slots, mostly null) is never iterated.

Chunks become dirty in three ways:

| Trigger | What happens |
|---|---|
| `ShouldRedrawAllBlocks` | All loaded chunks marked dirty + `baseLightDirty` |
| Block placed/removed | The chunk + up to 6 face-adjacent neighbours marked dirty |
| Server streams new chunk | Chunk arrives with `rendered == null`, scan picks it up on next tick |

---

## Extended Chunk Buffer (18³)

Before tessellation, `GetExtendedChunk` reads an **18×18×18** block region
centred on the chunk — one block of overlap on each side of the 16³ interior.

```
  ┌──────────────────┐
  │  overlap (1 blk) │
  │  ┌────────────┐  │
  │  │            │  │
  │  │  16×16×16  │  │  ← actual chunk
  │  │            │  │
  │  └────────────┘  │
  │                  │
  └──────────────────┘
       18×18×18
```

The overlap is needed so face-culling and lighting can correctly handle blocks
that sit exactly on a chunk boundary without reading from a different chunk
mid-tessellation.

---

## Lighting Pipeline

Lighting runs in two stages before tessellation:

### Stage 1 — LightBase (per chunk, cached)

Computes **base lighting** for a single 16³ chunk in isolation:

1. **Sunlight seeding** — reads the heightmap to find where sunlight enters each
   vertical column, then fills upward with full intensity (level 15).
2. **Sunlight flood** — propagates sunlight laterally through transparent blocks
   within the chunk.
3. **Emissive blocks** — seeds light from blocks with `LightRadius > 0` and
   floods outward.

Result is stored in `chunk.baseLight`. Recomputation is gated behind
`chunk.baseLightDirty` — once computed, the result is reused until a block
change invalidates it.

### Stage 2 — LightBetweenChunks (cross-chunk propagation)

Loads a **3×3×3 neighbourhood** of chunks (27 chunks total) and runs two
flood-fill passes across their shared faces, allowing light to bleed across
chunk boundaries. The result is written into an **18×18×18 output buffer**
(`rendered.Light`) which the tessellator samples during face colouring.

This stage is skipped entirely if no chunk in the 3×3×3 neighbourhood had its
base light recomputed — avoiding 27 neighbourhood reads and two flood passes on
chunk rebuilds not caused by lighting changes.

---

## Tessellation

`TerrainChunkTesselator.MakeChunk` takes the 18³ block buffer and the 18³ light
buffer and produces one or more `VerticesIndicesToLoad` submeshes (split by
texture atlas and transparency).

Because the tessellator reuses its internal output buffers on every call, the
geometry data is immediately deep-copied into `ArrayPool`-rented arrays before
being handed to the main thread. The pool buffers are returned after the GPU
upload in `DoRedraw`.

### Uniform chunk optimisation

Before calling the tessellator, `IsUniformChunk` checks whether every block in
the 18³ buffer is identical. A chunk filled entirely with air (or any single
block type) produces no visible faces and skips tessellation and lighting
entirely.

---

## GPU Upload and Batcher

`DoRedraw` runs on the main thread and:

1. Removes the chunk's previous batcher entries (`Batcher.Remove`)
2. Calls `Batcher.Add` for each submesh, passing position and a bounding-sphere
   radius of `√3/2 × chunkSize` (circumradius of a cube)
3. Stores the returned batcher IDs in `rendered.Ids` for future removal
4. Returns all `ArrayPool` buffers

`DrawTerrain` calls `Batcher.Draw` once per frame, passing the player position.
The batcher handles frustum culling using the bounding spheres registered at
upload time.

---

## Performance Tracking

`UpdatePerformanceInfo` runs once per second on the main thread and publishes
two values to the HUD:

- **chunk updates** — how many chunks were tessellated in the last second
- **triangles** — total triangle count currently in the batcher

---

## Key Constants

| Constant | Value | Meaning |
|---|---|---|
| `MaxLight` | 15 | Maximum light level |
| `BufferedChunkEdge` | 18 | Side length of the extended block buffer |
| `BufferedChunkVolume` | 5832 | 18³ — total entries in the extended buffer |
| `NoChunk` | -1 | Sentinel for "no block placed" |
| `MAX_BLOCKTYPES` | 1024 | Size of light radius / transparency lookup arrays |

---

## Related Classes

| Class | Role |
|---|---|
| `LightBase` | Per-chunk base lighting (sunlight + emissive blocks) |
| `LightBetweenChunks` | Cross-chunk light propagation across 3×3×3 neighbourhood |
| `TerrainChunkTesselator` | Converts block + light data into triangle meshes |
| `TerrainRendererRedraw` | Immutable record carrying mesh data across the thread boundary |
| `RenderedChunk` | Per-chunk render state: dirty flag, light buffer, batcher IDs |
| `GameLoop` | Calls `OnNewFrameDraw3d` and `OnReadOnlyBackgroundThread` each frame |
