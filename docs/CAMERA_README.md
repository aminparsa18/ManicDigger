# Camera System

> Manages first-person, third-person, overhead, and cinematic camera modes.  
> Geometry is computed by `CameraService`; the view matrix is built by `ModCamera`;  
> keyboard input is handled by `ModCameraKeys`; cinematic paths are recorded and
> played back by `ModAutoCamera`.

---

## Table of Contents

1. [Overview](#overview)
2. [The Four Camera Components](#the-four-camera-components)
3. [Architecture](#architecture)
4. [Camera Modes — Runtime](#camera-modes--runtime)
   - [First-Person (FPP)](#first-person-fpp)
   - [Third-Person (TPP)](#third-person-tpp)
   - [Overhead (RTS)](#overhead-rts)
5. [ModAutoCamera — Cinematic Mode](#modautocamera--cinematic-mode)
   - [Recording Waypoints](#recording-waypoints)
   - [Catmull-Rom Playback](#catmull-rom-playback)
   - [AVI Recording](#avi-recording)
   - [Save and Load](#save-and-load)
6. [Mouse and Touch Input](#mouse-and-touch-input)
7. [Wall Collision](#wall-collision)
8. [Data Flow — Runtime Cameras](#data-flow--runtime-cameras)
9. [Data Flow — Cinematic Camera](#data-flow--cinematic-camera)
10. [DI Registration](#di-registration)
11. [External References](#external-references)

---

## Overview

The camera system is split across four completely independent components with no shared state between them. Each owns a single concern:

| Component | Concern |
|---|---|
| `CameraService` | Orbit geometry math (azimuth, elevation, distance) |
| `ModCamera` | View matrix construction and wall collision |
| `ModCameraKeys` | Keyboard and touch input → camera and movement state |
| `ModAutoCamera` | Waypoint recording, Catmull-Rom playback, AVI capture |

`ModAutoCamera` is a **director mode** — it temporarily hijacks the camera and player position during playback, then restores everything when done. The other three components are the **runtime camera** and remain active at all times except during cinematic playback.

---

## The Four Camera Components

### `CameraService` / `ICameraService`

Pure math. No game loop, no input, no rendering. Holds:

- `Center` — world-space orbit target (set to player position each tick)
- `_azimuth` — horizontal rotation (radians)
- `_angle` — elevation 0°–89° (clamped, never flips)
- `_distance` / `OverHeadCameraDistance` — orbit radius

Exposes `GetPosition(ref Vector3)` which computes the eye position from those three values. Only used for the overhead camera mode.

---

### `ModCamera`

Runs in `OnBeforeNewFrameDraw3d` — once per rendered frame, before any GL draw call. Builds `game.Camera` (`Matrix4`) for whichever mode is active. Also writes `game.CameraEye` so other systems (audio listener, raycasting) know where the camera is.

---

### `ModCameraKeys`

Runs in `OnNewFrameFixed` — once per physics tick. Translates raw keyboard state into camera and movement deltas. Never touches the view matrix directly.

---

### `ModAutoCamera`

Runs in `OnNewFrame` — once per rendered frame. Completely independent of `CameraService`, `ModCamera`, and `ModCameraKeys`. During playback it writes directly to `game.LocalPositionX/Y/Z` and `game.LocalOrientationX/Y/Z`, bypassing the normal camera pipeline entirely.

---

## Architecture

```
Input
  ├── Keyboard/touch   →  ModCameraKeys.OnNewFrameFixed()
  │       ├── FPP/TPP: writes game.Controls movement deltas
  │       └── Overhead: writes azimuth + angle to CameraService
  │
  ├── Mouse            →  Game.UpdateMouseViewportControl()
  │       ├── FPP/TPP: writes Player.position.rotx / roty
  │       └── Overhead: calls CameraService.TurnLeft / TurnUp
  │
  └── Chat command     →  ModAutoCamera.OnClientCommand(".cam ...")
          ├── ".cam p"      → record waypoint
          ├── ".cam start"  → begin spline playback
          ├── ".cam rec"    → begin playback + AVI capture
          └── ".cam stop"   → restore player state

Per-frame (runtime camera)
  └── ModCamera.OnBeforeNewFrameDraw3d()
          ├── FPP/TPP  →  Player.position.rot* → Matrix4.LookAt()
          └── Overhead →  CameraService.GetPosition() → Matrix4.LookAt()

Per-frame (cinematic camera — overrides runtime camera)
  └── ModAutoCamera.OnNewFrame()
          ├── advances _playingTime
          ├── finds current spline segment
          ├── evaluates CatmullRom() → writes game.LocalPosition* + LocalOrientation*
          └── (optional) captures screenshot → AVI frame

game.Camera = Matrix4   →   consumed by the GL render pipeline
```

---

## Camera Modes — Runtime

### First-Person (FPP)

```
eye    = (player.x,  player.y + eyeHeight,  player.z)
target = eye + forward(rotx, roty)
```

`forward` is computed by `VectorUtils.ToVectorInFixedSystem` from pitch (`rotx`) and yaw (`roty`). The player sees exactly what they would see standing at their position looking in their current direction.

### Third-Person (TPP)

```
eye    = player_eye − forward × TppCameraDistance
target = player_eye
```

The camera floats behind and above the player. `TppCameraDistance` is reduced by wall-collision to prevent clipping through terrain.

### Overhead (RTS)

```
CameraService.Center = player position   (set each tick in ModCameraKeys)
eye    = CameraService.GetPosition()     (orbit math below)
target = Center + (0, eyeHeight, 0)
```

`CameraService` computes the eye position from three orbit parameters:

```
eyeX = cos(azimuth) × cos(angle°) × distance + Center.X
eyeY = sin(angle°)  × distance               + Center.Y
eyeZ = sin(azimuth) × cos(angle°) × distance + Center.Z
```

```
            eye
           /
          /  distance
         /  ↗
        / ↗ angle (elevation)
Center ·──────────────── horizon
        ↑
       azimuth rotates this whole plane
```

Elevation is clamped to [0°, 89°] — the camera can never flip upside-down.
Mouse right/middle button drag adjusts azimuth and elevation directly on `CameraService`.
A/D keys rotate azimuth; W/S keys tilt elevation via `CameraMoveArgs`.

---

## ModAutoCamera — Cinematic Mode

`ModAutoCamera` is a self-contained cinematic tool activated entirely through chat commands. It has no connection to `CameraService`, `ModCamera`, or `ModCameraKeys` — it writes player position and orientation directly and lets `ModCamera` read them as if the player had moved there normally.

### Recording Waypoints

```
.cam p   →   AddPoint()
```

Captures `game.LocalPosition*` and `game.LocalOrientation*` into a fixed `CameraPoint[256]` array. `CameraPoint` is a struct so the array is a single contiguous allocation with no per-waypoint heap objects.

```
_cameraPoints[0]  (x, y, z, rx, ry, rz)
_cameraPoints[1]  (x, y, z, rx, ry, rz)
...
_cameraPoints[N]
```

### Catmull-Rom Playback

```
.cam start [seconds]
```

Catmull-Rom is a spline interpolation algorithm that produces smooth, curved paths through a sequence of control points. Unlike linear interpolation (straight lines between waypoints), Catmull-Rom uses the two neighbouring points on each side of a segment to compute a tangent, giving natural curved motion without requiring the user to specify tangent vectors.

```
Linear interpolation:          Catmull-Rom interpolation:

p0 ──── p1 ──── p2 ──── p3    p0 ···╮ p1 ╭···╮ p2 ╭··· p3
        (sharp corners)              (smooth curves through all points)
```

The formula for one scalar coordinate:

```
CatmullRom(t, p0, p1, p2, p3) =
  0.5 × (  2×p1
          + (-p0 + p2) × t
          + (2×p0 - 5×p1 + 4×p2 - p3) × t²
          + (-p0 + 3×p1 - 3×p2 + p3)  × t³  )
```

where `p1` and `p2` are the segment endpoints and `p0`, `p3` are the neighbouring control points. At the ends of the path the nearest endpoint is duplicated as the missing neighbour.

**Segment lookup** uses precomputed cumulative distances built once at `StartPlayback`:

```
_segmentStartDists[0] = 0
_segmentStartDists[1] = dist(p0, p1)
_segmentStartDists[2] = dist(p0,p1) + dist(p1,p2)
...
```

Each frame, `playingDist = _playingTime × _playingSpeed` is compared against this table to find the active segment in O(n) — avoiding per-frame `sqrt` calls over the entire path.

**Speed** is derived from total path arc length divided by the requested playback duration:

```
_playingSpeed = TotalDistance() / totalTime   (world-units per second)
```

Before playback, the player's position, orientation, and freemove level are saved. On `Stop()` they are restored exactly.

### AVI Recording

```
.cam rec [real seconds] [video seconds]
```

Opens an AVI file via `IAviWriter` and captures screenshots at 60 fps (real-time adjusted by `_recSpeed`):

```
_recSpeed     = realSeconds / videoSeconds
_frameInterval = (1 / 60) × _recSpeed

each frame:
  _writeAccum += dt
  if _writeAccum >= _frameInterval:
      _writeAccum -= _frameInterval
      GrabScreenshot() → AVI.AddFrame()
```

A `_recSpeed` of 2 means one second of real playback produces 0.5 s of video (2× fast-forward). The first frame after playback starts is always skipped because the screen has not been redrawn yet at that moment.

### Save and Load

```
.cam save   →   serialise to clipboard
.cam load   →   parse from clipboard string
```

Waypoints are serialised as a comma-separated string of integers:

```
1,x0,y0,z0,rx0,ry0,rz0,x1,y1,z1,...

Positions    × 100   (centimetres, avoids float precision loss)
Orientations × 1000  (milliradians)
```

On load, values are divided back: `/ 100f` for positions, `/ 1000f` for orientations.

---

## Mouse and Touch Input

Mouse smoothing blends the current delta with a rolling velocity before applying rotation:

```
vel = (vel + delta × scale) × 0.85
```

When smoothing is off, `vel = delta` directly. After consumption, `mouseDeltaX / Y` are zeroed so deltas do not accumulate across frames.

Touch orientation (`TouchOrientationDx / Dy`) is added to `Player.position.rot*` using a fixed scale and also zeroed after use.

---

## Wall Collision

`LimitThirdPersonCameraToWalls` — used for both TPP and overhead modes:

1. Casts a ray from the look-at target toward the desired eye position (extended 1 unit).
2. If any block is hit, distance is clamped to `max(0.3, hitDistance − 1)`.
3. Eye position is recomputed from the clamped distance.
4. Clamped distance is written back to `TppCameraDistance` or `CameraService.OverHeadCameraDistance`.

Minimum distance **0.3 units** prevents the camera collapsing into the target even in tight spaces.

---

## Data Flow — Runtime Cameras

```
── Fixed tick (ModCameraKeys) ───────────────────────────────────────────────
CameraService.Center    ← player position
CameraService.TurnLeft / TurnRight / Move(CameraMoveArgs)

── Every frame (Game.UpdateMouseViewportControl) ────────────────────────────
FPP/TPP:  Player.position.rotx / roty ← mouse delta (smoothed)
Overhead: CameraService.TurnLeft / TurnUp ← mouse delta

── Every frame before draw (ModCamera) ──────────────────────────────────────
Overhead:
  eye    ← CameraService.GetPosition()
  target ← CameraService.Center + eyeHeight
  eye, distance ← LimitThirdPersonCameraToWalls(...)
  game.Camera ← Matrix4.LookAt(eye, target, Up)

FPP / TPP:
  eye, target ← Player.position.rot*
  (TPP) eye, distance ← LimitThirdPersonCameraToWalls(...)
  game.Camera ← Matrix4.LookAt(eye, target, Up)

game.CameraEye ← eye
```

---

## Data Flow — Cinematic Camera

```
.cam start 30          (play path over 30 seconds)
    │
    ├── _playingSpeed = TotalDistance() / 30
    ├── precompute _segmentStartDists[]
    ├── save Player.position + freemove
    ├── game.FreemoveLevel = Noclip
    └── game.EnableCameraControl = false

── Every frame (ModAutoCamera.OnNewFrame) ───────────────────────────────────
_playingTime += dt
playingDist = _playingTime × _playingSpeed

find segment i where _segmentStartDists[i] ≤ playingDist < _segmentStartDists[i+1]
t = (playingDist - _segmentStartDists[i]) / segmentLength

game.LocalPosition*    ← CatmullRom(t, p[i-1], p[i], p[i+1], p[i+2])
game.LocalOrientation* ← CatmullRom(t, ...)

── ModCamera reads game.LocalPosition* as if player moved ───────────────────
game.Camera ← Matrix4.LookAt(eye, target, Up)   (FPP path, normal pipeline)

── On path end or .cam stop ─────────────────────────────────────────────────
restore Player.position + freemove
game.EnableCameraControl = true
game.EnableDraw2d = true
_avi?.Close()
```

---

## DI Registration

```csharp
services.AddSingleton<ICameraService, CameraService>();
services.AddSingleton<IModBase, ModCamera>();
services.AddSingleton<IModBase, ModCameraKeys>();
services.AddSingleton<IModBase, ModAutoCamera>();
```

`ModCamera`, `ModCameraKeys`, and `ModAutoCamera` are added to `game.ClientMods` at startup via `IModRegistry` so their frame hooks are called automatically. `ICameraService` is injected into `ModCamera` (reads position) and `ModCameraKeys` (writes angles). `ModAutoCamera` bypasses `CameraService` entirely and writes to `IGame` directly.

---

## External References

| Topic | Link |
|---|---|
| Catmull-Rom spline interpolation | [Wikipedia — Centripetal Catmull-Rom spline](https://en.wikipedia.org/wiki/Centripetal_Catmull%E2%80%93Rom_spline) |
| Catmull-Rom for game cameras | [Gamasutra — Curved Paths](https://www.gamasutra.com/view/news/228602/Curved_paths_in_games_Catmull_Rom_splines.php) |
| Spline interpolation overview | [iquilezles.org — Splines](https://iquilezles.org/articles/minispline/) |
| Arc-length parameterisation of splines | [Gamedev.net — Arc Length Parameterisation](https://www.gamedev.net/articles/programming/math-and-physics/fast-and-simple-arclen-parameterization-r4230/) |
| Orbit camera math | [Tutsplus — Orbit Camera](https://gamedevelopment.tutsplus.com/tutorials/understanding-the-orbit-camera--cms-22879) |
| View matrix and LookAt | [LearnOpenGL — Camera](https://learnopengl.com/Getting-started/Camera) |
| Orthographic vs perspective projection | [LearnOpenGL — Coordinate Systems](https://learnopengl.com/Getting-started/Coordinate-Systems) |
| OpenGL matrix transform math | [Songho — OpenGL Transformations](http://www.songho.ca/opengl/gl_transform.html) |
| Frustum culling (sphere test) | [LearnOpenGL — Frustum Culling](https://learnopengl.com/Guest-Articles/2021/Scene/Frustum-Culling) |
| AVI file format | [Wikipedia — AVI](https://en.wikipedia.org/wiki/Audio_Video_Interleave) |