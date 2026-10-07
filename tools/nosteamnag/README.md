# NoSteamNag

A small RimWorld mod. When RimWorld runs without the Steam client (Madeira's "Start with: The game"), it opens a "Could not initialize Steam API" dialog at every start. This mod drops that one dialog (translation key `SteamClientMissing`) with a Harmony prefix on `WindowStack.Add`, and logs `[nosteamnag] suppressed the Steam client dialog` once. Clicking Ignore only closes the dialog, so nothing is lost.

It also matters for testing: the dialog is modal and blocks `MainMenuOnGUI`, so main-menu markers never fire while it is open.

Build and install it as described in `tools/harmonyprobe/README.md`. Its packageId is `local.nosteamnag`; it depends on Harmony.
