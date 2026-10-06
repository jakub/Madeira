# RimWorld on Madeira: per-game settings

This page lists the settings outside the code that RimWorld 1.6 (Unity 2022.3, Mono) needs to run on Madeira. It needs the code fixes on this branch too: the executable-heap tag in wine and `virtual_ios.c`, and the FEX backpatcher fix.

Verified on 2026-10-05 on an iPad Pro 11-inch (M5, iPadOS 27.0.1), with RimWorld 1.6.4871 rev591 from Steam (app 294100). The main menu, options and a dev quicktest map work: pawns move and right-click commands work.

## 1. Madeira library entry

Set these in the game's details page.

| Setting | Value | Why |
| --- | --- | --- |
| Start with | **The game** | RimWorld is DRM-free and runs without Steam. With this choice it shows a "Could not initialize Steam API" dialog at start; select **Ignore**. Madeira Dock was not tested for this setup. |
| Resolution | **Screen shape** | Matches the virtual monitor to the display shape: 1048x720 on the iPad Pro 11-inch (1.456:1 against the panel's 1.451:1). The default, 1408x648, has an iPhone shape and gives letterboxing on an iPad. |
| Aspect & scaling | **Fit** | Keeps the layer and the touch mapping in agreement. Use **Aspect** only if the game's back buffer shape differs from the monitor. |

## 2. Wine prefix: an OS font named "Arial"

Madeira now handles this itself (`madeira_ensure_arial_fonts` in `WineProcessBridge.m`). This section explains why, and how to fix a prefix by hand.

RimWorld's main UI fonts are Unity dynamic fonts without embedded data. `Arial_small` and `Arial_medium` have `fontNames=Arial`, so they need an installed font whose family is "Arial". The prefix has only Tahoma. Without Arial, every glyph has a 2x2 bitmap and an advance of 0. Every label then measures 0 px wide, and the game draws no text at all.

**How Unity looks up an OS font.** This was read from the disassembly of `UnityPlayer.dll` 2022.3 with its public PDB.

- `DynamicFontMap` scans `GetWindowsDirectory()\Fonts` once, and only that directory. It reads every `.ttf`, `.ttc`, `.otf` and `.dfont` file.
- It keys each scalable face by **its FreeType family name**, matched exactly and case-sensitively, together with its bold and italic bits.
- It ignores file names, the `...\CurrentVersion\Fonts` registry key, and Wine's `FontSubstitutes` and `Replacements`.
- It skips files it cannot open, so 0-byte files do no harm.

So the font's internal family name has to be "Arial". Copying Liberation Sans in as `arial.ttf` is not enough, because its family name stays "Liberation Sans". Registering it in the registry does not help either.

**What Madeira ships.** Liberation Sans 2.1.5 (SIL OFL 1.1, metric-compatible with Arial), with the family renamed to "Arial" by `tools/fonts/make-arial.py`. Proton does the same (`Makefile.in` in Proton 10.0, `arial_NAMES`/`arial_ORIG`). Madeira copies `arial.ttf`, `arialbd.ttf`, `ariali.ttf` and `arialbi.ttf` into `C:\windows\Fonts` whenever one of them is missing or empty, and never replaces an Arial that you installed. The bundle folder is called `arial-fonts` and not `fonts`, because Wine loads `<bundle>/fonts` as its own data-dir font folder.

**Fixing a prefix by hand.** You need a font whose family name is "Arial" placed in `Documents/wine/drive_c/windows/Fonts/`. The file name and the registry do not matter. Either:

- run `make-arial.py` on the Liberation release, or
- use the Monotype Arial 5.10 inside RimWorld's own `RimWorldWin64_Data/resources.assets`. It is an embedded sfnt at offset `0x51834c` in 1.6.4871 rev591 (778552 bytes; look for `\x00\x01\x00\x00` followed by a table directory with `DSIG`, `JSTF` and `LTSH`). It belongs to the game, so use it only in your own prefix.

## 3. RimWorld's own display preferences

At start, RimWorld applies `Config/Prefs.xml` and overrides Unity's registry values. It writes the file back when it exits.

The file is in `Documents/wine/drive_c/users/mobile/AppData/LocalLow/Ludeon Studios/RimWorld by Ludeon Studios/Config/Prefs.xml`. Set:

```xml
<screenWidth>1048</screenWidth>   <!-- = Madeira's Resolution -->
<screenHeight>720</screenHeight>
<fullscreen>True</fullscreen>
```

The values left from the iPhone default were 1048x648 with `fullscreen=False`. They gave a decorated window: its title bar shifted the content, the back buffer was 1048x648 and was stretched onto the 1048x720 monitor (it looked squashed), and taps landed off target.

Unity's own copies of these values are in `user.reg` under `[Software\\Ludeon Studios\\RimWorld by Ludeon Studios]`. The values are `"Screenmanager Fullscreen mode_h…"` (1 is FullScreenWindow, 3 is Windowed), `"Screenmanager Resolution Width_h…"` and `"Screenmanager Resolution Height_h…"`, together with their "Window" variants. RimWorld overwrites them from `Prefs.xml`. Keep them consistent, but `Prefs.xml` is what controls the result.

Close Madeira before you edit either file.

## 4. Display mode list

Madeira handles this by default now. DXMT used to give its DXGI output a synthetic `HMONITOR` (1) instead of user32's monitor handle. Unity's `WinScreenSetup::GetResolutions` compares `DXGI_OUTPUT_DESC.Monitor` with `MonitorFromWindow()`. It found no match, got an empty mode list (`Failed to find a valid fullscreen resolution` in `Player.log`), and fell back to a decorated window, whose title bar shifted every tap.

DXMT now reports user32's monitor by default (`DXMT_WSI_MONITOR_IDENTITY`; set `env.DXMT_WSI_MONITOR_IDENTITY = 0` in `madeira.cfg` to go back to the sentinel). Unity then gets the three virtual-monitor modes and starts borderless at 1048x720. The log shows `[dxgi-modes] … count=3` and no fallback line.

The cursor that Madeira draws over the game, and absolute trackpad moves, used to assume a 1024x768 guest. At 1048x720 that put the arrow up to about 2% below and to the right of the point the game hit-tests. Both now use the live guest size, so the highlighted control and the arrow tip agree.

## 5. Known issues

- **The Options resolution list is empty.** This is expected. Unity offers RimWorld the virtual monitor's modes: 640x480, 800x600 and 1048x720 (logged by `tools/textprobe`). RimWorld's Options menu lists only modes of at least 1024x768, so it shows none of them. Screen shape is 720 lines tall on purpose. Choose the size in Madeira and set it in `Prefs.xml` (section 3). If you want the menu to list modes, a Madeira Resolution of at least 768 lines does that (for example 1280x960), but on an iPad it is letterboxed.
- **"BAD POOL — exiting now".** Now and then the JIT pool cannot be placed when Madeira starts (no address hole fits). It does not depend on the game. Relaunch Madeira.
- **First start is slow.** Mono's call-site patching and DXMT shader compilation make the first start slow. Later starts are faster.

## 6. Not needed

- `madeira.cfg` keys: none are required. The diagnostics used during the investigation have been removed from the build (see `unity-mono-rimworld.md`).
- Mods: the `tools/textprobe` diagnostic mod is optional and changes nothing.
