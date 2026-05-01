# Camera System

> Manages first-person, third-person, and overhead camera modes.  
> Geometry is computed by `CameraService`; the view matrix is built by `ModCamera`;  
> input is handled by `ModCameraKeys` and `Game.UpdateMouseViewportControl`.

---

## Table of Contents

1. [Overview](#overview)
2. [Architecture](#architecture)
3. [Class Responsibilities](#class-responsibilities)
4. [Camera Modes](#camera-modes)
5. [Data Flow](#data-flow)
6. [Mouse and Touch Input](#mouse-and-touch-input)
7. [Wall Collision](#wall-collision)
8. [DI Registration](#di-registration)

---

## Overview

Three separate concerns make up the camera system:

| Concern | Owner |
|---|---|
| Orbit geometry (azimuth, elevation, distance) | `CameraService` |
| View matrix construction and wall collision | `ModCamera` |
| Keyboard input | `ModCameraKeys` |
| Mouse / touch input | `Game.UpdateMouseViewportControl` |

No single class does all of these. `Game` itself only stores the resulting `Matrix4` in `game.Camera` — it has no camera logic of its own beyond mouse delta accumulation.

---

## Architecture

```
Input
  ├── Mouse / touch  →  Game.UpdateMouseViewportControl()
  │       ├── FPP/TPP: writes Player.position.rotx / roty directly
  │       └── Overhead: calls cameraService.TurnLeft / TurnUp
  │
  └── Keyboard  →  ModCameraKeys.OnNewFrameFixed()
          ├── FPP/TPP: writes game.Controls.MovedX / MovedY
          └── Overhead: writes azimuth + angle into cameraService
                        sets cameraService.Center to player position

Per-frame draw
  └── ModCamera.OnBeforeNewFrameDraw3d()
          ├── Overhead  →  cameraService.GetPosition()  →  Matrix4.LookAt()
          └── FPP / TPP →  Player.position.rot*         →  Matrix4.LookAt()

game.Camera = Matrix4   →   consumed by the GL render pipeline
```

---

## Class Responsibilities

### `ICameraService` / `CameraService`

Pure orbit-geometry calculator. Holds:

- `Center` — the world-space point being orbited (set to player position each tick)
- `_azimuth` — horizontal rotation (radians)
- `_angle` — elevation above horizontal (degrees, clamped 0°–89°)
- `_distance` / `OverHeadCameraDistance` — orbit radius

Exposes `GetPosition(ref Vector3)` which computes the eye position from those three values:

```
eyeX = cos(azimuth) × cos(angle) × distance + Center.X
eyeY = sin(angle)   × distance              + Center.Y
eyeZ = sin(azimuth) × cos(angle) × distance + Center.Z
```

Has no knowledge of the game loop, input, or rendering.

---

### `ModCamera`

Runs in `OnBeforeNewFrameDraw3d` — once per rendered frame, before the GL draw call.
Builds `game.Camera` (`Matrix4`) from whichever mode is active.

- **Overhead** — asks `cameraService.GetPosition()` for the eye, sets the target to `cameraService.Center`, runs wall-collision adjustment, then calls `Matrix4.LookAt`.
- **First-person** — eye = player eye position, target = eye + forward vector.
- **Third-person** — eye = player eye position − forward × distance, target = player eye; distance is also wall-collision adjusted.

Also writes `game.CameraEye` so other systems (e.g. audio listener, picking) know where the camera is.

---

### `ModCameraKeys`

Runs in `OnNewFrameFixed` — once per physics tick.

- **FPP / TPP** — WASD writes to `game.Controls.MovedX / MovedY`; Space / LeftShift set jump and crouch flags; touch deltas are added to movement.
- **Overhead** — A/D call `cameraService.TurnRight / TurnLeft`; W/S set `AngleUp / AngleDown` in a `CameraMoveArgs`; player-destination click-to-move steers `Player.position.roty` toward the target.
- **Auto-jump** — sets `WantsJump` when the player walks into a one-block-high wall or a half-block, respecting the `AutoJumpEnabled` setting.

---

### `Game.UpdateMouseViewportControl`

Called every frame to consume accumulated mouse deltas (`mouseDeltaX / Y`).

- **FPP / TPP** (pointer locked) — delta → `Player.position.roty / rotx`; `rotx` is clamped to prevent flipping.
- **Touch** (FPP / TPP) — `TouchOrientationDx / Dy` → same rotation fields; zeroed after consumption.
- **Overhead** (middle or right button held) — delta X → `cameraService.TurnLeft`; delta Y → `cameraService.TurnUp`.

Optional mouse smoothing (`mouseSmoothing`) runs an exponential moving average on the deltas before they are applied.

---

## Camera Modes

### First-Person (FPP)

```
eye    = (player.x,  player.y + eyeHeight,  player.z)
target = eye + forward(rotx, roty)
```

The forward vector is computed by `VectorUtils.ToVectorInFixedSystem` from the player's pitch (`rotx`) and yaw (`roty`).

### Third-Person (TPP)

```
eye    = player_eye − forward × TppCameraDistance
target = player_eye
```

`TppCameraDistance` is reduced by wall-collision to prevent the camera clipping into terrain.

### Overhead (RTS)

```
cameraService.Center = player position   (set each tick in ModCameraKeys)
eye    = cameraService.GetPosition()     (orbit math)
target = Center + (0, eyeHeight, 0)
```

`OverHeadCameraDistance` is reduced by wall-collision and written back to `cameraService.OverHeadCameraDistance`.

---

## Data Flow

```
── Fixed tick (ModCameraKeys) ───────────────────────────────────────────────
cameraService.Center    ← player position
cameraService.TurnLeft / TurnRight / Move(CameraMoveArgs)   ← A / D / W / S

── Every frame (Game.UpdateMouseViewportControl) ────────────────────────────
FPP/TPP:  Player.position.rotx / roty  ← mouse delta (smoothed)
Overhead: cameraService.TurnLeft / TurnUp  ← mouse delta (raw / 70, / 3)

── Every frame before draw (ModCamera) ──────────────────────────────────────
Overhead:
  eye    ← cameraService.GetPosition()
  target ← cameraService.Center + eyeHeight
  eye, distance ← LimitThirdPersonCameraToWalls(...)
  game.Camera ← Matrix4.LookAt(eye, target, Up)

FPP / TPP:
  eye, target ← Player.position.rot*
  (TPP) eye, distance ← LimitThirdPersonCameraToWalls(...)
  game.Camera ← Matrix4.LookAt(eye, target, Up)

game.CameraEye ← eye   (written in all modes)
```

---

## Mouse and Touch Input

Mouse smoothing blends the current delta with a rolling velocity before applying rotation:

```
vel = (vel + delta × scale) × smoothing   // smoothing = 0.85, scale ∝ 0.8/4
```

When smoothing is off, `vel = delta` directly. After consumption, `mouseDeltaX / Y` are zeroed so deltas do not accumulate across frames.

Touch orientation (`TouchOrientationDx / Dy`) is added to `Player.position.rot*` using a fixed scale (`constRotationSpeed / 75`) and also zeroed after use.

---

## Wall Collision

`LimitThirdPersonCameraToWalls` (used for both TPP and overhead modes):

1. Casts a ray from the look-at target toward the desired eye position (extended by 1 unit).
2. If any block is hit, the camera distance is clamped to `max(0.3, hitDistance − 1)`.
3. The eye position is recomputed from the clamped distance.
4. The clamped distance is written back to `TppCameraDistance` or `cameraService.OverHeadCameraDistance` so subsequent frames start from the correct value.

The minimum distance of **0.3 units** prevents the camera from collapsing into the target point even in very tight spaces.

---

## DI Registration

```csharp
services.AddSingleton<ICameraService, CameraService>();
services.AddSingleton<ModCamera>();
services.AddSingleton<ModCameraKeys>();
```

`ModCamera` and `ModCameraKeys` are added to `game.ClientMods` at startup so their frame hooks are called automatically by the game loop. `ICameraService` is injected into both `ModCamera` (reads position) and `ModCameraKeys` (writes angles).
