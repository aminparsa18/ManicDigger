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
5. [Pending Upload and Unload Queue](#pending-upload-and-unload-queue)
   - [StageChunk](#stagechunk)
   - [StageUnload](#stageunload)
   - [FlushPendingUploads](#flushpeninguploads)
6. [Internal Data Flow](#internal-data-flow)
7. [BatchEntry Record Struct](#batchentry-record-struct)
8. [Texture Management](#texture-management)
9. [Render Ordering and Transparency](#render-ordering-and-transparency)
10. [Relationship to ChunkTessellationDispatcher](#relationship-to-chunktessellationdispatcher)
11. [Performance Notes](#performance-notes)
12. [Limits and Constants](#limits-and-constants)
13. [External References](#external-references)

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
ChunkTessellationDispatcher    MeshBatcher                    GPU
───────────────────────────    ───────────                    ───
StageChunk(chunk, meshes)  ──▶ _pendingUploads queue
StageUnload(chunk)         ──▶ _pendingUnloads queue
                               FlushPendingUploads() ──▶  Add / Remove
                               Draw() each frame     ──▶  Solid pass
                                                     ──▶  Transparent pass
```

The class never generates geometry — it only stores, organises, and issues draw calls.
`DoRedraw` (previously in `ModDrawTerrain`) now lives here, keeping all GPU-lifetime
management in one place.

---

## Where It Fits in the Pipeline

```
ChunkTessellationDispatcher (worker thread)
    │
    └─▶ StageChunk(chunk, meshes, meshCount, dataRented)
            │
            └─▶ _pendingUploads  (ConcurrentQueue — thread-safe handoff)
                    │
                    ▼  (render thread — OnRender3d)
              MeshBatcher.FlushPendingUploads()
                    │
                    ├─▶ drain _pendingUnloads → Remove old slots, free light buffer
                    └─▶ drain _pendingUploads → DoRedraw → Add new slots
                    │
                    ▼  (once per frame)
              MeshBatcher.Draw(playerX, playerY, playerZ)
                    │
                    ├─▶ [Solid pass]       glBindTexture → DrawModels
                    └─▶ [Transparent pass] glDisableCullFace → glBindTexture → DrawModels
                                           glEnableCullFace
```

`ModDrawTerrain` calls `FlushPendingUploads` and `Draw` each render frame.
Worker threads call `StageChunk` (and `StageUnload` for evicted chunks) from any thread
via the `ConcurrentQueue` — no locking required on the producer side.

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

- Uploads geometry to the GPU via `_openGlService.CreateModel(modelData)`.
- Registers the texture handle in the `_textureIndexMap` if not already present.
- The `centerX/Y/Z` and `radius` parameters are accepted for API compatibility and future
  frustum culling but are not stored in `BatchEntry` or evaluated inside `Draw`.
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

The player position parameters are accepted for future **back-to-front sorting** of
transparent models but are not currently used inside the method.

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

## Pending Upload and Unload Queue

Worker threads must never call `Add` or `Remove` directly — both touch GPU state.
Instead, they write to two `ConcurrentQueue`s that are drained on the render thread by
`FlushPendingUploads`.

### `StageChunk`

```csharp
public void StageChunk(Chunk chunk, VerticesIndicesToLoad[] meshes, int meshCount, bool dataRented)
```

Called by `ChunkTessellationDispatcher` (worker thread) after `MakeChunk` completes.
Enqueues a `PendingUpload` record — the render thread will call `DoRedraw` for it on the
next `FlushPendingUploads`. If `dataRented` is true, the `ArrayPool<VerticesIndicesToLoad>`
buffer is returned to the pool after `DoRedraw` finishes.

### `StageUnload`

```csharp
public void StageUnload(Chunk chunk)
```

Enqueues a chunk for removal. `FlushPendingUploads` processes unloads before uploads,
freeing slots before attempting to fill them. In addition to calling `Remove` on all
batcher IDs, the unload path also returns the chunk's `rendered.Light` buffer to
`ArrayPool<byte>` if it was rented, and marks `rendered.Dirty = true` so the chunk will
be re-tessellated if it re-enters the view distance.

### `FlushPendingUploads`

```csharp
public void FlushPendingUploads(int maxUploadsPerFrame = 512)
```

Called once per frame on the render thread, before `Draw`. Drains both queues in order:

1. **Unload queue** (unbounded drain) — removes old batcher entries and frees light buffers.
2. **Upload queue** (capped at `maxUploadsPerFrame`) — calls `DoRedraw` for each pending
   chunk, which removes any existing entries for that chunk and calls `Add` for each new
   submesh.

`DoRedraw` uses a `stackalloc int[meshCount]` span for the new IDs rather than a heap
allocation, then calls `.ToArray()` only once to persist them into `rendered.Ids`.

---

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

## Relationship to ChunkTessellationDispatcher

These two classes are complementary and intentionally decoupled:

| Concern | ChunkTessellationDispatcher | MeshBatcher |
|---|---|---|
| Runs on | Worker thread | Render thread |
| Input | `TessellationChunkWorkItem` | `PendingUpload` / `PendingUnload` queues |
| Output | `StageChunk` / `StageUnload` calls | GPU draw calls |
| GPU calls? | None | `CreateModel`, `DeleteModel`, `DrawModels` |
| Knows about camera? | No | No (player pos accepted, not used yet) |
| Lifetime of data | Per-chunk rebuild | Stable between dirty marks |

`ChunkTessellationDispatcher` produces the raw geometry and owns the `ArrayPool` buffers
until it calls `StageChunk`. `MeshBatcher` then owns the GPU lifetime of that geometry,
returning pool buffers after `DoRedraw` and freeing GPU memory on `Remove`.

---

## Performance Notes

| Aspect | Current behaviour | Notes |
|---|---|---|
| Texture switch cost | One `BindTexture2d` per texture per frame | Maximum 10 switches; near-optimal for this texture budget |
| `SortListsByTexture` cost | O(n) over `_modelsCount` every frame | Unavoidable without dirty tracking; acceptable for ≤16 K slots |
| Free-list reuse | LIFO via `Stack<int>` | Keeps `_modelsCount` compact; avoids O(n) scan growth over time |
| Per-slot allocation | Zero — `BatchEntry` is a struct embedded in `_models[]` | No GC pressure from model add/remove churn |
| Transparent sort | Not implemented | Can cause visual artifacts with overlapping transparent geometry |
| Upload cap | `maxUploadsPerFrame = 512` | Spreads GPU uploads across frames; prevents single-frame spikes on large redraws |

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
> **Dependencies:** `IOpenGlService`, `IMeshDrawer`, `IGameLogger`, `GeometryModel`, `BatchEntry`  
> **Thread safety:** `StageChunk` and `StageUnload` are safe to call from any thread via
> `ConcurrentQueue`. All other public methods (`Add`, `Remove`, `Draw`, `FlushPendingUploads`,
> `Clear`) must be called from the **render thread** only.