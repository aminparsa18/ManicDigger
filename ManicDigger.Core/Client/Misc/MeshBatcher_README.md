# MeshBatcher

> Groups 3D models by texture and renders them in two ordered passes —
> solid geometry first, transparent geometry second — minimising GPU texture
> switches and ensuring correct alpha blending.

---

## Table of Contents

1. [Overview](#overview)
2. [Where It Fits in the Pipeline](#where-it-fits-in-the-pipeline)
3. [Core Concepts](#core-concepts)
   - [Texture Batching](#texture-batching)
   - [Two-Pass Rendering](#two-pass-rendering)
   - [Slot Pool and Free-List](#slot-pool-and-free-list)
4. [API Reference](#api-reference)
   - [Add](#add)
   - [Remove](#remove)
   - [Draw](#draw)
   - [Clear](#clear)
   - [TotalTriangleCount](#totaltrianglecount)
5. [Internal Data Flow](#internal-data-flow)
6. [BatchEntry Record Struct](#batchentry-record-struct)
7. [Texture Management](#texture-management)
8. [Render Ordering and Transparency](#render-ordering-and-transparency)
9. [Relationship to TerrainChunkTesselator](#relationship-to-terraincunktesselator)
10. [Performance Notes](#performance-notes)
11. [Limits and Constants](#limits-and-constants)
12. [External References](#external-references)

---

## Overview

`MeshBatcher` is the **last step before the GPU** in the Manic Digger render pipeline.
It sits between the tessellator (which produces raw vertex data) and `glDrawElements`
(which consumes it). Its two jobs are:

1. **Batch by texture** — group all models sharing the same OpenGL texture handle so the
   GPU only switches texture state between groups, not between every model.
2. **Order by opacity** — draw all solid models before transparent ones, so the depth
   buffer is fully populated before blending begins.

```
TerrainChunkTesselator       MeshBatcher              GPU
──────────────────────       ───────────              ───
VerticesIndicesToLoad[] ──▶  Add(model, texture) ──▶  Solid pass
                             Draw() each frame   ──▶  Transparent pass
                             Remove(id)          ──▶  DeleteModel()
```

The class never generates geometry — it only stores, organises, and issues draw calls.

---

## Where It Fits in the Pipeline

```
ModDrawTerrain (background thread)
    │
    └─▶ TerrainChunkTesselator.MakeChunk()
            │
            └─▶ VerticesIndicesToLoad[]
                    │
                    ▼  (main thread, TerrainRendererCommit)
              MeshBatcher.Add(modelData, transparent, texture, ...)
                    │
                    ▼  (once per frame)
              MeshBatcher.Draw(playerX, playerY, playerZ)
                    │
                    ├─▶ [Solid pass]       glBindTexture → DrawModels
                    └─▶ [Transparent pass] glDisableCullFace → glBindTexture → DrawModels
                                           glEnableCullFace
```

`ModDrawTerrain` calls `Add` whenever a chunk is re-tessellated, `Remove` when a chunk is
unloaded or replaced, and `Draw` once every render frame inside `OnNewFrameDraw3d`.

---

## Core Concepts

### Texture Batching

Switching the active texture on the GPU is a state-change that flushes the pipeline.
On a scene with hundreds of chunk models, issuing one `glBindTexture` per model would
cause hundreds of pipeline stalls per frame.

`MeshBatcher` avoids this by sorting models into **per-texture buckets**
(`_tocallSolid[textureIndex]`, `_tocallTransparent[textureIndex]`).  
During `Draw`, each texture is bound **once**, then all models sharing that texture are
drawn in a single batch before moving to the next texture.

> **Reference:** [*Render State Changes*](https://www.khronos.org/opengl/wiki/Rendering_Pipeline_Overview)
> — Khronos OpenGL Wiki overview of why state changes are expensive.

### Two-Pass Rendering

Transparent geometry (water, glass, leaves) must be drawn **after** all solid geometry so
the depth buffer can reject fragments hidden behind opaque surfaces. Drawing transparent
objects first would cause them to write to the depth buffer and silently discard solid
geometry that should be visible through them.

```
Frame N render order:
  1. Solid pass   → fills depth buffer with terrain, blocks, entities
  2. Transparent  → blended on top; depth test reads the solid depth values
```

Back-face culling is also disabled during the transparent pass (`GlDisableCullFace`) so
that both the front and back faces of a water surface are visible when the camera enters
the volume.

> **Reference:** [*Transparency Sorting*](https://learnopengl.com/Advanced-OpenGL/Blending)
> — LearnOpenGL chapter on blending and transparency ordering.

### Slot Pool and Free-List

Models are stored in a fixed array `_models[ModelsMax]`. When `Add` is called:

1. If `_freeSlots` (a `Stack<int>`) is non-empty, the top index is popped and reused.
2. Otherwise, `_modelsCount` is incremented and the new tail slot is used.

When `Remove` is called, the slot index is pushed onto `_freeSlots`. This means slots
are reused in **LIFO order**, which keeps the active window of `_models` compact when
models are added and removed at similar rates — important for the `SortListsByTexture`
loop which iterates up to `_modelsCount`.

```
_models:  [ A | B |   | D |   |   | G ]
                 ↑           ↑   ↑
             freed slots in _freeSlots stack
             next Add() → pops index 6, then 5, then 2
```

---

## API Reference

### `Add`

```csharp
public int Add(GeometryModel modelData, bool transparent, int texture,
               float centerX, float centerY, float centerZ, float radius)
```

Registers a model and returns a **stable slot ID** valid until `Remove` is called.

- Uploads geometry to the GPU via `_platform.CreateModel(modelData)`.
- Registers the texture handle in the `_textureIndexMap` if not already present.
- The `centerX/Y/Z` and `radius` parameters are stored in the `BatchEntry` for future
  frustum culling (currently stored but not yet evaluated inside `Draw`).
- Throws `InvalidOperationException` if more than `MaxTextures` (10) distinct texture
  handles are registered.

### `Remove`

```csharp
public void Remove(int id)
```

Releases the slot and frees GPU memory via `_platform.DeleteModel`. The slot is pushed
onto `_freeSlots` for immediate reuse. Calling `Remove` with an already-empty slot is
undefined — guard with `!entry.Empty` if necessary.

### `Draw`

```csharp
public void Draw(float playerPositionX, float playerPositionY, float playerPositionZ)
```

Called once per frame. Internally:

1. Calls `SortListsByTexture()` to rebuild the per-texture draw lists from `_models`.
2. Solid pass: iterates `_tocallSolid`, binding each texture and calling `DrawModels`.
3. Transparent pass: disables cull face, iterates `_tocallTransparent`, re-enables cull face.

The player position parameters are accepted for future **view-distance culling** or
**back-to-front sorting** of transparent models but are not currently used inside the method.

### `Clear`

```csharp
public void Clear()
```

Calls `Remove` on every active slot, then clears the texture registry. This releases
**all GPU memory** held by the batcher. It is not a lightweight reset — use it only on
scene transitions or full world reloads.

### `TotalTriangleCount`

```csharp
public int TotalTriangleCount()
```

Iterates all active slots and sums `IndicesCount / 3`. Useful for performance overlays and
debugging. Does not include empty slots.

---

## Internal Data Flow

Per-frame `Draw` call in detail:

```
Draw()
  │
  └─▶ SortListsByTexture()
        │
        ├─ Clear all _tocallSolid[i] and _tocallTransparent[i] lists
        └─ for i in [0, _modelsCount):
               if _models[i].Empty  → skip
               if Transparent       → _tocallTransparent[Texture].Add(Model)
               else                 → _tocallSolid[Texture].Add(Model)
  │
  ├─▶ for each texture i:
  │       if _tocallSolid[i].Count > 0:
  │           BindTexture2d(_glTextures[i])
  │           DrawModels(_tocallSolid[i])
  │
  └─▶ GlDisableCullFace()
      for each texture i:
          if _tocallTransparent[i].Count > 0:
              BindTexture2d(_glTextures[i])
              DrawModels(_tocallTransparent[i])
      GlEnableCullFace()
```

`SortListsByTexture` rebuilds the draw lists from scratch every frame. This is intentional:
it avoids tracking dirty flags on individual models, which would complicate `Add`/`Remove`
and be error-prone when models change transparency or texture mid-frame.

---

## BatchEntry Record Struct

```csharp
public readonly record struct BatchEntry
{
    public bool          Empty;        // slot available for reuse?
    public int           IndicesCount; // triangle count × 3
    public float         CenterX/Y/Z; // bounding sphere centre (world space)
    public float         Radius;       // bounding sphere radius
    public bool          Transparent;  // which render pass?
    public int           Texture;      // logical texture index
    public GeometryModel Model;        // GPU handle
}
```

Using `readonly record struct` means the entire `_models[ModelsMax]` array is a **single
contiguous allocation** — no per-slot heap objects, no GC pressure, and cache lines
cover multiple entries simultaneously. The `with` expression in `Remove` creates an
updated copy in-place without boxing:

```csharp
_models[id] = _models[id] with { Empty = true };
```

---

## Texture Management

Textures are stored in two parallel structures:

| Structure | Type | Purpose |
|-----------|------|---------|
| `_glTextures` | `List<int>` | Ordered list of GL handles; index = logical texture ID |
| `_textureIndexMap` | `Dictionary<int, int>` | Maps GL handle → logical ID; O(1) lookup |

This replaces a previous `Array.IndexOf` linear scan that was O(n) on every `Add` call.
With `MaxTextures = 10`, the practical difference is small, but the dictionary also removes
the old convention that treated handle `0` as "unoccupied" — handle 0 is now a valid
texture identifier.

The logical texture ID (0–9) is what gets stored in `BatchEntry.Texture` and used to
index `_tocallSolid[]` and `_tocallTransparent[]`. The raw GL handle is only used when
`BindTexture2d` is called during `Draw`.

---

## Render Ordering and Transparency

The two-pass strategy is the simplest correct solution for opaque + transparent geometry:

```
Pass 1 — SOLID (depth writes ON, depth test ON, cull face ON)
    All terrain blocks, entities, and opaque models land in the depth buffer.

Pass 2 — TRANSPARENT (depth writes ON, depth test ON, cull face OFF)
    Water, glass, leaves. Back-face culling is disabled so submerged surfaces
    render from both sides when the camera enters a fluid volume.
```

One limitation of this approach: **transparent models are not sorted back-to-front within
the transparent pass**. For scenes with multiple overlapping transparent surfaces
(e.g. looking through a glass window into water), artifacts can appear depending on
draw order. A full fix requires per-model depth sorting before the transparent pass —
the `playerPositionX/Y/Z` parameters in `Draw` exist precisely for this future extension.

> **Reference:** [*Order Independent Transparency*](https://learnopengl.com/Guest-Articles/2020/OIT/Introduction)
> — LearnOpenGL overview of OIT techniques for when sorting isn't enough.

---

## Relationship to TerrainChunkTesselator

These two classes are complementary and intentionally decoupled:

| Concern | TerrainChunkTesselator | MeshBatcher |
|---------|------------------------|-------------|
| Runs on | Background thread | Main / render thread |
| Input | `int[]` block data | `GeometryModel` vertex buffers |
| Output | `VerticesIndicesToLoad[]` | GPU draw calls |
| Knows about camera? | No | No (player pos stored, not used yet) |
| GPU calls? | None | `CreateModel`, `DeleteModel`, `DrawModels` |
| Lifetime of data | Per-chunk rebuild | Stable between dirty marks |

`TerrainChunkTesselator` produces the raw geometry; `MeshBatcher` owns the GPU lifetime
of that geometry. The handoff happens in `TerrainRendererCommit` on the main thread:
the old slot is `Remove`d, the new geometry is `Add`ed, and the slot ID is stored back
in the chunk's `RenderedChunk` record.

---

## Performance Notes

| Aspect | Current behaviour | Notes |
|--------|-------------------|-------|
| Texture switch cost | One `BindTexture2d` per texture per frame | Maximum 10 switches; near-optimal for this texture budget |
| `SortListsByTexture` cost | O(n) over `_modelsCount` every frame | Unavoidable without dirty tracking; acceptable for ≤16 K slots |
| Free-list reuse | LIFO via `Stack<int>` | Keeps `_modelsCount` compact; avoids O(n) scan growth over time |
| Per-slot allocation | Zero — `BatchEntry` is a struct embedded in `_models[]` | No GC pressure from model add/remove churn |
| Transparent sort | Not implemented | Can cause visual artifacts with overlapping transparent geometry |
| Frustum culling | `CenterX/Y/Z` and `Radius` stored but unused in `Draw` | Sphere-frustum test would reduce `DrawModels` call volume significantly |

The most impactful missing feature is **frustum culling inside `Draw`**. With
`_modelsCount` at its maximum of 16 384 and a typical view frustum covering ~30–40% of
registered chunks, culling could eliminate over half of all `DrawModels` calls per frame
with a simple sphere-frustum intersection test using the already-stored bounding sphere.

> **Reference:** [*Frustum Culling*](https://learnopengl.com/Guest-Articles/2021/Scene/Frustum-Culling)
> — LearnOpenGL sphere-frustum culling implementation guide.

---

## Limits and Constants

| Constant | Value | Meaning |
|----------|-------|---------|
| `ModelsMax` | `1024 × 16 = 16 384` | Maximum simultaneous model slots |
| `MaxTextures` | `10` | Maximum distinct GL texture handles |

`ModelsMax` covers a worst-case view distance of roughly 8 chunks in each horizontal
direction (8 × 8 × vertical layers × 2 atlas pages ≈ several thousand). Exceeding it
requires either increasing the constant or evicting distant chunks before adding new ones.

`MaxTextures` is currently hardcoded. If the block texture atlas is ever split into
more than 10 pages (e.g. higher-resolution texture packs), both this constant and the
fixed-size `_tocallSolid[]` / `_tocallTransparent[]` arrays must grow together.

---

## External References

| Topic | Link |
|-------|------|
| GPU state change cost | [Khronos — Rendering Pipeline Overview](https://www.khronos.org/opengl/wiki/Rendering_Pipeline_Overview) |
| Transparency and blending | [LearnOpenGL — Blending](https://learnopengl.com/Advanced-OpenGL/Blending) |
| Order-independent transparency | [LearnOpenGL — OIT Introduction](https://learnopengl.com/Guest-Articles/2020/OIT/Introduction) |
| Frustum culling (sphere test) | [LearnOpenGL — Frustum Culling](https://learnopengl.com/Guest-Articles/2021/Scene/Frustum-Culling) |
| Free-list allocator pattern | [Game Programming Patterns — Object Pool](https://gameprogrammingpatterns.com/object-pool.html) |
| Render ordering in voxel engines | [0fps.net — Transparency in WebGL](https://0fps.net/2013/11/14/0-4-0-release-voxels-lighting/) |

---

> **File:** `MeshBatcher.cs`  
> **Namespace:** `ManicDigger`  
> **Dependencies:** `IGamePlatform`, `IMeshDrawer`, `GeometryModel`, `BatchEntry`  
> **Thread safety:** All public methods must be called from the **main / render thread**.
> `Add` and `Remove` modify GPU state via `CreateModel` / `DeleteModel` and are not safe
> to call from the background tessellation thread.
