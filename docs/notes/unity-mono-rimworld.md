# Madeira local build: 2026-10-05

Madeira `bbbf8d0`. Host: M1 Max, macOS 27.0.1, Xcode 27.0 (`27A266a`) plus the Metal Toolchain component.
Result: an unsigned Debug `Madeira.app` for iphoneos that links, packaged as `Madeira-unsigned.ipa`. It has not been run on a device.

## Submodule pins and local patches (uncommitted, in `patches/`)
- FEX `be778d7` (ios-port-2607): `FEX-ios-port-2607@be778d7.patch`
  - `Core.cpp`: guards the ml316 reporter with `FEX_IOS_HOST`, and the ml622 rpm-cas drain with `!__APPLE__`. rpmalloc is disabled on APPLE.
  - `Arm64.cpp`: guards `IosLogUnimplementedCASPAL`'s body with `_WIN32`, because it calls VirtualQuery.
  - None of these changes alter the PE builds, which define all of these macros.
- dxmt `8937c08` (ios-port): `dxmt-ios-port@8937c08.patch`. Xcode 27's metal needs a 5th memory-flags argument on `__metal_atomic_fetch_add_explicit`.
- wine `3a54f568`, madeira-dock `3cadfbe`: unmodified.
- Re-apply after any `git submodule update --force` or a fresh clone: `git -C FEX apply ../Madeira-build/patches/FEX-*.patch`, and the same for dxmt.

## Inputs not documented in docs/BUILDING.md
- **FreeType 2.13.3**: `git clone --depth 1 --branch VER-2-13-3 https://github.com/freetype/freetype.git research/freetype` (commit `42608f77f20749dd6ddc9e0536788eaad70ea4b5`), then `bash build/freetype-ios/build.sh`. Both ntdll-unix and win32u-unix need it.
- **wine/build-macos**: the unix scripts read `config.h` and the widl headers from here.
  - Configure command: `../configure --enable-archs=aarch64 --without-x --disable-tests --enable-winegstreamer && make include/all`.
  - `wine/build-arm64ec` uses the same flags with `--enable-archs=arm64ec`, run with llvm-mingw on PATH.
  - Both need brew bison 3 first on PATH.
  - Configure enables the gnutls and freetype paths only because brew `gnutls` and `freetype` are installed on the host. Without them, bcrypt and secur32 compile as stubs and nothing reports an error.
- **Base `libwineserver.a`**: `build/wineserver/build.sh` requires a prebuilt base archive that has no provenance in the repo. It was rebuilt from the 23 `wine/server/*.c` files that the script does not compile itself; see `provenance/wineserver-base.sh` and `wineserver-base.flags`.
- **DXMT `air_*.h` headers**: build.sh never generates them. They come from dxmt's meson recipe:
  `xcrun -sdk macosx metal -std=metal3.1 --target=air64-apple-macos14.0 -o X.air -c dxmt/src/airconv/shaders/X.metal && xxd -n X -i X.air X.h` for `air_msad`, `air_samplepos` and `air_tessellation`, output into `build/dxmt-ios/shader-headers/`.
- **`libdxmt_combined.a`**: build.sh only refreshes an existing archive, so the first build needs `xcrun -sdk iphoneos libtool -static -o libdxmt_combined.a obj/*.o ../../toolchains/llvm-ios-build/lib/*.a`.
- **Madeira Dock**: run `bash build/madeira-dock/build.sh` before the app build. BUILDING.md never lists this step. The script stages the gitignored `arm64ec-windows/dockhost.exe` and `dock-notices.txt`. Without them, the library hides every Steam section and nothing reports an error.
- **VC++ runtime**: the 12 DLLs from `VC_redist.x64.exe` 14.44.35211 (SHA-256 `cc0ff0eb…6b713b`, which matches the Microsoft CDN path), extracted with cabextract and msiextract. Authenticode blocks are intact.

## Toolchains
- **llvm-project** at `8dfdcc7b7bf66834a761bd8de445840ef68e4d1a` (llvmorg-15.0.7).
  - The host build makes only `llvm-tblgen`.
  - The iOS build uses the BUILDING.md flags plus `CMAKE_OSX_DEPLOYMENT_TARGET=17.0`, `LLVM_BUILD_UTILS=Off`, `LLVM_ENABLE_{ZSTD,TERMINFO,LIBXML2,LIBEDIT}=Off` and `LLVM_TABLEGEN=<host tblgen>`.
  - It builds only the 34 libraries in `provenance/libs.txt`, about 4 minutes on the M1 Max.
