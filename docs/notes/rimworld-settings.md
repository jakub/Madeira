# RimWorld on Madeira: per-game settings

This page lists the settings outside the code that RimWorld 1.6 (Unity 2022.3, Mono) needs to run on Madeira. It needs the code fixes on this branch too: the executable-heap tag in wine and `virtual_ios.c`, and the FEX backpatcher fix.

Verified on 2026-10-05 on an iPad Pro 11-inch (M5, iPadOS 27.0.1), with RimWorld 1.6.4871 rev591 from Steam (app 294100). The main menu, options and a dev quicktest map work: pawns move and right-click commands work.

## 1. Madeira library entry

Set these in the game's details page.

| Setting | Value | Why |
| --- | --- | --- |
| Start with | **The game** | RimWorld is DRM-free and runs without Steam. With this choice it shows a "Could not initialize Steam API" dialog at start; select **Ignore**. Madeira Dock was not tested for this setup. |
| Resolution | **native**, or **default** (both under This screen's shape) | Native is the screen's points times its scale, the framebuffer iPadOS draws: 2816x1940 on the iPad Pro 11-inch with the More Space display zoom (which iPadOS then scales down to the 2420x1668 panel), or 2420x1668 (the panel, 1:1) at the default zoom. At 2420x1668 the largest UI scale RimWorld allows is 2 (2420/2.5 is under 1024). Set RimWorld's UI scale to 2.5 (Options → Interface): the interface then lays out at 1126x776, about the size the default gives, but with sharp text and art. Measured on a colony map: 60 FPS, GPU 34% (5.6 ms), 3.3 GB of memory. The default (1160x800 with More Space) is lighter but scales up about 2.4x, which looks soft. RimWorld allows a UI scale only while the screen divided by the scale stays at least 1024x768, so 2.5 is the largest at native. Its own recommended scale there, 1.5, is too small on an 11-inch screen. |
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
<screenWidth>2816</screenWidth>   <!-- = Madeira's Resolution (1048 x 720 for Screen shape) -->
<screenHeight>1940</screenHeight>
<fullscreen>True</fullscreen>
<uiScale>2.5</uiScale>            <!-- 1 for Screen shape -->
```

The values left from the iPhone default were 1048x648 with `fullscreen=False`. They gave a decorated window: its title bar shifted the content, the back buffer was 1048x648 and was stretched onto the 1048x720 monitor (it looked squashed), and taps landed off target.

Unity's own copies of these values are in `user.reg` under `[Software\\Ludeon Studios\\RimWorld by Ludeon Studios]`. The values are `"Screenmanager Fullscreen mode_h…"` (1 is FullScreenWindow, 3 is Windowed), `"Screenmanager Resolution Width_h…"` and `"Screenmanager Resolution Height_h…"`, together with their "Window" variants. RimWorld overwrites them from `Prefs.xml`. Keep them consistent, but `Prefs.xml` is what controls the result.

Close Madeira before you edit either file.

## 4. Display mode list

Madeira handles this by default now. DXMT used to give its DXGI output a synthetic `HMONITOR` (1) instead of user32's monitor handle. Unity's `WinScreenSetup::GetResolutions` compares `DXGI_OUTPUT_DESC.Monitor` with `MonitorFromWindow()`. It found no match, got an empty mode list (`Failed to find a valid fullscreen resolution` in `Player.log`), and fell back to a decorated window, whose title bar shifted every tap.

DXMT now reports user32's monitor by default (`DXMT_WSI_MONITOR_IDENTITY`; set `env.DXMT_WSI_MONITOR_IDENTITY = 0` in `madeira.cfg` to go back to the sentinel). Unity then gets the three virtual-monitor modes and starts borderless at 1048x720. The log shows `[dxgi-modes] … count=3` and no fallback line.

The cursor that Madeira draws over the game, and absolute trackpad moves, used to assume a 1024x768 guest. At 1048x720 that put the arrow up to about 2% below and to the right of the point the game hit-tests. Both now use the live guest size, so the highlighted control and the arrow tip agree.

## 4a. iPad system UI and the trackpad

- **Run Madeira full screen.** Choose Settings → Multitasking & Gestures → Full Screen Apps. A windowed app keeps the status bar, and iPadOS refuses pointer lock to it.
- **What Madeira does while a game runs.** It hides the status bar, gives the first swipe from any edge to the game (a second swipe reaches iOS), and fades the home indicator. iPadOS does not let an app remove the home indicator outright. These preferences come from Madeira's overlay windows as well as the app window, because UIKit reads them from the topmost full-screen window.
- **Lock the pointer with Ctrl+Option+P** (Ctrl+Alt+P), or with the lock button. This is needed for a trackpad or mouse. While locked, the iPad pointer is hidden and cannot reach the screen edges, so the Dock and Control Center stay away. RimWorld draws its own cursor, so Madeira does not lock the pointer by itself. The log line `[hwinput] pointer lock granted=yes/no` shows whether iPadOS accepted the lock.
- **Pointer speed while locked.** One unit of trackpad movement moves the cursor one screen point, at any resolution. iPadOS pointer acceleration is not applied. Raise Mouse sensitivity in the pointer panel to taste.

## 5. Known issues

- **The Options resolution list is empty.** This is expected. Unity offers RimWorld the virtual monitor's modes: 640x480, 800x600 and 1048x720 (logged by `tools/textprobe`). RimWorld's Options menu lists only modes of at least 1024x768, so it shows none of them. Screen shape is 720 lines tall on purpose. Choose the size in Madeira and set it in `Prefs.xml` (section 3). If you want the menu to list modes, a Madeira Resolution of at least 768 lines does that (for example 1280x960), but on an iPad it is letterboxed.
- **"BAD POOL — exiting now".** Now and then the JIT pool cannot be placed when Madeira starts (no address hole fits). It does not depend on the game. Relaunch Madeira.
- **First start is slow.** Mono's call-site patching and DXMT shader compilation make the first start slow. Later starts are faster.

## 6. Not needed

- `madeira.cfg` keys: none are required. The diagnostics used during the investigation have been removed from the build (see `unity-mono-rimworld.md`).
- Mods: the `tools/textprobe` diagnostic mod is optional and changes nothing.
