# AutoQuickTest

A diagnostic RimWorld mod for unattended testing. Once the entry scene has been idle for 120 frames, it starts the dev quick test: the same long event as the dev-mode main-menu button, `Root_Play.SetupForQuickTestPlay` followed by `PageUtility.InitGameStart`. In game, for about two minutes, it closes windows that force a pause (mods' welcome dialogs) and holds the speed at superfast. It logs `[autoqt]` milestones to `Player.log`:

- `armed`, `starting dev quick test`, `map ready`
- `closed pausing window <type>`
- `600 ticks`, `2500 ticks`, `15000 ticks`, `60000 ticks`, each with the colonist count

It hooks `Root_Entry.Update` and not `MainMenuDrawer.MainMenuOnGUI`, because menu mods can replace the menu drawer. Do not leave it active: it starts a new colony on every launch.

Build and install it as described in `tools/harmonyprobe/README.md`.
