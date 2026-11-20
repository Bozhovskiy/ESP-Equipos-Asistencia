# ARC Raiders ESP — 1.0 Final

One-time experimental source and binary release. The archive contains the x64 overlay, its launcher, all project source files, reproducible build scripts, synthetic tests, and the native source material described below. This is a snapshot; there is no updater.

The reader targets `PioneerGame.exe` and contains game-specific offsets. It is not a universal Unreal Engine SDK. The release has not been verified against a current public game session or a specific server build. Treat the source as a starting point for a compatible local test build.

## What is included

- Player boxes, distance labels, radar and off-screen indicators.
- Adjustable appearance, range, refresh rate and horizontal/vertical FOV interpretation.
- Team, visibility and validated-bone skeleton labels when a compatible profile is present.
- Manual teammate exclusions and optional display filters for allies and unknown teams.
- Source for the overlay and launcher, tests, native patch and version-pinned native source archives.

Team labels, visibility labels and the skeleton posture require a compatible `perfil-objetivos.json`. The included profile is disabled because its missing fields have not been verified. Without such a profile that metadata stays unknown and the skeleton is not drawn.

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
| INSERT | Show/hide settings while the game or menu has focus. |
| F6 | Mark/unmark the single player under the crosshair as a teammate for this session. |
| F7 | Clear the session's manual teammate marks. |
| F10 | Close the overlay. |

The application interface retains Spanish labels; this documentation explains their meaning in English. Close the menu and return focus to the game before using the hotkeys. The application also retains its one-hour automatic session limit.

Manual teammate marks are local and temporary. They expire after five seconds without observing the actor and reset when the world or local pawn changes. F6 does not guess between overlapping players. A known or manually marked ally can be hidden from the display with the ally filters.

## Display metadata

Team, visibility and bone data come from `perfil-objetivos.json`. When the profile matches the test build, the distance card shows the relation (`aliado`, `rival`, `equipo ?`) and the head line-of-sight state (`LOS cabeza`, `oculto cabeza`, `LOS ?`), and the skeleton posture is drawn when the bone pose passes validation. With the included disabled profile this metadata stays unknown and the skeleton is not drawn.

The ally and unknown-team display filters, and the F6/F7 manual marks, control which players the overlay draws. They never change any game state.

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
| Team displays `equipo ?` | Automatic team information is unavailable; use manual exclusions or a validated profile. |
| Session ends after about one hour | This is the retained session limit, not an unlimited background service. |

`estado-esp.json` describes the current frame and display filters. `launcher-status.json`, `livekd-output.txt` and `livekd-error.txt` describe local launcher activity. These are generated locally and are not part of the release archive.

## Source, attribution and redistribution

See `SOURCE-GUIDE.md` for implementation details, `NATIVE-SOURCES.md` for the native patch and dependency provenance, and `SOURCE-NOTICE.txt` plus the included upstream license texts for attribution. The release contains no game executables, game dumps, account material or copied kernel symbol files. Project source availability does not grant new rights over vendor prerequisites or third-party code.
