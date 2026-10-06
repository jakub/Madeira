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

RimWorld's main UI fonts are Unity dynamic fonts without embedded data. `Arial_small` and `Arial_medium` have `fontNames=Arial`, so they need an installed font whose family is "Arial". The prefix has only Tahoma.

Wine's `FontSubstitutes` and `Replacements` map Arial to Tahoma, but Unity's own font lookup ignores those mappings. Without Arial, every glyph has a 2x2 bitmap and an advance of 0. Every label then measures 0 px wide, and the game draws no text at all.

**To fix it,** put a font with the family name "Arial" in `C:\windows\Fonts\arial.ttf` and register it:

1. Close Madeira. Wine rewrites the registry when it exits.
2. Copy the font to `Documents/wine/drive_c/windows/Fonts/arial.ttf`.
3. In `Documents/wine/system.reg`, add this line to each of these three keys:

   ```
   "Arial (TrueType)"="arial.ttf"
   ```

   - `[Software\\Microsoft\\Windows NT\\CurrentVersion\\Fonts]`
   - `[Software\\Wow6432Node\\Microsoft\\Windows NT\\CurrentVersion\\Fonts]`
   - `[Software\\Microsoft\\Windows\\CurrentVersion\\Fonts]`

**Where to get the font.** The test used the Monotype Arial 5.10 that RimWorld itself contains. It is an embedded sfnt in `RimWorldWin64_Data/resources.assets` at offset `0x51834c` (778552 bytes, 24 tables). Because it belongs to the game, use it only in your own prefix and never redistribute it. This script extracts it:

```python
import struct
a = open("resources.assets", "rb").read(); off = 0x51834c
n = struct.unpack(">H", a[off+4:off+6])[0]
end = max(struct.unpack(">I", a[off+12+16*k+8:off+12+16*k+12])[0] +
          struct.unpack(">I", a[off+12+16*k+12:off+12+16*k+16])[0] for k in range(n))
open("arial.ttf", "wb").write(a[off:off+end])
```

The offset belongs to this RimWorld build. Check it by looking for `\x00\x01\x00\x00` followed by a table directory that contains `DSIG`, `JSTF` and `LTSH`, just after the "Arial" strings.

A fix that Madeira could ship would register an open, metric-compatible font (Arimo or Liberation Sans) as `"Arial (TrueType)"`. That has not been tested yet: Unity may match on the font's internal family name rather than on the registry name.

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

## 4. Known issues

- **Taps land a little too low.** Even at 1048x720 fullscreen, the touch target sits slightly below the tap. Clicks from a trackpad or mouse are accurate. Use **Settings → Pointer → Relative**, or a trackpad, as a workaround. The cause is under investigation (window geometry on the Wine side).
- **The Options resolution list is empty.** Unity gets no usable display modes. `Player.log` shows `Failed to find a valid fullscreen resolution for exclusiveFullscreen …`. The virtual monitor lists standard 32 bpp modes, but the current Screen-shape size is not exposed as a mode that DXGI or Unity can use. Edit `Prefs.xml` instead, as described in section 3.
- **"BAD POOL — exiting now".** Now and then the JIT pool cannot be placed when Madeira starts (no address hole fits). It does not depend on the game. Relaunch Madeira.
- **First start is slow.** Mono's call-site patching and DXMT shader compilation make the first start slow. Later starts are faster.

## 5. Not needed

- `madeira.cfg` keys: none are required. The diagnostic keys used during the investigation (`env.MADEIRA_STUCK_WAIT_SECS`, `metal-validation`, `env.FEX_*` experiments) are optional.
- Mods: the `tools/textprobe` diagnostic mod is optional and changes nothing.
