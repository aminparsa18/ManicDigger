# Fractal Brownian Motion (fBm) — World Generation Primer

## What is Noise?

Before understanding fBm, you need to understand what **noise** means in this context.

Imagine you ask a random number generator for a value at position `(x, y)`. Pure random output looks like TV static — every pixel is completely unrelated to its neighbour. That's useless for terrain because real landscapes are *smooth*: the height at one point is related to the height of nearby points.

**Gradient noise** (Perlin, Simplex, etc.) solves this. It produces smooth, continuous values where nearby points return similar results. Sample it across a 2D grid and you get gentle rolling hills. But it looks organic only at one scale — everything is the same "blobby" size.

---

## What is fBm?

**Fractal Brownian Motion** is the answer to the flatness problem. The idea is simple:

> *Stack multiple layers of noise on top of each other, each one twice as detailed and half as loud.*

Each layer is called an **octave**.

```
Octave 1 — Low frequency,  high amplitude  →  big continent shapes
Octave 2 — 2× frequency,  ½× amplitude    →  mountain ranges
Octave 3 — 4× frequency,  ¼× amplitude    →  hills
Octave 4 — 8× frequency,  ⅛× amplitude    →  bumps
Octave 5 — 16× frequency, 1/16× amplitude →  surface roughness
```

Add them all up and you get something that has *structure at every scale* — just like a real landscape does.

---

## The Three Knobs

Every fBm generator in this codebase has three key parameters:

### 1. Lacunarity
How much the frequency multiplies each octave. Almost always `2.0`.
- `2.0` = each octave is twice as detailed as the last
- Higher values = finer detail appears faster

### 2. Persistence
How much the amplitude multiplies each octave. Usually `0.5`.
- `0.5` = each octave is half as loud as the last
- Higher values (e.g. `0.7`) = rough, jagged terrain
- Lower values (e.g. `0.3`) = smooth, gentle terrain

### 3. Octave Count
How many layers to stack.
- More octaves = more fine detail, more CPU cost
- 4–6 is typical for terrain; 8+ for continent-scale features

```
Total cost scales linearly with octave count.
6 octaves = 6× the work of 1 octave.
```

---

## The Four fBm Variants in This Codebase

### `Perlin` / `FastNoise`
Plain fBm. Signal passes through unchanged.
```
output = Σ noise(x, y, z) × amplitude
```
→ Smooth, general-purpose. Used here for temperature, humidity, and detail layers.

### `Billow` / `FastBillow`
Absolute-value fold.
```
output = Σ (2 × |noise| - 1) × amplitude + 0.5
```
→ Rounded, puffy shapes. Used here for continent masks and lowland rolling hills.
Visualised: smooth noise with all valleys flipped upward into rounded bumps.

### `RidgedMultifractal`
Inverted fold + feedback loop between octaves.
```
signal  = (1 - |noise|)²
signal *= weight_from_previous_octave
```
→ Sharp ridges with smooth valleys. The feedback loop makes ridges compound across octaves — a high peak in one octave amplifies peaks in the next. Used here for mountain ranges.

---

## How This Codebase Uses fBm

The world generator stacks several independent fBm modules, each tuned for a specific job:

| Module | Class | Purpose | Notable Settings |
|---|---|---|---|
| `continentNoise` | `Billow` | Land vs ocean mask | 8 octaves, persistence 0.4 |
| `heightRidged` | `RidgedMultifractal` | Mountain ridges | 7 octaves, lacunarity 2.4 |
| `heightSmooth` | `Billow` | Lowland base | 5 octaves |
| `heightDetail` | `Perlin` | Fine surface detail + coastline warp | 2× frequency overlay |
| `tempNoise` | `Perlin` | Temperature zones | 3 octaves, wide scale |
| `humidityNoise` | `FastNoise` | Humidity zones | 4 octaves |
| `warpNoise` | `FastNoise` | Domain warp offsets | 3 octaves |
| `treeNoise` | `Billow` | Forest cluster density | Low frequency, 6 octaves |