- **llvm-mingw 20260421** ucrt macos-universal: SHA-256 `bd85a397…3cb3f7`, verified.
- **FEX iOS configure**: add `-DCMAKE_SYSTEM_PROCESSOR=arm64 -DTUNE_CPU=none -DCMAKE_OSX_DEPLOYMENT_TARGET=17.0`, because cmake 4.3 leaves the processor empty and `TUNE_CPU=native` reads `/proc/cpuinfo`. Do not define `FEX_IOS_HOST`, which is the PE-module switch. LTO is on, so the archives are Xcode 27 bitcode.

## App
```sh
bash build/stage-licenses.sh
xcodebuild -project app/Madeira.xcodeproj -scheme Madeira -destination 'generic/platform=iOS' CODE_SIGNING_ALLOWED=NO build
```
There are about 81 "built for newer iOS (18.0) than being linked (17.0)" warnings. They come from DXMT objects and are harmless, since the app requires iOS 26 or later.

## Signed device install (verified 2026-10-05: iPad Pro 11" M5, iPadOS 27)
```sh
xcodebuild -project app/Madeira.xcodeproj -scheme Madeira -destination 'id=00008142-001609303E2B401C' \
  DEVELOPMENT_TEAM=AA2J3TYPRN MADEIRA_BUNDLE_IDENTIFIER=me.jakub.madeira \
  -allowProvisioningUpdates -allowProvisioningDeviceRegistration build
xcrun devicectl device install app --device 00008142-001609303E2B401C <DerivedData>/Build/Products/Debug-iphoneos/Madeira.app
```
- Prerequisites:
  - Xcode signed in to the Apple account.
  - The current Program License Agreement accepted. If it isn't, the portal API returns "PLA Update available" and Xcode shows "Failed to load provisioned devices".
  - Developer Mode enabled on the iPad.
- `-allowProvisioningDeviceRegistration` is required to register a new device from the CLI. `-allowProvisioningUpdates` alone does not register it.
- Signed entitlements:
  - Present: `get-task-allow` (app and helper) and `increased-memory-limit`.
  - Dropped: `com.apple.security.cs.allow-jit`, a macOS entitlement that development profiles for iOS do not carry. JIT does not need it.
- JIT: built-in StikJIT, using in-app pairing (iPadOS 27) plus LocalDevVPN. JIT and Memory+ both showed green.
  - Set **Settings → JIT** to Built-in explicitly. Automatic picks StikDebug whenever its URL scheme is installed and does not fall back.

## Driving the iPad from the Mac (no taps)
- Screenshot: `xcrun devicectl device capture screenshot --device <udid> --destination x.png`. Xcode 27 adds `capture screen-record` too.
- Start a library game, running the JIT flow first exactly like Play: `xcrun devicectl device process launch --device <udid> --terminate-existing --payload-url 'madeira://play?exe=<urlencoded C:\path>' me.jakub.madeira`.
  - The path is the entry's `windowsPath`, which is `C:\` + its relativePath with backslashes.
  - Example for RimWorld: `C:\Program Files (x86)\Steam\steamapps\common\RimWorld`.
- Files in the app container: `xcrun devicectl device copy from|to --domain-type appDataContainer --domain-identifier me.jakub.madeira`.
  - Game logs: `Documents/logs/<exe>-<timestamp>.txt`.
  - Config: `Documents/madeira.cfg`. Always read it, edit it, then write it back; never overwrite it blind.
- Device Hub (`Xcode.app/Contents/Applications/DeviceHub.app`) did not expose a window to accessibility automation.
- zsh does not word-split unquoted variables, so do not keep devicectl arguments in a `$VAR`.

## RimWorld (Unity 2022.3 Mono) status, 2026-10-05
- **Fixed locally: crash at Mono init.** Upstream issue #123 describes it.
  - Cause: the executable NT heap (`HEAP_CREATE_ENABLE_EXECUTE`, 1 MB grow) was classified as a JIT code chunk by `ios_guest_anon_rwx_view_ok`'s size rule.
  - Result: Wine's LFH `InterlockedAnd` (ldaxr/stlxr) looped forever under store emulation.
  - Fix: heap.c tags the reservation with `MADEIRA_MEM_EXTENDED_PARAMETER_HEAP` and virtual_ios.c treats tagged views as data. Patches are in `patches/*exec-heap.patch`.
  - Baseline: an unmodified rebuild of ntdll.dll matches the shipped one, except the 3-byte timestamp.
- **Open: hang on the loading screen**, after Player.log's `mscorlib ... remapped` line. 0 FPS, CPU about 5%.
- **Ruled out for the hang:**
  - the sync engine (fastsync and Wine standard sync behave the same)
  - real thread suspension
  - OutputDebugString
  - any single server wait longer than 3 s
