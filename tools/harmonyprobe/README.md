# HarmonyProbe

A diagnostic RimWorld mod. It checks whether Harmony, the runtime patching library that most RimWorld mods depend on, works under Madeira. Harmony rewrites methods by writing jumps into code that is already JIT-compiled; under Madeira that code lives in W^X memory managed by FEX.

At startup the mod applies a Harmony patch of each kind to a method the game has already compiled and run:

- a postfix on `Text.CalcSize`
- a prefix on `MainMenuDrawer.MainMenuOnGUI`
- an identity transpiler plus a postfix on `Text.CalcHeight`

It then calls the patched methods directly and logs the hit counts after 1 and after 600 main-menu frames, all to `Player.log` under `[harmonyprobe]`. It changes no game state.

Result on 2026-10-05 (iPad Pro 11-inch M5, RimWorld 1.6.4871 with all DLC, Harmony 2.4.2.0): every patch applied and was live at once, and after 600 frames the CalcSize postfix had run 32,409 times. There was no fault.

## Build

The references are the same as `tools/textprobe` (the game's `RimWorldWin64_Data/Managed`), plus `0Harmony.dll` from the Harmony mod (`HarmonyMod/Current/Assemblies`, from https://github.com/pardeike/HarmonyRimWorld/releases):

    dotnet build -c Release -p:RimWorldManaged=/path/to/Managed -p:HarmonyAssemblies=/path/to/HarmonyMod/Current/Assemblies

## Install

1. Put the Harmony mod in `RimWorld/Mods/Harmony`.
2. Put this mod's `About/` and `Assemblies/HarmonyProbe.dll` in `RimWorld/Mods/HarmonyProbe/`.
3. In `ModsConfig.xml`, add `<li>brrainz.harmony</li>` before `ludeon.rimworld` and `<li>local.harmonyprobe</li>` at the end.
