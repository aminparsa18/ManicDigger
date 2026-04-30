# TerrainChunkTesselator

> Converts raw 3-D block data into renderable geometry for a single 16 × 16 × 16 chunk.  
> Lives inside the **Manic Digger** voxel engine, called every time a chunk becomes dirty.

---

## Table of Contents

1. [Overview](#overview)
2. [Where It Fits in the Pipeline](#where-it-fits-in-the-pipeline)
3. [Coordinate System](#coordinate-system)
4. [Data Layout: The 18³ Buffer](#data-layout-the-18³-buffer)
5. [Three-Pass Architecture](#three-pass-architecture)
   - [Pass 1 — CalculateVisibleFaces](#pass-1--calculatevisiblefaces)
   - [Pass 2 — CalculateTilingCount](#pass-2--calculatetilingcount)
   - [Pass 3 — BuildBlockPolygons](#pass-3--buildblockpolygons)
6. [Smooth Lighting (Ambient Occlusion)](#smooth-lighting-ambient-occlusion)
7. [Block Shape Variants](#block-shape-variants)
8. [Texture Atlas System](#texture-atlas-system)
9. [Output Format](#output-format)
10. [Key Data Structures](#key-data-structures)
11. [Performance Notes](#performance-notes)
12. [External References](#external-references)

---

## Overview

`TerrainChunkTesselator` is a **pure geometry factory**. Feed it a chunk of block IDs and
shadow values; it gives back vertex buffers ready for the GPU. It has no knowledge of cameras,
frame timing, or OpenGL state — that is all handled upstream by `ModDrawTerrain`.

```
ModDrawTerrain (orchestrator)
    │
    ├─ decides which chunks are dirty
    ├─ runs on background thread
    └─▶ TerrainChunkTesselator.MakeChunk(x, y, z, chunk18, shadows18)
            │
            └─▶ VerticesIndicesToLoad[]   (vertex + index buffers per texture atlas)
```

The class is intentionally **stateless between chunks** — all working buffers are
pre-allocated in `Start()` and reused, so no per-chunk heap allocation occurs during normal
operation.

---

## Where It Fits in the Pipeline

```
Server                         Client background thread        Main / render thread
──────                         ────────────────────────        ────────────────────
Generate chunk data            TerrainChunkTesselator          ModDrawTerrain
Save to SQLite          ──▶    MakeChunk()                ──▶  DrawTerrain()
Send to client                   ├─ CalculateVisibleFaces        Upload VBO to GPU
                                 ├─ CalculateTilingCount         glDrawElements()
                                 └─ BuildBlockPolygons
```

`ModDrawTerrain.UpdateTerrain()` (background thread) calls `MakeChunk()`.  
The result is queued as a `TerrainRendererCommit` and consumed on the main thread for
GPU upload. The tessellator itself never touches the GPU.

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

All three loops — `CalculateVisibleFaces`, `CalculateTilingCount`, `BuildBlockPolygons` —
iterate `xx` in the innermost loop so that sequential reads walk along the X axis,
maximising cache-line utilisation.

---

## Data Layout: The 18³ Buffer

The tessellator receives an **18 × 18 × 18** block array (`currentChunk18`) even though the
chunk is only 16 × 16 × 16. The extra 1-block border on every side holds the neighbouring
chunks' outermost layer.

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

- **Face visibility** — checking whether a face on a chunk edge is hidden by a block
  in the neighbouring chunk.
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

Results are written into `currentChunkDraw16`, one byte per block, where each bit
represents one of the 6 sides via `TileSideFlags`.

### Pass 2 — `CalculateTilingCount`

Translates the `TileSideFlags` bitmask from Pass 1 into a flat byte array
`currentChunkDrawCount16Flat` with layout:

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
into the appropriate `GeometryModel` bucket (selected by texture atlas page and
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

The `lightlevels[]` lookup converts the raw shadow byte (0–15) to a float before the
averaging, so the final per-vertex colour is already in linear space when written into
`GeometryModel.Rgba`.

> **Reference:** The algorithm is a simplified version of the technique described by
> 0fps in [*Ambient occlusion for Minecraft-like worlds*](https://0fps.net/2013/07/03/ambient-occlusion-for-minecraft-like-worlds/)
> (2013) — the canonical write-up on per-vertex AO for voxel engines.

---

## Block Shape Variants

`BuildSingleBlockPolygon` branches on `DrawType` to handle non-cube geometry:

| `DrawType`     | Description                              | Key behaviour                                   |
|----------------|------------------------------------------|-------------------------------------------------|
| `Solid`        | Full cube                                | Standard 6-face cube, all faces culled normally |
| `Fluid`        | Water / lava                             | Top face slightly lowered; side faces use special fluid visibility rules |
| `HalfHeight`   | Half-slab                                | `IsLowered = true`; top face always drawn; height offset applied |
| `Flat`         | Flower, grass, rail bed                  | Single cross or flat quad; no side faces        |
| `Rail`         | Sloped rail track                        | `CornerHeights` modified per `RailSlope` value; uses `_cornerHeightLookup` table |

The corner-height system uses `CornerHeights` (a value-type struct) and a pre-built
`_cornerHeightLookup[side, corner]` table to avoid a nested switch on every vertex:

```csharp
float GetCornerHeightModifier(TileSide side, Corner corner)
{
    int index = _cornerHeightLookup[(int)side, (int)corner];
    return index < 0 ? 0f : _cornerHeights[(Corner)index];
}
```

---

## Texture Atlas System

All block textures are packed into one or more **atlas pages**. The tessellator computes
UV coordinates per face using:

```
texRecTop    = (textureIndex / texturesPerAtlas) + ATI_artifact_fix
texRecBottom = texRecTop + (1 / texturesPerAtlas) − ATI_artifact_fix
```

The `AtiArtifactFix` is a UV inset (half-texel on desktop, 1.5 texels on WebGL) that
prevents texture bleeding at atlas tile borders — a well-known artefact in bilinear-filtered
atlases.

> **Reference:** [*Texture atlases, wrapping and mip mapping*](https://gamedev.stackexchange.com/questions/46963/how-to-avoid-texture-bleeding-in-a-texture-atlas)
> — GameDev Stack Exchange discussion on bleeding prevention.

Different transparent and opaque blocks use separate `GeometryModel` buckets
(`toreturnatlas1d` vs `toreturnatlas1dtransparent`) so the render pass can draw all
opaque geometry first before blending transparent geometry over it.

---

## Output Format

`MakeChunk()` returns a pre-allocated `VerticesIndicesToLoad[]` array. Only entries where
`ModelData.IndicesCount > 0` contain live data; `retCount` (out parameter) gives the number
of live entries.

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

`GeometryModel` uses **interleaved arrays** (`float[] Xyz` = `[x0,y0,z0, x1,y1,z1, ...]`),
matching the VBO layout consumed by `glBufferData` on the main thread.

---

## Key Data Structures

| Field | Type | Purpose |
|-------|------|---------|
| `currentChunk18` | `int[]` (18³) | Block IDs including 1-block border |
| `currentChunkShadows18` | `byte[]` (18³) | Raw light levels for the same border volume |
| `currentChunkDraw16` | `byte[]` (16³) | Per-block `TileSideFlags` bitmask from Pass 1 |
| `currentChunkDrawCount16Flat` | `byte[]` (16³ × 6) | Per-block-per-side draw flag from Pass 2 |
| `_blockFlags` | `BlockRenderFlags[]` (1024) | Transparent / Lowered / Fluid packed flags per block type |
| `_cornerHeightLookup` | `int[6, 4]` | Maps (side, corner) → `CornerHeights` index; −1 = no modification |
| `c_OcclusionNeighbors` | `Vector3i[6][9]` | Neighbour offsets for AO sampling, one set per face direction |
| `toreturnatlas1d` | `GeometryModel[]` | Output buckets — one per atlas page, opaque |
| `toreturnatlas1dtransparent` | `GeometryModel[]` | Output buckets — one per atlas page, transparent |
| `_verticesReturnBuffer` | `VerticesIndicesToLoad[]` | Pre-allocated return array; avoids per-chunk allocation |

---

## Performance Notes

### What is already optimised

- **Face culling** — `CalculateVisibleFaces` skips all hidden faces before any geometry
  is emitted. Buried blocks contribute zero vertices.
- **Pre-allocated buffers** — all working arrays and the return buffer are allocated once
  in `Start()`. No heap allocation occurs inside `MakeChunk()`.
- **Block flag cache** — `_blockFlags[]` packs three former `bool[]` arrays into one byte
  array, improving cache density during the inner loop.
- **Flat draw-count buffer** — `currentChunkDrawCount16Flat` uses a single contiguous
  allocation (`blockIndex * 6 + side`) instead of a jagged `byte[][]`.
- **Struct return value** — `VerticesIndicesToLoad` is a struct, so the return array is
  a single contiguous heap object with no per-entry indirection.

### Known limitations

| Limitation | Impact | Notes |
|------------|--------|-------|
| No greedy meshing | Each visible face = 1 quad. Flat 16×16 surface = 256 quads instead of 1. | Incompatible with per-vertex smooth lighting unless AO is disabled. |
| No SIMD | All vertex and colour math is scalar. | Bottleneck is memory latency, not compute; SIMD gain here is modest (~10–15%). |
| Two passes over 16³ data | `CalculateVisibleFaces` + `CalculateTilingCount` could be merged. | Halving the traversal count is a low-risk, meaningful improvement. |
| Interleaved vertex layout | `float[] Xyz` = `[x,y,z, x,y,z,...]` prevents SIMD on vertex writes. | Changing to SoA would require refactoring the GPU upload path. |

---

## External References

| Topic | Link |
|-------|------|
| Ambient occlusion in voxel engines | [0fps.net — AO for Minecraft-like worlds](https://0fps.net/2013/07/03/ambient-occlusion-for-minecraft-like-worlds/) |
| Greedy meshing algorithm | [0fps.net — Meshing in a Minecraft game](https://0fps.net/2012/06/30/meshing-in-a-minecraft-game/) |
| Texture atlas bleeding fix | [GameDev SE — Avoiding texture bleeding](https://gamedev.stackexchange.com/questions/46963/how-to-avoid-texture-bleeding-in-a-texture-atlas) |
| Voxel face culling overview | [GPU Gems — Voxel rendering](https://developer.nvidia.com/gpugems/gpugems3/part-i-geometry/chapter-1-generating-complex-procedural-terrains-using-gpu) |
| .NET SIMD intrinsics | [Microsoft Docs — System.Runtime.Intrinsics](https://learn.microsoft.com/en-us/dotnet/api/system.runtime.intrinsics) |
| Manic Digger source | [github.com/manicdigger/manicdigger](https://github.com/manicdigger/manicdigger) |

---

> **File:** `TerrainChunkTesselator_ci.cs`  
> **Namespace:** `ManicDigger`  
> **Dependencies:** `OpenTK.Mathematics`, `ModDrawHand3d`, `IGameClient`, `IGamePlatform`  
> **Thread safety:** Single-threaded per instance. `ModDrawTerrain` creates one instance
> and calls it exclusively from the background terrain thread.
