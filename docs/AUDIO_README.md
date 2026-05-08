# Audio System

> Manages sound loading, decoding, spatial playback, and lifecycle for all in-game sounds.  
> Built on **OpenAL** via OpenTK, integrated through a DI-registered `IAudioService`.

---

## Table of Contents

1. [Overview](#overview)
2. [Architecture](#architecture)
3. [Class Responsibilities](#class-responsibilities)
4. [Data Flow](#data-flow)
5. [Lifecycle of a Sound](#lifecycle-of-a-sound)
6. [Spatial Audio and the Listener](#spatial-audio-and-the-listener)
7. [Looping Sounds](#looping-sounds)
8. [Asset Decoding](#asset-decoding)
9. [Threading Model](#threading-model)
10. [DI Registration](#di-registration)
11. [Playing a Sound — How To](#playing-a-sound--how-to)

---

## Overview

The audio system is deliberately thin. Its only job is:

1. Decode an asset file (`.ogg` or `.wav`) into raw PCM once.
2. Hand a playback task to OpenAL on a thread-pool thread.
3. Update the task's world position every frame.
4. Clean it up when it finishes or is stopped.

`IAudioService` is the single audio entry point for the entire codebase. It owns the sound pool, the OpenAL device, and all playback state. `Game` holds no audio objects — it calls `_audioService` directly. All per-frame processing logic lives in `ModAudio`.

---

## Architecture

```
DI Container
    │
    └── IAudioService  (AudioService)
            │
            ├── Sound pool  (Sounds[], SoundsCount)   ← owned here, not on Game
            │
            ├── Game calls:                           ← one line per call site
            │     Add(sound)
            │     StopAll() / Clear()
            │     FindLoopingSound / StopLoopingSound  (via Sounds[])
            │
            └── ModAudio calls:
                  ProcessSounds(game)   → TryLoad / TryUpdatePosition /
                                          TryStop  / TryFinish
                  UpdateListener(...)   → OpenAL listener position
```

`AudioControl` no longer exists. `Game.Audio` no longer exists.

---

## Class Responsibilities

| Class | Responsibility |
|---|---|
| `IAudioService` | Single contract for pool management, playback control, decoding, and listener updates |
| `AudioService` | Implements `IAudioService`; owns the OpenAL device + context, sound pool, and asset decoding; lazily initialised |
| `AudioTask` | One thread-pool thread per sound; drives the OpenAL source state machine (play / pause / loop / stop) |
| `AudioData` | Plain data holder: raw PCM bytes, channel count, sample rate, bit depth |
| `ModAudio` | Per-frame pool processing and listener positioning; no audio state of its own |
| `Sound` | Lightweight descriptor: asset name, world position, loop flag, stop flag, and the live `AudioTask` handle |
| `OggDecoder` | Stateless helper; converts an Ogg Vorbis stream to `AudioData` using NVorbis |

---

## Data Flow

```
game.PlayAudioAt("hit.ogg", x, y, z)
    └── _audioService.Add(new Sound { Name = "hit.ogg", X, Y, Z })
                │
                ▼
        Sound pool  Sounds[i]
                │
                ▼  (next OnNewFrame via ModAudio)
        TryLoad
            └── CreateAudioData()
                    └── OggDecoder.OggToWav()  or  DecodeWave()
                            └── AudioData { Pcm, Channels, Rate, BitsPerSample }
            └── CreateAudio(data)  →  new AudioTask(_gameExit, data)
            └── Play(task)
                    └── ThreadPool.QueueUserWorkItem(RunAudio)
                            └── AL.GenSource / AL.BufferData / AL.SourcePlay
                                    └── poll loop until finished or stopped
```

---

## Lifecycle of a Sound

```
_audioService.Add(sound)
        │
        ▼  (next OnNewFrame)
TryLoad ── AudioData cached? decode once → CreateAudio + Play
        │
        ▼  (every OnNewFrame)
TryUpdatePosition ── SetPosition(x, y, z)
        │
        ▼  (every OnNewFrame)
TryStop ── sound.Stop == true? → DestroyAudio → Sounds[i] = null
        │
        ▼  (every OnNewFrame)
TryFinish ── IsFinished? → Sounds[i] = null   (one-shot)
                        → (loop handled inside AudioTask itself)
```

Each stage is guarded — a null `AudioTask` handle skips all work for that slot.

---

## Spatial Audio and the Listener

OpenAL models sound attenuation from a **listener** (the player's ears) to **sources** (individual sounds). Both must be updated every tick.

**Listener** — updated in `ModAudio.OnNewFrameFixed`:

```csharp
float orientationX =  MathF.Sin(game.Player.position.roty);
float orientationZ = -MathF.Cos(game.Player.position.roty);
_audioService.UpdateListener(
    game.EyesPosX, game.EyesPosY, game.EyesPosZ,
    orientationX, 0f, orientationZ);
```

**Source** — updated per sound in `TryUpdatePosition`:

```csharp
_audioService.SetPosition(sound.Task, sound.X, sound.Y, sound.Z);
```

The attenuation model used is `ALDistanceModel.InverseDistance` with:

| Parameter | Value | Effect |
|---|---|---|
| `RolloffFactor` | 0.3 | Gentle distance falloff |
| `ReferenceDistance` | 1 | Full volume within 1 unit |
| `MaxDistance` | 64 | Silent beyond 64 units |

`Game.FrameTick` contains no audio code.

---

## Looping Sounds

Looping is handled entirely inside `AudioTask`. Set `Sound.Loop = true` before adding to the pool — `ModAudio.TryLoad` propagates it to the task at creation time:

```csharp
AudioTask task = _audioService.CreateAudio(data);
task.Loop  = sound.Loop;
sound.Task = task;
_audioService.Play(task);
```

The task's internal thread pauses and resumes the OpenAL source based on the `_shouldPlay` flag, and rewinds when `Restart()` is called. A looping sound is only cleared from the pool when `sound.Stop = true`.

Finding and stopping a named looping sound from `Game`:

```csharp
// Find
for (int i = 0; i < _audioService.SoundsCount; i++)
    if (_audioService.Sounds[i]?.Name == file) return _audioService.Sounds[i];

// Stop
for (int i = 0; i < _audioService.SoundsCount; i++)
    if (_audioService.Sounds[i]?.Name == file)
        _audioService.Sounds[i].Stop = true;
```

---

## Asset Decoding

Both formats are decoded once and cached in `ModAudio._audioCache`. Subsequent plays of the same asset reuse the cached `AudioData`. `.wav` extensions are normalised to `.ogg` at the `Game` call sites before the name reaches the pool.

| Format | Decoder | Notes |
|---|---|---|
| `.wav` (RIFF/PCM) | `AudioService.DecodeWave` | Reads `fmt` + `data` chunks; handles extensible WAV (`fmtSize > 16`) |
| `.ogg` (Vorbis) | `OggDecoder.OggToWav` via NVorbis | Converts interleaved floats → 16-bit signed little-endian PCM |

All `.ogg` assets are preloaded on the first frame after `AssetsLoadProgress == 1` so the first playback request is always instant.

---

## Threading Model

| Thread | Does |
|---|---|
| Main / game thread | Calls `_audioService.Add`, `StopAll`, `Clear` |
| Mod/frame thread | `ModAudio.OnNewFrame` → `ProcessSounds` drives pool management |
| Fixed-tick thread | `ModAudio.OnNewFrameFixed` calls `UpdateListener` |
| Thread-pool thread (per task) | `AudioTask.RunAudio` owns all OpenAL source calls |

`AudioTask` uses `volatile` flags for cross-thread communication (`_shouldPlay`, `_stopRequested`, `_isFinished`). `Position` is guarded by a lock. No other shared mutable state exists between threads.

`AudioService` is lazily initialised with a double-checked lock — safe to construct before OpenAL is needed.

---

## DI Registration

```csharp
services.AddSingleton<IGameExit, GameExit>();
services.AddSingleton<IAudioService, AudioService>();
services.AddSingleton<ModAudio>();
```

`ModAudio` is added to `game.ClientMods` during startup so `OnNewFrame` and `OnNewFrameFixed` are called automatically by the game loop. No other wiring is required.

---

## Playing a Sound — How To

```csharp
// One-shot at a world position
_audioService.Add(new Sound { Name = "explosion.ogg", X = x, Y = y, Z = z });

// One-shot at the listener (player's ears)
_audioService.Add(new Sound { Name = "click.ogg", X = EyesPosX, Y = EyesPosY, Z = EyesPosZ });

// Looping sound
_audioService.Add(new Sound { Name = "wind.ogg", X = x, Y = y, Z = z, Loop = true });

// Stop a specific looping sound
for (int i = 0; i < _audioService.SoundsCount; i++)
    if (_audioService.Sounds[i]?.Name == "wind.ogg")
        _audioService.Sounds[i]!.Stop = true;

// Stop everything (e.g. on map unload)
_audioService.StopAll();
_audioService.Clear();
```

No other code is required. `ModAudio` handles everything from decoding through cleanup.