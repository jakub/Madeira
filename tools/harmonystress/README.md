# HarmonyStress

A diagnostic RimWorld mod. From its `Mod` constructor (the same phase where RimWorld's `CreateModClasses` runs every mod's `Harmony.PatchAll`) it applies no-op Harmony patches to a fixed, sorted sample of `Verse` and `RimWorld` methods, and logs every failure to `Player.log` under `[harmonystress]`.

A failure that repeats on the same method in every run is a real Harmony incompatibility. A failure that moves between runs points to emulation trouble, such as the stale thread contexts that made Mono's garbage collector free live objects (wine `fix(server): re-capture 64-bit threads' snapshot contexts on every stop`).

`stress.txt` in the mod folder sets the run:

    count=400                         methods to patch
    rounds=3                          patch, unpatch, repeat
    kinds=prefix,postfix,transpiler

It changes no game state beyond the no-op patches. Build and install it as described in `tools/harmonyprobe/README.md`.