- **Diagnostic patch (since reverted):** `build/wineserver/fd_ios.c` ran the ml585 stuck-wait report when `MADEIRA_STUCK_WAIT_SECS` was set, not only in desktop mode.
- **Symbols:** Unity publishes PDBs at `https://symbolserver.unity3d.com/<pdb>/<GUID><age>/<pdb-without-b>_` (a CAB file; unpack with cabextract). Put the PDB next to UnityPlayer.dll and llvm-symbolizer finds it.

## RimWorld loading hang: root cause and FEX fix (2026-10-05, untested on device)
- **What hangs:** a FEX self-deadlock caused by our heap fix.
  - Mono backpatches code that lives inside the executable heap. FEX's iOS `MonoBackpatcherWrite` holds `CodeInvalidationMutex` exclusively.
  - `IosMonoResolveRW` misses, because the heap is no longer in the JIT pool.
  - The direct store then faults on the SMC-trapped page, and `HandleRWXAccessViolation` waits on the same mutex.
  - The log records it directly: `[fexlock] STUCK write-wait ... owner tid=012c` on thread 012c itself.
- **The fix:** `SyscallHandler::WriteUntrackedLocked` (FEXCore header). ARM64EC `ECSyscallHandler` does `BeginUntrackedWriteLocked`, then the store, then `ReprotectRWXIntervals`, under the held lock and without taking `ThreadCreationMutex`. The patch is `patches/FEX-ios-port-2607@*.patch`, which also carries the earlier guards.
- **Building xtajit64.dll (FEX ARM64EC).** `build/fex-arm64ec/build.sh`'s configure is WRONG for a macOS host. Configure from scratch with:
  ```
  cmake -S FEX -B FEX/build-arm64ec -G Ninja -DCMAKE_BUILD_TYPE=Release -DCMAKE_TOOLCHAIN_FILE=$PWD/FEX/Data/CMake/toolchain_mingw.cmake \
    -DMINGW_TRIPLE=arm64ec-w64-mingw32 -DFEX_IOS_HOST_BUILD=ON -DCMAKE_C_FLAGS=-DFEX_IOS_HOST -DCMAKE_CXX_FLAGS=-DFEX_IOS_HOST \
    -DCMAKE_ASM_FLAGS=-DFEX_IOS_HOST -DENABLE_LTO=OFF -DENABLE_ASSERTIONS=OFF -DENABLE_JEMALLOC_GLIBC_ALLOC=OFF -DBUILD_TESTING=OFF \
    -DBUILD_FEXCONFIG=OFF -DTUNE_ARCH=generic -DTUNE_CPU=none -DCMAKE_POLICY_VERSION_MINIMUM=3.5 \
    -DCMAKE_DISABLE_FIND_PACKAGE_{fmt,unordered_dense,Zycore,Zydis,xxhash,Catch2,range-v3}=TRUE
  cmake --build FEX/build-arm64ec --target arm64ecfex && cp FEX/build-arm64ec/Bin/libarm64ecfex.dll app/Madeira/arm64ec-windows/xtajit64.dll
  ```
  - `FEX_IOS_HOST_BUILD` is required: without it, ld.lld segfaults on the macOS-host link.
  - Disabling find_package is required: otherwise Homebrew's `fmt` leaks into the mingw build.
  - Result: the unmodified rebuild matches the shipped `.rdata` exactly, but `.text` is about 24 KB smaller. The codegen flags (likely CPU tuning) are unknown. If behaviour diverges beyond the fix, test the unpatched rebuild as the control.

## Diagnostics used during the investigation (all reverted)
- **dxmt `src/winemetal/unix/winemetal_unix.c`:** the A8 trace (texture creation, uploads, readback). Added in dxmt `95ad350`, reverted in `b50ccae`.
- **`app/Madeira/Info.plist`:** `MetalCaptureEnabled = YES`, for the GPU frame capture. Added in `86d5965`, reverted in `73e6849`.
- **`build/wineserver/fd_ios.c`:** the stuck-wait report outside desktop mode, gated on `MADEIRA_STUCK_WAIT_SECS`. Added in `2fe4ec2`, reverted in `04598d2`.
- **On the device:** the TextProbe mod, an `ft-test` library entry, and `madeira.cfg` keys (`MADEIRA_STUCK_WAIT_SECS`, `DXMT_DISPLAY_MODE_STATS`). These are removed as part of the clean-up.
- **Kept as tools:** `tools/textprobe` (the RimWorld probe mod) and `tools/ft-test`.
- **Keep, these are the fixes:**
  - wine `heap.c` and `virtual_ios.c` VPROT_HEAP (exec-heap classification)
  - FEX `WriteUntrackedLocked` (backpatcher deadlock)
  - the FEX native-build guards
  - the dxmt Metal atomic. Prefer dxmt#18's public `atomic_fetch_add_explicit` form.