---

## Domain Warping — The Biggest Visual Win

Plain fBm produces smooth blobs. The reason real coastlines look jagged and organic is that they've been physically deformed over millions of years. We fake that with **domain warping**: offsetting the sample coordinates using a second noise field *before* sampling the main noise.

```csharp
// Instead of:
float cont = continentNoise.GetValue(x, 0, y);

// We first warp the input coordinates:
float dx = warpNoise.GetValue(x / scale, y / scale, 0f);
float dy = warpNoise.GetValue(x / scale + 31.4f, y / scale + 47.2f, 0f);
float cont = continentNoise.GetValue(x + dx * strength, 0, y + dy * strength);
```

The two different offsets (`31.4` and `47.2`) ensure the X and Y warp are decorrelated — if you used the same offset you'd just shift coordinates diagonally rather than distort them. The result is that flat circular continent blobs become irregular landmasses with bays, peninsulas, and varied coastlines.

This also applies to the coastline itself — `heightDetail` is used as a secondary warp on continent coordinates, breaking the coast into natural inlets rather than smooth arcs.

---

## BiomeWeights — Blended Biomes Instead of Hard Assignments

The naive approach to biomes is a lookup table:

```
if normH > 0.6 → Mountains
if temp > 0.7 and humidity < 0.3 → Desert
...
```

This creates **hard biome boundaries** — you step from Plains to Desert in one block. The fix used here is **biome weight blending**:

```csharp
struct BiomeWeights { float Plains, Desert, Forest, Mountains, Snow; }
```

Instead of assigning one biome, every tile gets a weight for each biome type using smooth S-curves:

```csharp
w.Mountains = SmoothStep(0.5f, 0.8f, normH);    // fades in gradually with height
w.Desert    = (heat * 0.7f + dryness * 0.6f) * (1f - w.Mountains);
w.Forest    = wet * (1f - hot) * (1f - w.Mountains);
```

These weights then drive everything — surface block selection, terrain amplitude, and vegetation — so transitions between biomes are gradual. A tile at the edge of a desert blends through mixed material based on the weights rather than suddenly switching from sand to grass.

---

## Continuous Height — No More Cliff Edges

The other common mistake is a per-biome height lookup table:

```
Ocean  → baseH=14, amp=12
Shore  → baseH=27, amp=5   ← 13-block cliff!
Plains → baseH=34, amp=10  ← another cliff!
```

Adjacent biomes with different base heights produce vertical walls at every boundary regardless of how smooth the noise is. The fix is a **continuous ramp** driven purely by the continent value:

```csharp
private static float ContinentToBaseZ(float cont)
{
    if (cont < 0.18f) return Lerp(5f,  14f, cont / 0.18f);           // deep ocean  z= 5→14
    if (cont < 0.28f) return Lerp(14f, 22f, (cont - 0.18f) / 0.10f); // ocean       z=14→22
    if (cont < 0.36f) return Lerp(22f, 30f, (cont - 0.28f) / 0.08f); // shore slope z=22→30
    return Lerp(30f, 36f, Math.Clamp((cont - 0.36f) / 0.64f, 0f, 1f));// land base  z=30→36
}
```

Since water fills everything below z=30, the ocean basin forms naturally and the shore slopes out of it. Noise amplitude also fades to zero at the coastline:

```csharp
float inland = Math.Clamp((cont - 0.36f) / 0.64f, 0f, 1f);
float amp    = GetBlendedAmplitude(weights) * inland;
```

A tile right at the coast gets zero noise amplitude — always flat at water level. A tile deep inland gets full amplitude. No cliffs, no seams.

---

## Fake Erosion

Real terrain has flat valleys and sharp peaks because water erodes soft areas and leaves hard rock exposed. We approximate this with a post-process pass on the noise value:

```csharp
// S-curve compresses the middle, flattens valleys, sharpens peaks
float erosion = h * h * (3f - 2f * h);
h = float.Lerp(h, erosion, 0.5f);

// Power curve exaggerates peaks slightly
h = MathF.Pow(h, 1.15f);
```

The S-curve pushes values away from 0.5 toward 0 and 1 — low areas flatten out, high areas sharpen up. The power curve then lifts peaks slightly further. Together they produce the characteristic shape of eroded terrain without the expense of an actual erosion simulation.

---

## Why fBm is Expensive (and What We Did About It)

Every octave calls the underlying noise function once. With 7 octaves and a 256×256 map:

```
256 × 256 × 7 octaves × 3 noise calls (ridged) ≈ 1.4 million noise evaluations per GetChunk pass
```

### The `float` rewrite

The entire LibNoise stack was originally using `double` (64-bit) arithmetic. Switching every module to `float` (32-bit):

- Halves memory bandwidth on gradient table lookups
- Enables SIMD — the CPU can process more values per instruction cycle
- Eliminated silent `float → double → float` widenings scattered across the call chain

**Result:** LibNoise dropped from ~42% of total CPU to not appearing in the profiler at all.

### Other optimisations made

- Hash multiplications hoisted out of the 8-corner loop in `GradientNoiseBasis` — 32 multiplications → 8
- `ReadOnlySpan<float>` on gradient and permutation tables — removes per-access bounds checks in hot paths
- `[AggressiveInlining]` on `GradientCoherentNoise` — eliminates call frame overhead inside octave loops
- Field caching before octave loops in all fBm modules — prevents repeated `this`-pointer dereferences
- `MathF.Abs`, `MathF.Pow`, `MathF.Sqrt` replace `Math.*` equivalents — stays in float registers throughout
- Pre-computed `Dirs8` table in `TreeGenerator` — eliminates `Math.Cos/Sin` calls inside tree placement loops
- `FindSurface` scan in `TreeGenerator` — replaced random z guess (1/16 hit rate) with a guaranteed surface scan

---

## Further Reading & Watching

### Videos
- 🎥 [**The Art of Code — Fractal Brownian Motion**](https://www.youtube.com/watch?v=BFld4EBO2RE)
  Excellent visual breakdown with live shader demos
- 🎥 [**Sebastian Lague — Procedural Terrain Generation**](https://www.youtube.com/watch?v=wbpMiKiSKm8&list=PLFt_AvWsXl0eBW2EiBtl_sxmDtSgZBxB3)
  Full Unity series covering octaves, biomes, and erosion directly
- 🎥 [**Inigo Quilez — Painting a Landscape with Maths**](https://www.youtube.com/watch?v=BFld4EBO2RE)
  More advanced — shows how fBm layers combine visually in practice

### Articles
- 📄 [**Inigo Quilez — fBm**](https://iquilezles.org/articles/fbm/)
  The definitive written reference. Covers domain warping, derivatives, and all variants
- 📄 [**Inigo Quilez — Domain Warping**](https://iquilezles.org/articles/warp/)
  Specifically covers the warp technique used in `GetContinent` and `Warp()`
- 📄 [**Inigo Quilez — Ridged Noise**](https://iquilezles.org/articles/morenoise/)
  Explains the ridged and billow variants with live shader demos
- 📄 [**Red Blob Games — Noise + Elevation**](https://www.redblobgames.com/maps/terrain-from-noise/)
  Interactive, beginner-friendly. Covers the continent ramp concept directly
- 📄 [**Red Blob Games — Biome Blending**](https://www.redblobgames.com/articles/noise/introduction.html)
  Covers smooth biome transitions — the concept behind `BiomeWeights`

### Interactive Tools
- 🛠️ [**FastNoise Lite Previewer**](https://auburn.github.io/FastNoiseLite/)
  Real-time noise previewer — change octaves, lacunarity, persistence and see results instantly
- 🛠️ [**Book of Shaders — Noise Chapter**](https://thebookofshaders.com/13/)
  Interactive GLSL playground, great for building intuition around fBm layering