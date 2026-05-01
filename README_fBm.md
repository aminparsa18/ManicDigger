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

## Why fBm is Expensive

Every octave calls the underlying noise function once. With 7 octaves and a 256×256 map:

```
256 × 256 × 7 octaves × 3 noise calls (ridged) ≈ 1.4 million noise evaluations per GetChunk pass
```

And each single noise evaluation — one call to `GradientCoherentNoise` — has to visit **eight corners** of a tiny 3D cube surrounding the sample point, look up a gradient at each corner, and blend all eight results together. That's the trilinear interpolation at the heart of gradient noise.

So a single chunk rebuild is really more like:

```
1.4 million evaluations × 8 corners each = ~11 million corner lookups per chunk
```

This is the bottleneck. Everything in the performance engineering section below targets it.

---

## The Performance Engineering

This section explains the optimisations made to `GradientNoiseBasis`, `FastNoiseBasis`, `FastNoise`, and `FastBillow` in plain terms. The goal isn't to make you an expert — it's to give you enough mental model to understand *why* the code looks the way it does.

---

### Background: How a CPU Core Works (Very Briefly)

A modern CPU core can do more than one thing at a time. It has multiple execution units — one for additions, one for multiplications, one for memory loads — and it can run several of them simultaneously on *independent* pieces of work.

The key word is **independent**. If calculation B depends on the result of calculation A, the CPU must finish A before it can start B. That's a **serial dependency**, and it forces the CPU to sit idle waiting. Removing unnecessary serial dependencies is the single most effective performance technique in all of the optimisations below.

