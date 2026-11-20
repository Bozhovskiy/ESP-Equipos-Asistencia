# Source guide

## Architecture

The program is a C# .NET Framework application with a WinForms overlay. `Abrir-ESP.exe` starts a local PowerShell launcher with elevation. The launcher checks the package, reads explicitly configured local prerequisites and starts LiveKD with the overlay as its debugger process. MemProcFS opens the LiveKD-backed file device and exposes process memory reads. The application does not use its memory-write APIs.

The managed project files are:

| File | Responsibility |
| --- | --- |
| `Abrir-ESP.cs` | Small executable launcher and `--verify` script-presence check. |
| `Probar-LiveKd.ps1` | Package checks, local prerequisite configuration, single-launch mutex and lifetime of the external acquisition process. |
| `Entidades-live.cs` | Native API bindings, process/module lookup, memory access and game-specific name decoding. |
| `ESP-LiveKd.cs` | Scene/camera discovery, timestamped frames, reader loop, overlay and radar layout. |
| `Proyeccion.cs` | Camera-basis projection, world-transform validation, radar and edge markers. |
| `Caja-Compartida.cs` | Shared player box geometry for drawing. |
| `Menu-ESP.cs` | Settings persistence and menu. |
| `Objetivos.cs` | Optional target profile, team/bone/visibility reading, validation and manual teammate display filtering. |
| `Compiler.ps1` / `Recompilar.ps1` | Installed compiler discovery and deterministic managed builds. |
| `pruebas` | Synthetic regression suites. |

No full game SDK is included. The existing base reader offsets and name-decoding constants are visible in the source. Their presence does not establish compatibility with a new patch. The optional target profile has a separate PE identity check, but the base reader is not protected by a complete executable-version fingerprint.

## Sampling and scene discovery

The reader searches the persistent level and loaded levels, deduplicates actor addresses and applies a global 8192-actor limit. It rechecks array headers before accepting their pointers. A changing or malformed level can be rejected independently of otherwise usable levels.

Local-controller discovery follows the local player chain through GameInstance. It revalidates ownership and array state before accepting the controller. A known but unreadable local chain does not silently select an unrelated named controller. A legacy fallback is retained when there is exactly one named controller and no authoritative local chain is available.

Invalid scenes trigger rediscovery at approximately 250 ms plus read time; valid scenes retain the slower discovery interval while positions and camera data are refreshed separately. Requested update frequency is not a measured guarantee of frame rate or read latency.

Memory reads already use flag 1 (`NOCACHE`) and reject incomplete byte counts. That is not a guarantee that address translation or the acquisition backend supplies data from one common instant. Frames currently read camera data before the actor loop. No scatter migration or synchronized engine snapshot was added in this release.

## Projection

The camera rotation builds forward/right/up axes from pitch, yaw and roll. Points behind the near-depth check are rejected rather than clamped in front of the camera. Horizontal and vertical FOV modes choose the corresponding focal-length calculation.

The current projection uses the full client rectangle. It does not fully model an aspect-constrained image rectangle, black bars, off-center projection, or every Unreal axis-constraint convention. `AspectRatio` and `DesiredFov` are available in diagnostics but do not automatically resolve those semantics. Changing the menu's axis option is a manual interpretation, not a proven conversion of the game camera field.

## Teams and display filters

Confirmed allies and manually marked teammates can be hidden from the overlay with the display filters. Unknown team data stays unknown and is labelled `equipo ?` rather than guessed.

F6 requires one unambiguous player near the crosshair. Marks are session state, expire after five seconds of absence, and reset on world/local-pawn change. They are not a permanent classifier or automatic team detector.

Without validated bones, the box approximates a standing character. A validated profile uses the head and feet bones for the box and draws the skeleton posture instead. Unknown metadata never becomes a positive or negative classification.

## Optional profile contract

`perfil-objetivos.json` loads at startup. `Enabled=true` alone is not enough: its PE timestamp and image size must match and at least one complete capability must be configured. Restart after changing the profile.

| Fields | Expected semantics |
| --- | --- |
| `PeTimestamp`, `ImageSize` | x64 PE TimeDateStamp and SizeOfImage. Basic identity check, not proof of all offsets. |
| `PawnStateOffset`, `TeamIdOffset` | Pawn-to-state pointer and `int32` team field. Valid values are 0–65535 and are compared with the local pawn. Their actual meaning must be verified. |
| `MeshOffset` | Pawn-to-skeletal-mesh pointer. |
| `BoneArrayOffset` | Component-space array of 96-byte double FTransforms, at most 512 bones. |
| `ComponentTransformOffset` | Mesh-to-world double FTransform. |
| Five bone indices | Distinct, verified indices for head, chest, pelvis and both feet. |

Bone translations are composed with the component's scale, quaternion and translation. The reader rejects partial data, invalid pointers, changed headers, implausible scales, invalid quaternions, nonfinite positions and incoherent poses. Local-to-parent arrays and float transforms do not satisfy this profile contract. A plausible pose does not prove that a given index is actually the named bone.

Teams and bones are separate capabilities; neither requires enabling an incomplete layout for the other. Missing data does not become a verified negative or positive result.

## Visibility

The visibility contract requires `VisibilitySource="CameraRaycast"`, a local observer pointer, world time and raycast time using the same double-seconds clock, and separate head/chest bytes: 1 means clear, 0 means blocked, other values mean unknown. Observer and timestamp are checked again during sampling; the observation age limit is 80 ms and future results are rejected.

These fields must actually exist with those meanings in a compatible test implementation. Render timestamps or flags are not substituted for raycast results. The disabled distributed profile does not expose working visibility offsets.

The visibility label describes a camera-to-bone raycast only. Life state, downed state, ammunition, projectile trajectory and weapon muzzle obstruction are not validated, and a camera ray is not proof of a clear weapon path.

## Local outputs and release boundaries

The application writes its settings and diagnostic reports locally. There is no updater, login, advertising module, external file-download routine, or telemetry endpoint in the project code. The release launcher supplies a local-only symbol path and the reader disables its symbol server. No runtime traffic capture was performed, so this is not certification of every behavior inside third-party tools.

Native loading and acquisition are separate: export verification is safe to run without a game, while starting a real acquisition session uses the configured external tool and local permissions. The recorded test results do not establish public-game functionality, detection status or stability across Windows/game updates.