## Text bug findings so far (RimWorld renders no text; everything else draws)
- **Not DXMT or Metal.** A8 uploads read back byte-identical; Metal validation is clean. The GPU frame capture shows 3 pipelines, 35 draws, no text pipeline, and no font atlas bound.
- **Unity never issues text draws.** Only ONE glyph is rasterised all session: an 8x11 bitmap into a dynamic font atlas.
- **Not OS fonts (WRONG, see below).** resources.assets embeds 3 TrueType fonts (MS Arial at 0x51834c, Calibri with EBDT bitmaps at 0x62cd8c, a FontForge face at 0x5d7420). The prefix's FontSubstitutes maps Arial to Wine's Tahoma, which has glyf outlines.
- **Not the FEX Mono backpatcher.** Verified: with FEX_MONOHACKS=0, `[mono-cfg] MonoHacks=0` and text is still missing.
- **FEX_MULTIBLOCK=0 and HostFeatures=disableavx:** both negative on screen, but the effective values were NOT yet confirmed in the logs.
- **Current hypothesis:** FreeType's outline rasterisation fails under FEX/ARM64EC, possibly setjmp/longjmp or SEH unwinding. Embedded bitmap strikes still work.

## TEXT BUG: SOLVED (2026-10-05 19:00), missing Arial in the Wine prefix
- **Root cause.** RimWorld's main UI fonts `Arial_small` and `Arial_medium` are Unity dynamic fonts with no embedded data (`fontNames=Arial`). They need an OS font whose family is "Arial".
  - Madeira's prefix has only Tahoma, and maps Arial to Tahoma through FontSubstitutes and Replacements. Unity's own OS-font lookup ignores that substitution.
  - So every glyph came back 2x2 with advance=0, labels measured 0 px wide, and IMGUI drew nothing.
  - `Calibri_tiny` embeds its data and always worked (CalcSize 62 px).
- **Proof.** The TextProbe mod (`scratchpad/textprobe`) logs GetCharacterInfo and CalcSize to Player.log.
  - Before: Arial_small 'N' advance=0, glyph 2x2; CalcSize("New colony") = 0x22.
  - After: 'N' advance=10, glyph 10x13; CalcSize = 73x22. Text renders on the device.
- **Local fix applied.**
  - Copied RimWorld's own embedded Monotype Arial 5.10 (carved from resources.assets @0x51834c, 778552 bytes) to `C:\windows\Fonts\arial.ttf`.
  - Added `"Arial (TrueType)"="arial.ttf"` under `HKLM\...\Windows NT\CurrentVersion\Fonts`, `Wow6432Node\...\Fonts` and `Windows\CurrentVersion\Fonts` in `Documents/wine/system.reg`. Backups are in `scratchpad/reg/`.
- **Durable fix still open.** MS Arial can't be shipped. Options:
  - (a) Register `"Arial (TrueType)"` against an open metric-compatible font such as Arimo or Liberation Sans. Untested whether Unity matches on the registry name or on the font's internal family name.
  - (b) A user-supplied Arial.
  - (c) Madeira copying the game's own embedded Arial when one exists.
- **The probe mod is still installed and enabled.** It lives in `RimWorld/Mods/TextProbe` with `local.textprobe` in ModsConfig.xml, and changes nothing in-game. The original ModsConfig is backed up at `scratchpad/textprobe/ModsConfig.backup.xml`.

## Durable font fix (shipped 2026-10-05)
- **How Unity looks fonts up, from the disassembly.** This was read from UnityPlayer.dll 2022.3 with its public PDB.
  - `DynamicFontMap::StaticInitialize` scans `GetWindowsDirectory()\Fonts` once.
  - It keys each scalable face by its FreeType family name, matched exactly and case-sensitively, together with its bold and italic bits.
  - It never reads file names or the registry, and it skips files it cannot open.
  - So option (a) above cannot work: a Liberation file stays "Liberation Sans" whatever it is named or registered as.
- **Why the first Liberation test seemed to work.** Unproven. The likeliest explanation is that the copy had not landed and the MS Arial was still in place.
- **What ships.** Liberation Sans 2.1.5 with its family renamed to "Arial" (`tools/fonts/make-arial.py`, as Proton does).
  - The bundle carries it in `arial-fonts/`. It is not called `fonts/`, because Wine loads `<bundle>/fonts` as its data-dir font folder.
  - `madeira_ensure_arial_fonts` copies `arial.ttf`, `arialbd.ttf`, `ariali.ttf` and `arialbi.ttf` into the prefix when they are missing or empty.
  - Verified on the device: 'N' advance=10, CalcSize("New colony") = 73x22, and the files are restored after being emptied.