> 📄 **Further reading:** [What Every Programmer Should Know About Memory](https://www.akkadia.org/drepper/cpumemory.pdf) — Ulrich Drepper's classic paper on CPU caches and memory access patterns. Long but worth skimming sections 1–3.

---

### Optimisation 1: Think in `float`, Not `double`

The original LibNoise code used 64-bit `double` arithmetic everywhere. Every gradient value, every noise output, every intermediate calculation was a 64-bit number.

The noise output is used as a terrain height, which is eventually rounded to an integer block position. 64-bit precision is completely wasted here — 32-bit `float` gives about 7 decimal digits of precision, which is millions of times more precise than the distance between two Minecraft blocks.

Switching to `float`:

- **Halves memory bandwidth** on gradient table lookups — the same cache line holds twice as many values
- **Enables SIMD** — the CPU can process twice as many floats per SIMD instruction (explained next)
- **Eliminates silent widenings** — when `float` and `double` values are mixed in an expression, C# quietly promotes everything to `double`, does the work, then demotes back to `float`. These conversions cost time and prevent SIMD.

**Result:** LibNoise dropped from ~42% of total CPU time to not appearing in the profiler.

---

### Optimisation 2: SIMD — Eight Corners at Once

#### The problem: one corner at a time

When gradient noise evaluates a point, it finds the 8 corners of the unit cube surrounding that point (imagine a tiny box in 3D space), looks up the gradient at each corner, and blends the results. A naive implementation visits each corner in sequence:

```
Evaluate corner (x0,y0,z0) → hash → gradient → dot product
Evaluate corner (x1,y0,z0) → hash → gradient → dot product
Evaluate corner (x0,y1,z0) → hash → gradient → dot product
...  (5 more)
Blend all 8 results together
```

Each step is independent of the others — corner (x1,y0,z0) doesn't need the result from corner (x0,y0,z0) to compute its own hash. Yet the scalar code processes them one by one, like having 8 items to weigh on a scale that only holds one at a time.

#### The solution: SIMD

**SIMD** stands for *Single Instruction, Multiple Data*. It means giving the CPU one instruction that operates on multiple values simultaneously instead of one.

Modern CPUs have special 256-bit **AVX2** registers that hold eight 32-bit floats at once:

```
Normal (scalar) register:   [ float ]
AVX2 (SIMD) register:       [ f0 | f1 | f2 | f3 | f4 | f5 | f6 | f7 ]
```

One AVX2 multiply instruction multiplies **all eight floats at the same time** — same time, same energy, same instruction slot. It's like upgrading from a one-lane road to an eight-lane highway.

Here's the before and after for the 8-corner evaluation:

```
BEFORE — scalar, 8 corners in sequence:

  corner 0: hash → look up gradient (gx,gy,gz) → gx*dx + gy*dy + gz*dz  ──┐
  corner 1: hash → look up gradient (gx,gy,gz) → gx*dx + gy*dy + gz*dz  ──┤
  corner 2: hash → look up gradient (gx,gy,gz) → gx*dx + gy*dy + gz*dz  ──┤
  corner 3: hash → look up gradient (gx,gy,gz) → gx*dx + gy*dy + gz*dz  ──┤ blend
  corner 4: hash → look up gradient (gx,gy,gz) → gx*dx + gy*dy + gz*dz  ──┤
  corner 5: hash → look up gradient (gx,gy,gz) → gx*dx + gy*dy + gz*dz  ──┤
  corner 6: hash → look up gradient (gx,gy,gz) → gx*dx + gy*dy + gz*dz  ──┤
  corner 7: hash → look up gradient (gx,gy,gz) → gx*dx + gy*dy + gz*dz  ──┘

AFTER — AVX2, all 8 corners simultaneously:

  [ c0 | c1 | c2 | c3 | c4 | c5 | c6 | c7 ]  ← 8 hashes, one SIMD pass
       ↓
  [ gx0| gx1| gx2| gx3| gx4| gx5| gx6| gx7]  ← 8 gradient X values, one gather
  [ gy0| gy1| gy2| gy3| gy4| gy5| gy6| gy7]  ← 8 gradient Y values, one gather
  [ gz0| gz1| gz2| gz3| gz4| gz5| gz6| gz7]  ← 8 gradient Z values, one gather
       ↓
  [ d0 | d1 | d2 | d3 | d4 | d5 | d6 | d7 ]  ← 8 dot products, one FMA chain
       ↓
       blend (trilinear lerp) → single result
```

The hashes, gradient lookups, and dot products that used to take 8 sequential passes now take 1 pass each. This is the main reason `GradientCoherentNoise` is fast.

> 🎥 **Further watching:** [SIMD explained visually — SimonDev](https://www.youtube.com/watch?v=x6pGlMtLn2A) — short, clear video showing scalar vs SIMD with animations.  
> 📄 **Further reading:** [Intel Intrinsics Guide](https://www.intel.com/content/www/us/en/docs/intrinsics-guide/) — the reference for every AVX2 instruction with latency/throughput data. Useful when you want to understand what a specific intrinsic actually costs.

---

### Optimisation 3: Data Layout — Putting Things Where the CPU Can Find Them

#### The problem: interleaved gradient data

The gradient table stores 256 pre-computed 3D vectors — one per possible hash value. The original layout stored each vector's X, Y, Z, and a padding W together:

```
AoS (Array of Structs) — original layout:
index:  0           1           2           3   ...
memory: [X0 Y0 Z0 W0 X1 Y1 Z1 W1 X2 Y2 Z2 W2 X3 Y3 Z3 W3 ...]
```

To load the X component of 8 different gradients (for the 8 corners), the CPU has to fetch values at positions 0, 4, 8, 12, 16, 20, 24, 28. They're scattered — every lookup is 4 floats apart.

#### The solution: SoA (Structure of Arrays)

Instead of one array of structs, use three flat arrays — one for all X components, one for all Y, one for all Z:

```
SoA (Structure of Arrays) — new layout:
GradX: [X0 X1 X2 X3 X4 X5 X6 X7 X8 ... X255]
GradY: [Y0 Y1 Y2 Y3 Y4 Y5 Y6 Y7 Y8 ... Y255]
GradZ: [Z0 Z1 Z2 Z3 Z4 Z5 Z6 Z7 Z8 ... Z255]
```

Now all 8 X components for 8 different corners can be loaded in a single **gather** operation: tell the CPU "load from GradX at indices [3, 17, 42, 91, ...]" and it fetches all 8 simultaneously. Same for Y and Z.

The analogy: imagine a library where each book about a country contains that country's population, area, and GDP all mixed together (AoS). To compare populations across 8 countries you'd pull 8 books. With SoA, there's a separate "population" reference book — open one book, get all 8 populations on the same page.

The SoA arrays are also pre-scaled by the constant factor `2.12f` that was applied at the end of every dot product. That pre-baking removes one multiply instruction from the inner loop of every single corner evaluation.

> 📄 **Further reading:** [Data-Oriented Design — Richard Fabian](https://www.dataorienteddesign.com/dodbook/) — free online book. Chapter 2 covers AoS vs SoA with concrete cache analysis.

---

### Optimisation 4: The 32-Bit Hash

Each corner's gradient is chosen by hashing its grid coordinates. The original hash used 64-bit `long` arithmetic:

```csharp
// Original
long hx = 1619L * x;
long hy = 31337L * y;
long hz = 6971L * z;
long hs = 1013L * seed;
long combined = hx + hy + hz + hs;
long h = combined & 0xFFFF_FFFFL;  // keep only the low 32 bits
h ^= h >> 8;
int index = (int)(h & 0xFF);       // keep only the low 8 bits
```

Notice that `& 0xFFFF_FFFFL` immediately discards the upper 32 bits, and `& 0xFF` at the end keeps only 8 bits. The entire computation only ever cares about the low 8 bits of the result — using `long` the whole way gives the same answer as using `int` with wrapping arithmetic, just with twice the register width and 64-bit multiplies.

Switching to `unchecked int`:

```csharp
// Optimised — identical output for the bits we actually use
unchecked
{
    int hx = 1619  * x;
    int hy = 31337 * y;
    int hz = 6971  * z;
    int hs = 1013  * seed;
    int h  = hx + hy + hz + hs;
    h ^= h >> 8;
    int index = h & 0xFF;
}
```

This matters for the SIMD path too — AVX2 integer operations work on 32-bit lanes. Using 32-bit hashes lets us compute all 8 corner hashes in a single 256-bit AVX2 register (8 × 32 bits = 256 bits exactly). 64-bit hashes would only fit 4 per register, requiring two passes.

---

### Optimisation 5: Breaking the Serial Chain in the fBm Loop

#### The problem: each octave depends on the previous

The fBm loop in `FastNoise` and `FastBillow` used to look like this:

```csharp
x *= Frequency;  // scale once before the loop
y *= Frequency;
z *= Frequency;

for (int i = 0; i < octaveCount; i++)
{
    sum += GradientCoherentNoise(x, y, z, ...) * amplitude;
    x         *= lacunarity;   // ← depends on x from iteration i
    y         *= lacunarity;   // ← depends on y from iteration i
    z         *= lacunarity;   // ← depends on z from iteration i
    amplitude *= persistence;  // ← depends on amplitude from iteration i
}
```

Visualised as a dependency chain:

```
iter 0:  noise(x0, y0, z0) × amp0  →  sum
             ↓ multiply
iter 1:  noise(x1, y1, z1) × amp1  →  sum
             ↓ multiply
iter 2:  noise(x2, y2, z2) × amp2  →  sum
             ↓ multiply
iter 3:  noise(x3, y3, z3) × amp3  →  sum
         ...

Each iteration cannot begin until the previous one has finished updating x, y, z, and amplitude.
```

This is like a factory assembly line with a single worker who must finish one car completely before starting the next. The CPU has multiple execution units sitting idle waiting for each `*= lacunarity` to complete.

#### The solution: pre-computed tables

The values `x * lacunarity^i` and `persistence^i` are the same on every call to `GetValue` for a given set of parameters. They only change when `Lacunarity`, `Persistence`, `Frequency`, or `OctaveCount` changes — which normally happens once at startup.

So we pre-compute them at construction time:

```csharp
// Computed once when parameters change, not on every GetValue call:
_scales[0]     = Frequency              // Frequency × Lacunarity⁰
_scales[1]     = Frequency * Lacunarity // Frequency × Lacunarity¹
_scales[2]     = Frequency * Lacunarity²
...

_amplitudes[0] = 1f                     // Persistence⁰
_amplitudes[1] = Persistence            // Persistence¹
_amplitudes[2] = Persistence²
...
```

The new loop reads these tables instead of computing them:

```csharp
for (int i = 0; i < _octaveCount; i++)
{
    float s = _scales[i];
    sum += GradientCoherentNoise(x * s, y * s, z * s, ...) * _amplitudes[i];
}
```

Visualised:

```
iter 0:  noise(x × scale[0]) × amp[0]  →  sum  ─┐
iter 1:  noise(x × scale[1]) × amp[1]  →  sum  ─┤  all independent!
iter 2:  noise(x × scale[2]) × amp[2]  →  sum  ─┤  CPU can overlap them
iter 3:  noise(x × scale[3]) × amp[3]  →  sum  ─┘
```

No iteration reads a value written by a previous iteration. The CPU's out-of-order execution engine can start octave 2's AVX2 gathers while octave 1's lerp reduction is still finishing. For a typical 6-octave terrain noise call, this means 2–3 noise evaluations can be genuinely in-flight simultaneously.

The `_tablesDirty` flag ensures we rebuild the tables when parameters change, but pay zero cost on steady-state `GetValue` calls:

```
parameter change  →  _tablesDirty = true
first GetValue    →  RebuildTables() (6 multiplies), then evaluate
every other call  →  _tablesDirty branch predicted-not-taken, free
```

> 🎥 **Further watching:** [Performance-Aware Programming — Casey Muratori, Episode 1](https://www.youtube.com/watch?v=jKiQeEcMVsA) — walks through the exact problem of CPU pipeline stalls caused by serial dependencies, with profiler demonstrations.

---

### Putting It All Together

A single `GetValue` call with 6 octaves on a modern desktop CPU now executes roughly like this:

```
GetValue(x, y, z):
│
├─ check _tablesDirty  (branch predicted-not-taken → free)
│
└─ loop 6 iterations (each independent, CPU overlaps 2–3 at a time):
      │
      ├─ multiply x, y, z by _scales[i]     (3 scalar multiplies, pre-loaded from cache)
      │
      └─ GradientCoherentNoise():
            │
            ├─ compute smoothing weights sx, sy, sz
            │
            ├─ AVX2: 8 hashes in one SIMD pass         (8 × int32 add/shift/xor)
            │
            ├─ AVX2: load 8 gradient X values           (GatherVector256 from GradX[])
            ├─ AVX2: load 8 gradient Y values           (GatherVector256 from GradY[])
            ├─ AVX2: load 8 gradient Z values           (GatherVector256 from GradZ[])
            │        ↑ all three gathers issue together; latency hidden by CPU
            │
            ├─ AVX2: 8 dot products in 3 instructions  (FMA chain)
            │
            └─ SSE:  trilinear lerp in 2 instructions  (Shuffle + FMA × 2)
                     → single float result
```

> 📄 **Further reading:** [Agner Fog — Optimising C++ Software](https://www.agner.org/optimize/optimizing_cpp.pdf) — free PDF, chapters 7–9 cover instruction-level parallelism, pipelines, and SIMD with concrete x86 examples. The most practically useful low-level optimisation reference available.

---

## Further Reading & Watching

### Noise and Terrain
- 🎥 [**The Art of Code — Fractal Brownian Motion**](https://www.youtube.com/watch?v=BFld4EBO2RE)
  Excellent visual breakdown with live shader demos
- 🎥 [**Sebastian Lague — Procedural Terrain Generation**](https://www.youtube.com/watch?v=wbpMiKiSKm8&list=PLFt_AvWsXl0eBW2EiBtl_sxmDtSgZBxB3)
  Full Unity series covering octaves, biomes, and erosion directly
- 🎥 [**Inigo Quilez — Painting a Landscape with Maths**](https://www.youtube.com/watch?v=BFld4EBO2RE)
  More advanced — shows how fBm layers combine visually in practice
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
- 🛠️ [**FastNoise Lite Previewer**](https://auburn.github.io/FastNoiseLite/)
  Real-time noise previewer — change octaves, lacunarity, persistence and see results instantly
- 🛠️ [**Book of Shaders — Noise Chapter**](https://thebookofshaders.com/13/)
  Interactive GLSL playground, great for building intuition around fBm layering

### Performance Engineering
- 🎥 [**Performance-Aware Programming — Casey Muratori**](https://www.youtube.com/watch?v=jKiQeEcMVsA)
  Hands-on series covering pipeline stalls, SIMD, and cache effects with a profiler open the whole time. Start here if you want to understand *why* the code is structured the way it is.
- 🎥 [**SIMD explained visually — SimonDev**](https://www.youtube.com/watch?v=x6pGlMtLn2A)
  Short and clear. Good first watch before diving into the intrinsics.
- 📄 [**Agner Fog — Optimising C++ Software**](https://www.agner.org/optimize/optimizing_cpp.pdf)
  Free PDF. Chapters 7–9 cover instruction-level parallelism, SIMD, and pipeline stalls. The most practically useful low-level reference available.
- 📄 [**What Every Programmer Should Know About Memory — Ulrich Drepper**](https://www.akkadia.org/drepper/cpumemory.pdf)
  Free PDF. Explains CPU caches in depth — why SoA beats AoS, why sequential access beats random access, and why the gather instructions in GradientNoiseBasis matter.
- 📄 [**Data-Oriented Design — Richard Fabian**](https://www.dataorienteddesign.com/dodbook/)
  Free online book. Chapter 2 is a clear treatment of AoS vs SoA with cache analysis.
- 🛠️ [**Intel Intrinsics Guide**](https://www.intel.com/content/www/us/en/docs/intrinsics-guide/)
  The reference for every AVX2 / SSE instruction with latency and throughput numbers. Use this to understand the cost of any specific intrinsic in the code.
- 🛠️ [**.NET SIMD Intrinsics — Microsoft Docs**](https://learn.microsoft.com/en-us/dotnet/api/system.runtime.intrinsics)
  The C# API surface for `Avx2`, `Fma`, `Sse`, and friends — what's used directly in `GradientNoiseBasis`.