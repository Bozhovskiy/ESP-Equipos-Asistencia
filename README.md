# ARC Raiders ESP and Aim Assist — 1.0 Final

One-time experimental source and binary release. The archive contains the x64 overlay, its launcher, all project source files, reproducible build scripts, synthetic tests, and the native source material described below. This is a snapshot; there is no updater.

The reader targets `PioneerGame.exe` and contains game-specific offsets. It is not a universal Unreal Engine SDK. The release has not been verified against a current public game session or a specific server build. Treat the source as a starting point for a compatible local test build.

## What is included

- Player boxes, distance labels, radar and off-screen indicators.
- Adjustable appearance, range, refresh rate and horizontal/vertical FOV interpretation.
- Hold-right-mouse aim assistance with screen, distance or balanced selection.
- Independent X/Y movement calibration, response and speed limits.
- Estimated head/chest targeting when authoritative bone data is unavailable.
- Manual teammate exclusions and optional strict team, bone and visibility filters.
- A guarded autofire implementation that requires confirmed target data.
- Source for the overlay and launcher, tests, native patch and version-pinned native source archives.

Automatic team identification, validated bone targeting and confirmed visibility require a compatible `perfil-objetivos.json`. The included profile is disabled because its missing fields have not been verified. Without such a profile, the approximate mode does not identify enemies or prove line of sight. Autofire cannot operate in approximate mode.

## Requirements and external prerequisites

Use x64 Windows 11 with .NET Framework 4.8 and the x64 Visual C++ runtime. The runtime loader must be able to resolve `VCRUNTIME140.dll` and the Windows system DLLs used by MemProcFS/LeechCore.

LiveKD 5.65, the x64 Debugging Tools components and the correct kernel symbols must already be available locally. LiveKD and Microsoft kernel PDB files are deliberately absent from this public archive: the accompanying upstream terms restrict redistribution. They are prerequisites, not files supplied by this project. The launcher does not download them or configure a remote symbol server.

The launcher pins LiveKD to this SHA256:

```text
E2884FA2BAA44A092FC7E8A6FE59FC02AF754FF18B1E10ECAEBD91A7097D5FB9
```

`procedencia-livekd.json` contains the value used by the program. Matching this hash checks the expected file; it does not validate your kernel symbols or game offsets.

## Configure local prerequisites

1. Extract the archive into one writable folder. Keep the EXEs, sources, scripts and `runtime` together.
2. Copy `local-prerequisites.example.json` to `local-prerequisites.json` and replace its example paths with your existing local files.
3. `LiveKdPath` must identify your local `livekd64.exe` matching the pinned version/hash.
4. `KernelSymbolDirectory` must contain the kernel PDB matching your Windows kernel. This is an ordinary local directory, not `srv*` syntax, a URL or a symbol-server path. An unrelated PDB is not sufficient even if its filename matches.
5. `DebuggingToolsDirectory` must contain your local x64 `dbghelp.dll` and `symsrv.dll`. If omitted, the launcher checks beside LiveKD.
6. Keep this personal configuration out of any file you redistribute. The launcher also accepts `%LOCALAPPDATA%\ESP-Assist\local-prerequisites.json` when no configuration is present beside the EXE.

Example:

```json
{
  "LiveKdPath": "C:\\Tools\\LiveKD\\livekd64.exe",
  "KernelSymbolDirectory": "C:\\Tools\\WindowsSymbols\\CurrentKernel",
  "DebuggingToolsDirectory": "C:\\Tools\\DebuggingTools\\x64"
}
```

The launcher checks local file hashes and prevents duplicate acquisition sessions. It does not remove an existing `livekd.dmp` or interfere with an existing LiveKD service. Resolve such a conflict through the tool that created it before starting another reader.

## Start and controls

Launch `Abrir-ESP.exe`, accept its administrator prompt, and focus your compatible local test game. LiveKD's own license terms apply to its use; the launcher passes its EULA-acceptance option when starting it.

| Control | Action |
| --- | --- |
| Right mouse button | Hold to request assistance; assistance must also be enabled in the menu. |
| INSERT | Show/hide settings while the game or menu has focus. |
| F6 | Mark/unmark the single player under the crosshair as a teammate for this session. |
| F7 | Clear the session's manual teammate marks. |
| F10 | Close the overlay. |

The application interface retains Spanish labels; this documentation explains their meaning in English. Close the menu and return focus to the game before using assistance. Release and press right mouse again after a target is lost, settings change, or input is rejected. A button already held during initialization must first be released. The application also retains its one-hour automatic session limit.

Manual teammate marks are local and temporary. They expire after five seconds without observing the actor and reset when the world or local pawn changes. F6 does not guess between overlapping players. A known or manually marked ally is excluded from assistance even when its ESP display is enabled.

## Estimated versus confirmed targeting

The supplied display/preferences file preserves the approximate setup: `AssistRequireEnemy`, `AssistRequireVisible` and `AssistRequireBones` are false. This lets the controller use an estimated point from a player box. It can include players whose team is unknown unless you exclude them manually or enable strict filtering.

