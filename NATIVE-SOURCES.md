# Native dependencies and reproducibility

The project source and the native dependency source are included separately. A managed rebuild does not rebuild the native libraries.

## MemProcFS

`runtime/vmm.dll` is the existing unmodified MemProcFS 5.19.0.251 library. `third-party/MemProcFS-v5.19-source.zip` contains the upstream source tree at commit `2e85ff7edc3772e24bce28bdc4cccb744a8b2456` (tag `v5.19`). Its `vmm/version.h` declares the same four version components. This version match identifies the supplied source baseline; a byte-for-byte rebuild of the vendor binary has not been demonstrated.

The upstream license text is `runtime/LICENSE.txt` (AGPL-3.0). `runtime/license_info_all.txt` describes individual components; `license_lz4.txt` accompanies `tinylz4.dll`. Preserve their copyright and license notices. The source tree also contains its own component notices and build projects.

The source archive is complete at the pinned commit, rather than a hand-picked set of excerpts. Upstream build dependencies, toolchain versions and vendor build settings remain separate requirements. No automated installer or download script is included.

## LeechCore File-backend adjustment

`runtime/leechcore.dll` contains the existing local File-backend concurrency adjustment. It is not an official LeechCore update and does not modify game memory.

The change sets `ctxLC->fMultiThread` to false in `DeviceFile_Open`. The backend's own file locks remain; LeechCore's context lock serializes access to the shared contiguous-read context. This addresses the previously reproduced concurrent File-backend crash and can reduce parallel throughput.

`ajuste-nativo` contains:

- `LeechCore-fuentes-modificadas.zip`: the complete modified source tree based on upstream commit `7436c5e9720c7bc816260f29c705332197ea286d`.
- `device_file-serial.diff`: the source change.
- `device_file.c` and `leechcore.c`: directly inspectable relevant files.
- `LICENSE-LeechCore.txt`: GPL-3.0 license.
- `leechcore-original.dll`: the exact original library needed to reproduce the version-specific binary change.
- `patch_serial.py`: checks the original SHA256 and instruction before applying the one-byte change.
- `cambio.json`: pinned patch metadata.
- `NativeStress.cs`: concurrent reads of a synthetic local file.

Original SHA256:

```text
87B69A036772E58D6D9297982F6029D02EA0DBAF98C91F366E3FBB28F6045673
```

Adjusted SHA256:

```text
120145F081A2C96A8615A236E1181EFBD73B4B868F76E78689D6A0558D8604C2
```

Reproduce in a separate working folder with Python 3:

```powershell
python .\ajuste-nativo\patch_serial.py .\ajuste-nativo\leechcore-original.dll .\leechcore-reproduced.dll
```

The script refuses any unrecognized original. It changes one immediate in the pinned build, not arbitrary versions. Compare the result against the adjusted hash before using it. Building the supplied modified C source is a separate route; its output need not be byte-identical to the patched vendor binary.

## Vendor prerequisites outside the archive

LiveKD, Microsoft debugging DLLs, Visual C++ redistributables and kernel PDB files are not supplied by this public archive. Use your local licensed copies under their respective terms. The launcher expects LiveKD 5.65, local x64 debugging tools, and symbols for the current Windows kernel. It does not retrieve them.

Native imports are not all proof of runtime use: the libraries support more devices and APIs than this project invokes. The reduced runtime omits Python/Java bindings, standalone tools, FPGA drivers, remote-device plugins and YARA. Export loading and the native File-backend stress check are recorded in `VALIDATION.md`; end-to-end acquisition with that reduced runtime remains unverified.

## Attribution

MemProcFS and LeechCore are by Ulf Frisk and their upstream contributors. The local serialization change and project integration are documented separately. Microsoft retains ownership and licensing of its prerequisites. No new project notice replaces upstream notices or assigns ownership of their work to the release maintainer.
