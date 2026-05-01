# Game — 2D Rendering & Block Geometry

> Partial `Game` class responsible for block height queries, 2D screen-space rendering,
> text drawing, and circle primitives.  
> All geometry is pre-allocated and reused across frames — no per-call heap allocation
> occurs during normal operation.

---

## Table of Contents

1. [Overview](#overview)
2. [Dependencies](#dependencies)
3. [Block Geometry](#block-geometry)
4. [The 2D Coordinate System](#the-2d-coordinate-system)
5. [2D Drawing Pipeline](#2d-drawing-pipeline)
   - [Entry Point](#entry-point)
   - [Simple Quad](#simple-quad)
   - [Atlas Quad](#atlas-quad)
   - [Batch Draw](#batch-draw)
6. [Texture Atlas UV Mapping](#texture-atlas-uv-mapping)
7. [Text Rendering](#text-rendering)
8. [Circle Primitive](#circle-primitive)
9. [Pre-allocated Models](#pre-allocated-models)
10. [Per-frame Flow](#per-frame-flow)
11. [Performance Notes](#performance-notes)
12. [External References](#external-references)

---

## Overview

This partial class handles everything between "I have pixels in memory" and "they appear on screen as UI". It owns three concerns:

- **Block geometry** — column height scanning and per-block visual height fractions used by physics and the tessellator.
- **2D drawing** — textured quads for HUD elements, inventory icons, and any screen-space UI drawn by mods.
- **Primitives** — text via a texture cache and circle outlines via a line-loop model.

None of this code touches game logic or the player. It is purely a rendering utility layer called from `Draw2d` (once per frame) and from mods via `OnNewFrameDraw2d`.

---

## Dependencies

| Dependency | Role |
|---|---|
| `IOpenGlService` | Texture binding, buffer upload (`UpdateModel`), depth/cull state |
| `IMeshDrawer` | Matrix stack (`OrthoMode / PerspectiveMode`, `GLPushMatrix`, etc.) and draw dispatch |
| `TextureAtlas` | Converts packed-atlas tile index → UV `RectangleF` |
| `ColorUtils` | Packs/unpacks ARGB colour integers |
| `GeometryModel` | Vertex/index/colour/UV buffer container shared with the 3D pipeline |

`IOpenGlService` and `IMeshDrawer` are injected at construction; this class never calls OpenGL directly.

---

## Block Geometry

### `Blockheight(x, y, z_)`

Scans the column `(x, y)` downward from `z_` and returns the 1-based Z of the highest non-air block, or 0 if the column is entirely air.

```
z = z_ (top)
 │   block? → return z + 1
 │   air?   → continue
 ▼
z = 0 (bottom)
 │   still air? → return 0
```

Used by the heightmap, tree placement, and surface-finding code.

### `Getblockheight(x, y, z)`

Returns the visual height of a single block as a fraction of a full block. This fraction is used by the tessellator when computing the adjusted bounding box for sloped or short geometry, and by the camera wall-collision raycast:

```
┌─────────────────────────────────────────────┐
│  Full block          █████████████  1.00    │
│                      █████████████          │
│                                             │
│  HalfHeight slab     ▓▓▓▓▓▓▓▓▓▓▓▓▓  0.50   │
│  (half-block)                               │
│                                             │
│  Rail                ░░░░░░░░░░░░░  0.30   │
│  (3/10 height)                              │
│                                             │
│  Flat (flower/grass) ·············  0.05   │
└─────────────────────────────────────────────┘
```

Out-of-bounds positions return 1.0 — treated as solid to prevent the camera or physics from clipping through the map edge.

---

## The 2D Coordinate System

All 2D drawing happens inside an orthographic projection set up by `IMeshDrawer.OrthoMode`:

```
(0,0) ──────────────────────────▶ X  (canvas width)
  │
  │     Screen-space coordinates.
  │     Origin is top-left.
  │     +Y goes downward.
  │
  ▼
  Y  (canvas height)
```

This is the standard screen-space convention used in most game UIs and is opposite to OpenGL's default (Y-up). `OrthoMode` configures `glOrtho(0, w, h, 0, 0, 1)` — note `bottom = h, top = 0` — which flips Y so that (0, 0) is top-left.

> **Reference:** [LearnOpenGL — Coordinate Systems](https://learnopengl.com/Getting-started/Coordinate-Systems)

---

## 2D Drawing Pipeline

### Entry Point

```
Draw2dTexture(textureId, x, y, w, h, inAtlasId?, atlastextures, color, depthTest)
        │
        ├─── color == white AND inAtlasId == null?
        │           └── Draw2dTextureSimple       (pre-built VAO, 3 matrix ops)
        │
        └─── otherwise
                    └── Draw2dTextureInAtlas       (FillAtlasQuadModel → UpdateModel → DrawModelData)
                    └── Draw2dTexturePart          (same path, explicit UV extents)
```

The routing exists because the "simple" path avoids per-frame CPU work entirely — the VAO is built once and only the matrix transform changes. The atlas path must rewrite UV coordinates into the VBO every call.

---

### Simple Quad

`_quadModel` is a VAO allocated once from `Quad.Create()`. Its vertices form a unit square centred at the origin:

```
  (-1,-1)──────(1,-1)
     │              │
     │    origin    │   ← vertices in object space
     │    (0,0)     │
  (-1, 1)──────(1, 1)
```

To place it at screen position `(x, y)` with size `(w, h)`, the matrix stack applies:

```
Translate(x + w×0.5,  y + h×0.5)   ← move centre to target position
    · Scale(w×0.5, h×0.5)           ← stretch unit square to desired size
```

This is equivalent to the legacy five-operation sequence (push → translate → scale → scale → translate) collapsed into three (push → translate+scale → pop), keeping the matrix stack lean.

> **Reference:** [OpenGL Matrix Math — songho.ca](http://www.songho.ca/opengl/gl_transform.html)

---

### Atlas Quad

When a specific tile from a packed texture atlas is needed, `FillAtlasQuadModel` writes four vertices directly into the pre-allocated `_atlasQuadModel` arrays:

```
Screen space (Xyz):              Atlas UV space (Uv):

(x,    y   ) ── (x+w, y   )     (u,    v   ) ── (u+uw, v   )
    │                   │            │                   │
    │                   │            │     tile          │
    │                   │            │                   │
(x,    y+h ) ── (x+w, y+h )     (u,    v+vh) ── (u+uw, v+vh)
```

Index layout (two triangles, counter-clockwise):
```
  0 ──── 1
  │  ╲   │
  │   ╲  │
  3 ──── 2

  Triangle 1: [0, 1, 2]
  Triangle 2: [0, 2, 3]
```

After filling, `UpdateModel` calls `glBufferSubData` on the existing VBOs — no VAO recreation, no heap allocation. The index array `[0,1,2, 0,2,3]` is set once on first use and never changes.

> **Reference:** [OpenGL VBO and glBufferSubData — khronos.org](https://www.khronos.org/opengl/wiki/Buffer_Object#Data_Specification)

---

### Batch Draw

`Draw2dTextures` renders many quads sharing the same texture in a single GPU draw call. This is the most performance-sensitive path — it is used for inventory grids, chat lines, and any HUD with repeated tile icons.

```
Input: Draw2dData[N]  (x, y, w, h, atlasId, color)
          │
          ▼
for i in [0, N):
    _batchModelScratch[i]  ← lazily allocated once, then overwritten in-place
    FillQuadModel(...)     ← writes Xyz, Uv, Rgba directly into scratch arrays

          │
          ▼
CombineModelDataInPlace(_batchModelScratch, N, ref _combinedModel)
    │
    ├── totalVertices = N × 4,  totalIndices = N × 6
    ├── only reallocates _combinedModel when current arrays are too small
    └── concatenates all Xyz / Uv / Rgba, rebasing index offsets:
            quad 0: indices [0,1,2, 0,2,3]
            quad 1: indices [4,5,6, 4,6,7]   ← baseVertex = 4
            quad 2: indices [8,9,10, 8,10,11] ← baseVertex = 8
            ...

          │
          ▼
UpdateModel(_combinedModel)   ← glBufferSubData: one upload
DrawModelData(_combinedModel) ← glDrawElements: one call
```

Without batching, N quads would require N bind + N draw calls. With batching, it is always 1 bind + 1 draw regardless of N.

> **Reference:** [Batch Rendering — learnopengl.com](https://learnopengl.com/Advanced-OpenGL/Instancing)  
> **Reference:** [Why Batch Draw Calls — GPU Gems](https://developer.nvidia.com/gpugems/gpugems/part-iv-image-processing/chapter-28-graphics-pipeline-performance)

---

## Texture Atlas UV Mapping

A texture atlas packs many tiles into a single texture to minimise GPU texture switches. `TextureAtlas.TextureCoords2d` converts a linear tile index into UV coordinates for a square atlas:

```
Atlas layout (texturesPacked = 4):

 ┌────┬────┬────┬────┐  v=0.00
 │  0 │  1 │  2 │  3 │
 ├────┼────┼────┼────┤  v=0.25
 │  4 │  5 │  6 │  7 │
 ├────┼────┼────┼────┤  v=0.50
 │  8 │  9 │ 10 │ 11 │
 ├────┼────┼────┼────┤  v=0.75
 │ 12 │ 13 │ 14 │ 15 │
 └────┴────┴────┴────┘  v=1.00
u=0  0.25 0.50 0.75  1.00

For tile index 6 (texturesPacked = 4):
    col = 6 % 4 = 2  →  u = 2 × 0.25 = 0.50
    row = 6 / 4 = 1  →  v = 1 × 0.25 = 0.25
    UV rect = (0.50, 0.25, width=0.25, height=0.25)
```

> **Reference:** [Texture Atlases — Beginner's Guide (GameDev.net)](https://www.gamedev.net/articles/programming/graphics/texture-packing-r3781/)  
> **Reference:** [Avoiding Texture Bleeding in Atlases — GameDev SE](https://gamedev.stackexchange.com/questions/46963/how-to-avoid-texture-bleeding-in-a-texture-atlas)

---

## Text Rendering

Text rendering is expensive — rasterising a glyph string to a bitmap involves GDI+ calls and a GPU texture upload. The system avoids repeating this work by caching the result keyed on every property that affects appearance:

```
TextStyle cache key:
    { Text, Color, FontSize, FontFamily, FontStyle }
              │
              ▼
    CachedTextTextures (Dictionary<TextStyle, CachedTexture>)
              │
    Hit  ──── CachedTexture.lastuseMilliseconds = now
    │               └── Draw2dTexture(cached.textureId, ...)
    │
    Miss ──── MakeTextTexture(style)
                  │  rasterise with GDI+
                  │  upload bitmap to GPU
                  └── store in CachedTextTextures
```

### Eviction

`DeleteUnusedCachedTextTextures` runs **once per frame** at the end of `Draw2d`, after all mods have drawn. It evicts entries whose `lastuseMilliseconds` is older than a threshold, freeing the GPU texture handle. Running it once per frame rather than inside `Draw2dText` means:

- A texture used multiple times in one frame is never evicted mid-pass.
- Eviction cost is paid exactly once regardless of how many text draws occur.

### Font cache

`Draw2dText1` adds a `_fontCache` keyed by point size so `new Font(...)` is not called every frame. GDI `Font` objects hold unmanaged handles and must be explicitly disposed — call `ClearFontCache()` when font settings change (window resize, DPI change, options update).

> **Reference:** [Text Rendering in Games — learnopengl.com](https://learnopengl.com/In-Practice/Text-Rendering)  
> **Reference:** [GDI+ Font Disposal — Microsoft Docs](https://learn.microsoft.com/en-us/dotnet/api/system.drawing.font.dispose)

---

## Circle Primitive

`Circle3i` draws a 32-segment circle outline in screen space using a line-loop model:

```
Segment layout (32 segments, shown as 8 for clarity):

        *   *
      *       *
     *    ×    *    ← centre (x, y) — not a vertex
      *       *
        *   *

Each segment is a line pair [i, (i+1) % 32].
Indices: [0,1, 1,2, 2,3, ..., 31,0]  ← set once, never changes
```

On each call, only the `Xyz` array is recomputed:

```csharp
angle = i × 2π / 32
Xyz[i] = (x + cos(angle) × radius,
           y + sin(angle) × radius,
           0)
```

`UpdateModel` streams the new positions to the GPU; `DrawModelData` issues the draw. Indices and colours (`Rgba` = all 255) are written once on first use.

> **Reference:** [Circle Generation with Trigonometry — iquilezles.org](https://iquilezles.org/articles/functions/)

---

## Pre-allocated Models

The zero-allocation design means all `GeometryModel` objects are allocated during the first call to each path and reused indefinitely thereafter:

```
First call                         Subsequent calls
────────────────────────────────   ────────────────────────────────────────
_quadModel         CreateModel()   No-op — VAO is static, only matrix changes
_atlasQuadModel    new GM + init   FillAtlasQuadModel() overwrites arrays in-place
_batchModelScratch new GM per slot FillQuadModel() overwrites arrays in-place
_combinedModel     new GM          CombineModelDataInPlace() reuses if large enough
_circleModelData   new GM + init   Only Xyz[] recomputed per call
```

| Field | Max size | Grows? |
|---|---|---|
| `_quadModel` | Fixed (4 verts) | Never |
| `_atlasQuadModel` | Fixed (4 verts) | Never |
| `_batchModelScratch` | 512 slots × 4 verts | Never (capped at 512) |
| `_combinedModel` | 512 × 4 = 2048 verts | Only when batch exceeds capacity |
| `_circleModelData` | 32 verts | Never |

> **Reference:** [Object Pool Pattern — Game Programming Patterns](https://gameprogrammingpatterns.com/object-pool.html)

---

## Per-frame Flow

```
Game.OnNewFrameDraw3d(dt)
    │
    └── Draw2d(dt)
            │
            ├── ENABLE_DRAW2D? → return early if false
            │
            ├── meshDrawer.OrthoMode(canvasW, canvasH)
            │       ├── GLMatrixModeProjection → push ortho matrix
            │       └── GLMatrixModeModelView  → push identity
            │
            ├── for each mod in ClientMods:
            │       mod.OnNewFrameDraw2d(game, dt)
            │           │
            │           ├── Draw2dTexture(...)       simple or atlas quad
            │           ├── Draw2dTextures(...)      batch quad (1 GPU call for N quads)
            │           ├── Draw2dText(...) / Draw2dText1(...)
            │           │       └── texture cache hit/miss → Draw2dTexture
            │           └── Circle3i(...)            line-loop outline
            │
            ├── DeleteUnusedCachedTextTextures()     ← once per frame, not per draw
            │
            └── meshDrawer.PerspectiveMode()
                    ├── GLMatrixModeProjection → pop ortho matrix (restore 3D projection)
                    └── GLMatrixModeModelView  → pop identity    (restore 3D model-view)
```

---

## Performance Notes

| Technique | Benefit |
|---|---|
| Pre-allocated `GeometryModel` objects | Zero heap allocation during the draw pass |
| `glBufferSubData` instead of `glBufferData` | Avoids GPU buffer reallocation; streams only changed data |
| Batch draw — single `glDrawElements` for N quads | Eliminates N−1 draw call overheads and N−1 state-change round-trips |
| Matrix collapse in simple quad | 5 matrix ops → 3; saves 2 push/pop cycles per HUD element |
| Text eviction once per frame | Prevents mid-pass texture deletion and amortises the scan cost |
| `_combinedModel` grows monotonically | No repeated allocation for similarly-sized batches across frames |
| `_fontCache` by point size | Prevents per-frame GDI `Font` allocation and unmanaged handle churn |

---

## External References

| Topic | Link |
|---|---|
| Orthographic projection and coordinate systems | [LearnOpenGL — Coordinate Systems](https://learnopengl.com/Getting-started/Coordinate-Systems) |
| OpenGL matrix transform math | [Songho — OpenGL Transformations](http://www.songho.ca/opengl/gl_transform.html) |
| VBO buffer updates (`glBufferSubData`) | [Khronos — Buffer Object Data Specification](https://www.khronos.org/opengl/wiki/Buffer_Object#Data_Specification) |
| Batch rendering and instancing | [LearnOpenGL — Instancing](https://learnopengl.com/Advanced-OpenGL/Instancing) |
| Draw call batching performance | [GPU Gems — Graphics Pipeline Performance](https://developer.nvidia.com/gpugems/gpugems/part-iv-image-processing/chapter-28-graphics-pipeline-performance) |
| Texture atlas layout and UV | [GameDev.net — Texture Packing](https://www.gamedev.net/articles/programming/graphics/texture-packing-r3781/) |
| Texture atlas bleeding prevention | [GameDev SE — Avoiding Texture Bleeding](https://gamedev.stackexchange.com/questions/46963/how-to-avoid-texture-bleeding-in-a-texture-atlas) |
| Text rendering techniques | [LearnOpenGL — Text Rendering](https://learnopengl.com/In-Practice/Text-Rendering) |
| GDI Font disposal | [Microsoft Docs — Font.Dispose](https://learn.microsoft.com/en-us/dotnet/api/system.drawing.font.dispose) |
| Circle generation with trigonometry | [Inigo Quilez — Functions](https://iquilezles.org/articles/functions/) |
| Object pool / pre-allocation pattern | [Game Programming Patterns — Object Pool](https://gameprogrammingpatterns.com/object-pool.html) |
