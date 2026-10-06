# TextProbe

A diagnostic RimWorld mod. Once loading finishes it logs translation state, each
game font's glyph metrics and `Text.CalcSize`, and a `TextGenerator` layout to
`Player.log`, under `[textprobe]`. It changes no game state.

It found the cause of invisible text under Madeira: `Arial_small` and `Arial_medium`
need an OS font named Arial. Without one, the glyphs are 2x2 with an advance of 0,
and every label measures 0 px wide.

## Build

Copy these assemblies from the game's `RimWorldWin64_Data/Managed` into one directory:
`Assembly-CSharp.dll`, `UnityEngine*.dll`, `mscorlib.dll`, `System*.dll` and
`netstandard.dll`. Then run:

    dotnet build -c Release -p:RimWorldManaged=/path/to/that/dir

## Install

1. Put `About/` and `Assemblies/TextProbe.dll` under `RimWorld/Mods/TextProbe/`.
2. Add `<li>local.textprobe</li>` after `ludeon.rimworld` in `ModsConfig.xml`.
