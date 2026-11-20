# Source guide

## Architecture

The program is a C# .NET Framework application with a WinForms overlay. `Abrir-ESP.exe` starts a local PowerShell launcher with elevation. The launcher checks the package, reads explicitly configured local prerequisites and starts LiveKD with the overlay as its debugger process. MemProcFS opens the LiveKD-backed file device and exposes process memory reads. The application does not use its memory-write APIs.

The managed project files are:

| File | Responsibility |
| --- | --- |
| `Abrir-ESP.cs` | Small executable launcher and `--verify` script-presence check. |
| `Probar-LiveKd.ps1` | Package checks, local prerequisite configuration, single-launch mutex and lifetime of the external acquisition process. |
| `Entidades-live.cs` | Native API bindings, process/module lookup, memory access and game-specific name decoding. |
| `ESP-LiveKd.cs` | Scene/camera discovery, timestamped frames, reader loop, overlay, radar layout and input coordination. |
| `Proyeccion.cs` | Camera-basis projection, world-transform validation, radar and edge markers. |
| `Caja-Compartida.cs` | Shared player box geometry for drawing and target point estimation. |
| `Menu-ESP.cs` | Settings migration, persistence, menu and calibration controls. |
| `Objetivos.cs` | Optional target profile, team/bone/visibility reading, validation and manual teammate filtering. |
| `Asistencia.cs` | Target selection, calibrated movement, timing/input guards and autofire alignment state. |
| `Raton-Normal.cs` | Relative mouse input packets and guarded click/release handling. |
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

## Selection and movement

Candidates must pass geometry, range and optional metadata checks. Acquisition is limited to the configured circle. Once acquired, the controller retains that actor with a 20% radius margin. It does not automatically jump to another actor while the activation button remains held after losing the first one.

The priorities are screen error, world distance, or a weighted combination. Ties use distance and then actor address to avoid dependence on enumeration order. Actor addresses are not a complete generational identity; address reuse and broader session identity remain limitations.

The new movement conversion is:

```text
errorUnitsX = screenErrorX / PixelsPerUnitX
errorUnitsY = screenErrorY / PixelsPerUnitY
```

The existing response curve scales these input-space errors by an exponential gain anchored at a 20 ms interval. A vector limit caps relative-input units per second. Each axis carries a fractional remainder, clears an opposing remainder on a direction change, and limits an integer step to the floor of its remaining calibrated error. Errors below one relative input unit do not accumulate a crossing step.

Calibration values are independently persisted in `AssistPixelsPerUnitX` and `AssistPixelsPerUnitY`. Old profiles receive 1.00 for both; invalid settings are normalized before reaching controls. An invalid runtime option pauses the controller and requires release. The values are manual linear estimates. There is no response observer, automatic calibration or FOV/ADS compensation in this release.

One frame produces at most one correction. A repeated timestamp does not resend movement. Guards check focus, menu state, activation, frame validity, monotonic timing, frame age and client dimensions. Before input submission, the application rechecks relevant conditions and visibility freshness. Windows accepting `SendInput` is recorded separately from the unverified effect on the game.

## Teams and estimated points

Confirmed allies and manually marked teammates are excluded from targeting. Unknown team data stays unknown. Display suppression of allies is separate from targeting exclusion; turning their display back on does not make them targetable.

F6 requires one unambiguous player near the crosshair. Marks are session state, expire after five seconds of absence, and reset on world/local-pawn change. They are not a permanent classifier or automatic team detector.

Without validated bones, the box approximates a standing character. The controller accepts visible boxes from 1 pixel wide and 3 pixels high. Estimated Head uses the saved height percentage; estimated Chest uses 45% from the top. Small accepted boxes do not establish skeletal accuracy or enemy status.

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

## Visibility and autofire

The visibility contract requires `VisibilitySource="CameraRaycast"`, a local observer pointer, world time and raycast time using the same double-seconds clock, and separate head/chest bytes: 1 means clear, 0 means blocked, other values mean unknown. Observer and timestamp are checked again during sampling. The combined observation/input age limit is 80 ms; future results are rejected.

These fields must actually exist with those meanings in a compatible test implementation. Render timestamps or flags are not substituted for raycast results. The disabled distributed profile does not expose working visibility offsets.

Autofire requires a confirmed enemy, validated point and fresh visibility; it additionally checks RMB, focus, menu state and alignment across multiple samples for at least 60 ms. It will not fire while a movement correction is pending or reuse one frame as repeated confirmation. A rejected click blocks until release. Partial left-button insertion is tracked so the release is retried without issuing a new press.

Life state, downed state, ammunition, projectile trajectory and weapon muzzle obstruction are not validated. A camera-to-bone ray is not proof of a clear muzzle-to-target path.

## Local outputs and release boundaries

The application writes its settings and diagnostic reports locally. There is no updater, login, advertising module, external file-download routine, or telemetry endpoint in the project code. The release launcher supplies a local-only symbol path and the reader disables its symbol server. No runtime traffic capture was performed, so this is not certification of every behavior inside third-party tools.

Native loading and acquisition are separate: export verification is safe to run without a game, while starting a real acquisition session uses the configured external tool and local permissions. The recorded test results do not establish public-game functionality, detection status or stability across Windows/game updates.