`Cabeza` means Head. `Pecho` means Chest. Estimated Head uses `AssistPointPercent`, measured downward from the top of the box; estimated Chest uses 45%. A validated profile uses the corresponding bone's projected position instead. The estimated standing box does not certify the character's actual pose or head location.

To require confirmed data, enable the strict options. With the included disabled profile, these options can leave no eligible targets. This is intentional: unavailable metadata stays unknown. `AutoFireEnabled` starts false when settings are loaded and autofire additionally requires a verified enemy, bone and fresh visibility result. Enabling its checkbox alone is insufficient.

## New: per-axis movement calibration

`Calibracion X (px/unidad)` and `Calibracion Y (px/unidad)` set the expected screen displacement in pixels produced by one relative mouse unit on each axis. Both default to 1.00, preserving the previous controller model. Their accepted range is 0.10–20.00.

- A larger value requests less movement for the same screen error. Increase it on an axis that consistently overshoots.
- A smaller value requests more movement. Decrease it on an axis that consistently responds too weakly.
- Adjust one axis at a time in a controlled local scene, with a stationary target and unchanged camera/input settings.
- Recalibrate after changing sensitivity, resolution, FOV or ADS behavior. There is no automatic ADS profile switch or automatic sensitivity measurement.

For example, a configured horizontal value of 4 means that a 40-pixel horizontal error corresponds to approximately 10 mouse units before smoothing. A vertical value of 2 treats the same vertical error as approximately 20 units. These are a linear model, not measurements of your game.

The controller applies response smoothing, a maximum relative-input rate and per-axis quantization limits. It does not keep accumulating a full unit when that unit would exceed the remaining calibrated error. With high sensitivity, one input unit can be several pixels; an integer input API cannot guarantee a subpixel final position. The screen stopping zone remains 1.5 pixels, but the attainable residual also depends on your calibration and the game's response.

## Build from source

Install a modern Roslyn compiler with the .NET desktop tools. The build script looks for `csc.exe` and then uses Visual Studio's installed `vswhere` to locate Roslyn. You can supply the compiler explicitly.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Recompilar.ps1

# Or specify an installed Roslyn compiler:
powershell -NoProfile -ExecutionPolicy Bypass -File .\Recompilar.ps1 -CompilerPath "C:\Tools\Roslyn\csc.exe"
```

Both the overlay and launcher are compiled x64. The build uses .NET Framework references, not a modern .NET desktop runtime. It updates the project executable hashes in `procedencia-livekd.json`. It does not start acquisition, install prerequisites or download build dependencies. Native dependencies are separate builds; see `NATIVE-SOURCES.md`.

## Verify without reading the game

```powershell
# Hash checks and native export loading only:
powershell -NoProfile -ExecutionPolicy Bypass -File .\Probar-LiveKd.ps1 -Mode Verify

# Compile and run the synthetic suites in isolated folders:
powershell -NoProfile -ExecutionPolicy Bypass -File .\pruebas\Verificar.ps1
```

These checks do not initialize the memory acquisition device or send real mouse input. They cannot demonstrate live-game compatibility. Read `VALIDATION.md` before interpreting the results: the functional suites pass, while the Windows scheduling test has an intermittent timing-threshold failure in the recorded environment.

## Troubleshooting

| Symptom | What to inspect |
| --- | --- |
| Launcher says prerequisites are missing | Configure the local prerequisite paths; no vendor tools are downloaded automatically. |
| Native export verification fails | Check x64 Visual C++ runtime availability and the required DLL hashes. |
| No camera or local character | The target process, game offsets or scene discovery may be incompatible. |
| ESP geometry is displaced | Check the FOV interpretation, client dimensions and actual camera semantics; a fixed offset is not a general correction. |
| No assistance | Check activation, RMB, focus, closed menu, fresh data, range, selection radius and strict filters. |
| Team displays `equipo ?` | Automatic team information is unavailable; use manual exclusions or a validated profile. |
| Autofire remains inactive | It requires authoritative team, bone and visibility information, not an estimated box. |
| Aim overshoots or reacts weakly | Adjust the corresponding calibration axis and check sensitivity/FOV changes. |
| Session ends after about one hour | This is the retained session limit, not an unlimited background service. |

`estado-esp.json` describes the current frame and targeting exclusions. `launcher-status.json`, `livekd-output.txt` and `livekd-error.txt` describe local launcher activity. These are generated locally and are not part of the release archive. Input accepted by Windows is not proof that the game applied the requested camera movement.

## Source, attribution and redistribution

See `SOURCE-GUIDE.md` for implementation details, `NATIVE-SOURCES.md` for the native patch and dependency provenance, and `SOURCE-NOTICE.txt` plus the included upstream license texts for attribution. The release contains no game executables, game dumps, account material or copied kernel symbol files. Project source availability does not grant new rights over vendor prerequisites or third-party code.
