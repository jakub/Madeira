# QuitTest

A diagnostic RimWorld mod. 300 main-menu frames after the menu first draws, it calls `Root.Shutdown()`, the same call as the menu's Quit button, and logs `[quittest] quitting via Root.Shutdown()` to `Player.log`.

A quit that completes logs `[WineProc] Wine exited with code 0` in Madeira's log. Use it to check that a change has not brought back the quit hang that ml716 fixed (Mono wedged in `suspend_sync_nolock`). Do not leave it active.

Build and install it as described in `tools/harmonyprobe/README.md`.
