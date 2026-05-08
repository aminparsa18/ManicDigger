# Chunk Tessellation

> Converts raw 3-D block data into renderable geometry for a single 16 × 16 × 16 chunk.
> Two classes share the responsibility: `ChunkTessellationDispatcher` owns thread routing
> and resource lifecycle; `TerrainChunkTesselator` is a pure geometry factory.

---

## Table of Contents

1. [Overview](#overview)
2. [Where It Fits in the Pipeline](#where-it-fits-in-the-pipeline)
3. [ChunkTessellationDispatcher](#chunktessellationdispatcher)
   - [Per-Thread Context](#per-thread-context)
   - [TessellationChunkWorkItem](#tessellationchunkworkitem)
   - [Shadow Buffer Lifecycle](#shadow-buffer-lifecycle)
   - [Uniform Chunk Fast-Path](#uniform-chunk-fast-path)
   - [Mesh Clone and StageChunk](#mesh-clone-and-stagechunk)
4. [TerrainChunkTesselator](#terraincunktesselator)
   - [Initialisation](#initialisation)
   - [Block Type Cache](#block-type-cache)
   - [ChunkTessellationContext](#chunktessellationcontext)
5. [Coordinate System](#coordinate-system)
6. [Data Layout: The 18³ Buffer](#data-layout-the-18³-buffer)
7. [Three-Pass Architecture](#three-pass-architecture)
   - [Pass 1 — CalculateVisibleFaces](#pass-1--calculatevisiblefaces)
   - [Pass 2 — CalculateTilingCount](#pass-2--calculatetilingcount)
   - [Pass 3 — BuildBlockPolygons](#pass-3--buildblockpolygons)
8. [Smooth Lighting (Ambient Occlusion)](#smooth-lighting-ambient-occlusion)
9. [Block Shape Variants](#block-shape-variants)
10. [Texture Atlas System](#texture-atlas-system)
11. [Output Format](#output-format)
12. [Key Data Structures](#key-data-structures)
13. [Performance Notes](#performance-notes)
14. [External References](#external-references)

---

## Overview

Tessellation is split between two classes with complementary responsibilities:

**`ChunkTessellationDispatcher`** — the worker-thread orchestrator. It receives
`TessellationChunkWorkItem`s from the worker pool, fetches the 18³ block region, copies
the pre-baked lighting snapshot, calls `MakeChunk`, deep-copies the result into
`ArrayPool`-rented buffers, and hands the finished geometry to the batcher. It never
generates a single vertex itself.

**`TerrainChunkTesselator`** — the pure geometry factory. Feed it block IDs and shadow
values via a `ChunkTessellationContext`; it returns vertex buffers ready for the GPU.
It has no knowledge of threads, cameras, frame timing, or OpenGL state.

```
ChunkTessellationDispatcher (worker thread)
    │
    ├─ receives TessellationChunkWorkItem
    ├─ reads 18³ block region into ctx.CurrentChunk
    ├─ copies ShadowBuffer into ctx.CurrentChunkShadows
    ├─ uniform-chunk check → skip if all blocks identical
    └─▶ TerrainChunkTesselator.MakeChunk(x, y, z, lightLevels, ctx)
            │
            └─▶ VerticesIndicesToLoad[]  (vertex + index buffers per atlas page)
    │
    ├─ deep-copies result into ArrayPool-rented arrays
    ├─ returns ShadowBuffer to pool
    └─▶ meshBatcher.StageChunk(chunk, meshData, meshCount)
```

---

## Where It Fits in the Pipeline

```
ChunkLightingDispatcher          ChunkTessellationDispatcher       Render thread
───────────────────────          ───────────────────────────       ─────────────
LightBase + LightBetweenChunks   TessellateChunk()                 FlushPendingUploads()
  → snapshots rendered.Light  ─▶   GetExtendedChunk()         ──▶  Batcher.Add()
  → rents ShadowBuffer            MakeChunk()                      glDrawElements()
  → enqueues                      StageChunk()
    TessellationChunkWorkItem
```

The lighting stage produces the pre-baked `ShadowBuffer` and enqueues the work item.
The tessellation stage consumes it. The render thread drains the batcher's pending-upload
queue via `FlushPendingUploads` and never touches geometry generation directly.

---

## ChunkTessellationDispatcher

### Per-Thread Context

Each worker thread gets its own `ChunkTessellationContext` via a `ThreadLocal<T>` whose
factory calls `_tesselator.CreateContext()`:

```csharp
_context = new ThreadLocal<ChunkTessellationContext>(
    () => _tesselator.CreateContext(),
    trackAllValues: false);
```

`ChunkTessellationContext` holds all working buffers — `CurrentChunk` (18³ block IDs),
`CurrentChunkShadows` (18³ light levels), `ChunkDraw16` (per-block face flags), and the
per-atlas `GeometryModel` output buckets. Because each thread owns its context, there is
no locking and no shared mutable state anywhere in the tessellation path.

### TessellationChunkWorkItem

The work item is produced by `ChunkLightingDispatcher` and carries everything the
dispatcher needs without touching lighting state on the worker thread:

```csharp
public record TessellationChunkWorkItem(
    int ChunkX, int ChunkY, int ChunkZ,
    Chunk Chunk,
    byte[] ShadowBuffer,       // ArrayPool-rented 18³ snapshot of rendered.Light
    bool ShadowBufferRented,   // whether the pool must be returned
    TaskCompletionSource? Completion,
    int Priority
) : ChunkWorkItem(...);
```

The `ShadowBuffer` is a read-only snapshot from the worker thread's perspective. It is
written once by the lighting stage and consumed once by the tessellation stage.

### Shadow Buffer Lifecycle

The shadow buffer is returned to `ArrayPool<byte>.Shared` by the dispatcher
unconditionally after `MakeChunk`, whether tessellation ran or was skipped by the
uniform-chunk check:

```
lighting stage   → ArrayPool<byte>.Rent(BufferedChunkVolume)  → ShadowBuffer
tessellation     → copies ShadowBuffer into ctx.CurrentChunkShadows
dispatcher       → ArrayPool<byte>.Return(ShadowBuffer)        ← always
```

This ensures the buffer is never leaked regardless of the code path taken.

### Uniform Chunk Fast-Path

Before calling `MakeChunk`, the dispatcher checks whether every entry in the 18³ block
buffer is identical. A chunk filled entirely with one block type (including all-air)
produces no visible faces and skips tessellation entirely, returning zero submeshes to
the batcher immediately.

### Mesh Clone and StageChunk

`TerrainChunkTesselator` reuses its internal `GeometryModel` output buffers on every
`MakeChunk` call. The dispatcher therefore deep-copies each live submesh into fresh
`ArrayPool`-rented arrays before handing off to the batcher:

```
MakeChunk returns  → shared internal buffers (reused next call)
CloneModelData     → ArrayPool-rented copy owned by the batcher
StageChunk         → batcher takes ownership; returns arrays after GPU upload
```

The pool arrays are returned by the batcher after `Batcher.Add` completes during
`FlushPendingUploads` on the render thread.

---

## TerrainChunkTesselator

### Initialisation

`Start()` must be called before `MakeChunk`. It reads map dimensions from `IVoxelMap`,
allocates the `_blockFlags` cache, and calls `RefreshBlockTypeCache()`.

`OnAtlasReady(int texturesPerAtlas)` must be called after the terrain texture atlas is
built. It computes UV constants, the `AtiArtifactFix` inset, and the atlas count — all
values that `MakeChunk` reads per-face but that only change when the atlas changes.

### Block Type Cache

`RefreshBlockTypeCache()` builds `_blockFlags[]`, a flat `BlockRenderFlags[]` array
indexed by block type ID, packing the Transparent / Lowered / Fluid flags into a single
byte per block type:

```csharp
private bool IsTransparent(int id) => (_blockFlags[id] & BlockRenderFlags.Transparent) != 0;
private bool IsLowered(int id)     => (_blockFlags[id] & BlockRenderFlags.Lowered)     != 0;
private bool IsFluid(int id)       => (_blockFlags[id] & BlockRenderFlags.Fluid)       != 0;
```

Previously this rebuild ran inside `MakeChunk` on every single chunk build, even though
block definitions almost never change. It is now called once from `Start()` and again
only when block types are explicitly changed (via `ChunkLightingDispatcher.InvalidateBlockTypeCache`
→ `RefreshBlockTypeCache`).

### ChunkTessellationContext

`CreateContext()` returns a `ChunkTessellationContext` sized for the current atlas
configuration. The context holds all working arrays that were previously pre-allocated
as fields on the tessellator itself:

| Field | Type | Purpose |
|---|---|---|
| `CurrentChunk` | `int[]` (18³) | Block IDs including 1-block border — written by dispatcher |
| `CurrentChunkShadows` | `byte[]` (18³) | Light levels copied from `ShadowBuffer` — written by dispatcher |
| `ChunkDraw16` | `byte[]` (16³) | Per-block `TileSideFlags` bitmask from Pass 1 |
| `ChunkDrawCount16Flat` | `byte[]` (16³ × 6) | Per-block-per-side draw flag from Pass 2 |
| `TmpNPos` | `int[]` (6) | Scratch neighbour positions reused each block |
| Output buckets | `GeometryModel[]` | One per atlas page, opaque and transparent |

Moving working state into the context is what makes the tessellator safe to call from
multiple worker threads simultaneously without any locking.

---

## Coordinate System

```
        Z  (up)
        │
        │
        │
        └──────── X
       /
      /
     Y
```

Block positions follow this **right-handed, Z-up** convention throughout.
Array indexing uses the helper:

```csharp
Index3d(x, y, z, sizeX, sizeY) = (z * sizeY + y) * sizeX + x
```

All three passes iterate `xx` in the innermost loop so that sequential reads walk along
the X axis, maximising cache-line utilisation.

---

## Data Layout: The 18³ Buffer

The tessellator receives an **18 × 18 × 18** block array even though the chunk is only
16 × 16 × 16. The extra 1-block border on every side holds the neighbouring chunks'
outermost layer.

```
┌─────────────────────────┐
│  18 × 18 × 18 buffer    │
│  ┌───────────────────┐  │
│  │  16 × 16 × 16     │  │
│  │  actual chunk     │  │
│  └───────────────────┘  │
│  (1-block ghost border) │
└─────────────────────────┘
```

This border is essential for two operations:

- **Face visibility** — checking whether a face on a chunk edge is hidden by a block in
  the neighbouring chunk.
- **Smooth lighting / AO** — sampling shadow values from blocks outside the chunk
  boundary when computing per-vertex ambient occlusion.

Without the border, edge-block faces would either always be drawn or always be culled,
and lighting seams would appear at every chunk boundary.

---

## Three-Pass Architecture

### Pass 1 — `CalculateVisibleFaces`

Walks every block in the 16³ interior and decides which of its 6 faces are visible.
A face is visible if the neighbouring block is:
- air (`tt == 0`), **or**
- transparent and not lowered (e.g. glass, leaves), **or**
- a fluid when the current block is not a fluid.

Special cases handled here:
- **Lowered blocks** (half-slabs, rails) — the top face is forced visible when any side
  is visible, because the block is shorter than a full cube.
- **Rail slopes** — extra faces are enabled depending on the slope direction
  (`RailSlope.TwoDownRaised`, etc.) so sloped rail geometry is not clipped.

Results are written into `ctx.ChunkDraw16`, one byte per block, where each bit
represents one of the 6 sides via `TileSideFlags`.

### Pass 2 — `CalculateTilingCount`

Translates the `TileSideFlags` bitmask from Pass 1 into a flat byte array
`ctx.ChunkDrawCount16Flat` with layout:

```
index = blockIndex * 6 + sideIndex
```

This de-normalises the per-block flags into a format that Pass 3 can read with a single
contiguous span instead of computing bit positions at draw time. It also applies the
**axis-swap** between `TileSideFlags` (render-space) and `TileSide` (enum order), which
differs from the flag bit positions for historical reasons.

### Pass 3 — `BuildBlockPolygons`

The main geometry loop. For each block with any visible face:

```
BuildSingleBlockPolygon
    └─ for each visible TileSide
           BuildBlockFace          (computes per-corner AO)
               └─ DrawBlockFace   (emits 4 vertices + 6 indices)
```

Each call to `DrawBlockFace` appends exactly **4 vertices and 6 indices** (2 triangles)
into the appropriate `GeometryModel` bucket in `ctx` (selected by texture atlas page and
transparency flag).

---

## Smooth Lighting (Ambient Occlusion)

This is the most computationally expensive part of the tessellator. For each face, the
method samples **9 neighbours** (center + 8 surrounding) to produce a per-corner shadow
ratio, approximating the soft-shadow effect seen in Minecraft-style renderers.

```
Neighbours sampled for the Top face of one block:

  TL ─── T ─── TR
  │              │
  L    center    R      (one level above the block)
  │              │
  BL ─── B ─── BR
```

Each corner's final light value is a weighted average of its two adjacent edge-neighbours
and the diagonal:

```csharp
// Corner TopLeft = average of Top, Left, and TopLeft neighbours
// If two edge neighbours are both opaque → apply hard AO (halfocc)
// If any single neighbour is opaque     → apply soft AO (occ)
// Otherwise                             → full brightness
```

The `lightLevels[]` lookup (passed in from `ILightManager`) converts the raw shadow byte
(0–15) to a float before the averaging, so the final per-vertex colour is already in
linear space when written into `GeometryModel.Rgba`.

> **Reference:** The algorithm is a simplified version of the technique described by
> 0fps in [*Ambient occlusion for Minecraft-like worlds*](https://0fps.net/2013/07/03/ambient-occlusion-for-minecraft-like-worlds/)
> (2013) — the canonical write-up on per-vertex AO for voxel engines.

---

## Block Shape Variants

`BuildSingleBlockPolygon` branches on `DrawType` to handle non-cube geometry:

| `DrawType` | Description | Key behaviour |
|---|---|---|
| `Solid` | Full cube | Standard 6-face cube, all faces culled normally |
| `Fluid` | Water / lava | Top face slightly lowered; side faces use special fluid visibility rules |
| `HalfHeight` | Half-slab | `IsLowered = true`; top face always drawn; height offset applied |
| `Flat` | Flower, grass, rail bed | Single cross or flat quad; no side faces |
| `Rail` | Sloped rail track | `CornerHeights` modified per `RailSlope` value; uses `_cornerHeightLookup` table |

The corner-height system uses a pre-built `_cornerHeightLookup[side, corner]` table to
avoid a nested switch on every vertex:

```csharp
float GetCornerHeightModifier(TileSide side, Corner corner)
{
    int index = _cornerHeightLookup[(int)side, (int)corner];
    return index < 0 ? 0f : _cornerHeights[(Corner)index];
}
```

---

## Texture Atlas System

All block textures are packed into one or more **atlas pages**. UV coordinates per face
are computed from constants set by `OnAtlasReady`:

```
texRecTop    = (textureIndex / texturesPerAtlas) + AtiArtifactFix
texRecBottom = texRecTop + (1 / texturesPerAtlas) − AtiArtifactFix
```

The `AtiArtifactFix` is a UV inset (half-texel on desktop, 1.5 texels on WebGL) that
prevents texture bleeding at atlas tile borders — a well-known artefact in
bilinear-filtered atlases.

> **Reference:** [*Texture atlases, wrapping and mip mapping*](https://gamedev.stackexchange.com/questions/46963/how-to-avoid-texture-bleeding-in-a-texture-atlas)
> — GameDev Stack Exchange discussion on bleeding prevention.

Opaque and transparent blocks use separate `GeometryModel` output buckets in `ctx` so
the render pass can draw all opaque geometry before blending transparent geometry over it.

---

## Output Format

`MakeChunk()` signature:

```csharp
VerticesIndicesToLoad[] MakeChunk(
    int x, int y, int z,
    float[] lightLevels,
    ChunkTessellationContext ctx,
    out int retCount)
```

It returns a reference to the tessellator's **internal** `VerticesIndicesToLoad[]` array.
Only entries where `ModelData.IndicesCount > 0` contain live data; `retCount` gives the
number of live entries. The dispatcher immediately deep-copies all live entries into
`ArrayPool`-rented buffers before the tessellator is called again.

```csharp
public struct VerticesIndicesToLoad
{
    public GeometryModel ModelData;  // Xyz[], Uv[], Rgba[], Indices[]
    public float PositionX;          // chunk world-space origin
    public float PositionY;
    public float PositionZ;
    public int   Texture;            // OpenGL texture handle
    public bool  Transparent;        // drives render pass ordering
}
```

`GeometryModel` uses interleaved arrays (`float[] Xyz` = `[x0,y0,z0, x1,y1,z1, ...]`),
matching the VBO layout consumed by `glBufferData` on the render thread.

---

## Key Data Structures

| Field | Owner | Type | Purpose |
|---|---|---|---|
| `ctx.CurrentChunk` | Context | `int[]` (18³) | Block IDs written by dispatcher before `MakeChunk` |
| `ctx.CurrentChunkShadows` | Context | `byte[]` (18³) | Light levels copied from `ShadowBuffer` by dispatcher |
| `ctx.ChunkDraw16` | Context | `byte[]` (16³) | Per-block `TileSideFlags` bitmask from Pass 1 |
| `ctx.ChunkDrawCount16Flat` | Context | `byte[]` (16³ × 6) | Per-block-per-side draw flag from Pass 2 |
| `_blockFlags` | Tesselator | `BlockRenderFlags[]` (1024) | Transparent / Lowered / Fluid flags per block type; rebuilt only on block type change |
| `_cornerHeightLookup` | Tesselator | `int[6, 4]` | Maps (side, corner) → `CornerHeights` index; −1 = no modification |
| `c_OcclusionNeighbors` | Tesselator | `Vector3i[6][9]` | Neighbour offsets for AO sampling, one set per face direction |
| `ShadowBuffer` | WorkItem | `byte[]` (18³) | ArrayPool-rented lighting snapshot; read-only on worker; returned by dispatcher |

---

## Performance Notes

### What is already optimised

- **Face culling** — `CalculateVisibleFaces` skips all hidden faces before any geometry
  is emitted. Buried blocks contribute zero vertices.
- **Block flag cache** — `_blockFlags[]` packs three flags into one byte per block type,
  improving cache density during the inner loop. Rebuilt only on block type change, not
  per chunk.
- **Per-thread context** — all working buffers live in `ChunkTessellationContext`,
  allocated once per worker thread. No heap allocation occurs inside `MakeChunk`.
- **Flat draw-count buffer** — `ChunkDrawCount16Flat` uses a single contiguous allocation
  (`blockIndex * 6 + side`) instead of a jagged array.
- **Uniform chunk fast-path** — all-identical chunks skip tessellation entirely before
  any geometry work begins.
- **Struct submesh** — `VerticesIndicesToLoad` is a struct; the return array is a single
  contiguous heap object with no per-entry indirection.

### Known limitations

| Limitation | Impact | Notes |
|---|---|---|
| No greedy meshing | Each visible face = 1 quad. Flat 16×16 surface = 256 quads instead of 1. | Incompatible with per-vertex smooth lighting unless AO is disabled. |
| No SIMD | All vertex and colour math is scalar. | Bottleneck is memory latency, not compute; SIMD gain here is modest (~10–15%). |
| Two passes over 16³ data | `CalculateVisibleFaces` + `CalculateTilingCount` could be merged. | Halving the traversal count is a low-risk, meaningful improvement. |
| Interleaved vertex layout | `float[] Xyz` = `[x,y,z, x,y,z,...]` prevents SIMD on vertex writes. | Changing to SoA would require refactoring the GPU upload path. |

---

## External References

| Topic | Link |
|---|---|
| Ambient occlusion in voxel engines | [0fps.net — AO for Minecraft-like worlds](https://0fps.net/2013/07/03/ambient-occlusion-for-minecraft-like-worlds/) |
| Greedy meshing algorithm | [0fps.net — Meshing in a Minecraft game](https://0fps.net/2012/06/30/meshing-in-a-minecraft-game/) |
| Texture atlas bleeding fix | [GameDev SE — Avoiding texture bleeding](https://gamedev.stackexchange.com/questions/46963/how-to-avoid-texture-bleeding-in-a-texture-atlas) |
| Voxel face culling overview | [GPU Gems — Voxel rendering](https://developer.nvidia.com/gpugems/gpugems3/part-i-geometry/chapter-1-generating-complex-procedural-terrains-using-gpu) |
| .NET SIMD intrinsics | [Microsoft Docs — System.Runtime.Intrinsics](https://learn.microsoft.com/en-us/dotnet/api/system.runtime.intrinsics) |
| Manic Digger source | [github.com/manicdigger/manicdigger](https://github.com/manicdigger/manicdigger) |