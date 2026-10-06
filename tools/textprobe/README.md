# TextProbe

A diagnostic RimWorld mod. Once loading finishes it logs translation state, each
game font's glyph metrics and `Text.CalcSize`, and a `TextGenerator` layout to
`Player.log`, under `[textprobe]`. For the first 40 left clicks it also logs, under
`[mouseprobe]`, Unity's mouse position and `Screen` size next to user32's cursor,
window rect, client rect, client origin and window styles, plus RimWorld's UI scale.
That separates a tap offset caused by the game window from one caused by how Madeira
maps touches. It changes no game state.

It found the cause of invisible text under Madeira: `Arial_small` and `Arial_medium`
need an OS font named Arial. Without one, the glyphs are 2x2 with an advance of 0,
and every label measures 0 px wide.

## Build

Copy these assemblies from the game's `RimWorldWin64_Data/Managed` into one directory:
`Assembly-CSharp.dll`, `UnityEngine*.dll` (including `UnityEngine.InputLegacyModule.dll`), `mscorlib.dll`, `System*.dll` and
`netstandard.dll`. Then run:

    dotnet build -c Release -p:RimWorldManaged=/path/to/that/dir

## Install

1. Put `About/` and `Assemblies/TextProbe.dll` under `RimWorld/Mods/TextProbe/`.
2. Add `<li>local.textprobe</li>` after `ludeon.rimworld` in `ModsConfig.xml`.
