// AutoQuickTest: starts the dev quick test (what the main menu's dev Quick test button does) once
// the entry scene has been idle for 120 frames, then runs the colony at superfast speed and logs [autoqt] milestones.
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace AutoQuickTest
{
    public class AutoQuickTestMod : Mod
    {
        static int menuFrames, ticks;
        static bool started;

        public AutoQuickTestMod(ModContentPack content) : base(content)
        {
            var h = new Harmony("local.autoquicktest");
            // Root_Entry.Update, not MainMenuDrawer.MainMenuOnGUI: menu mods can replace the menu drawer.
            h.Patch(AccessTools.Method(typeof(Root_Entry), "Update"),
                postfix: new HarmonyMethod(typeof(AutoQuickTestMod), nameof(MenuPostfix)));
            Log.Message("[autoqt] armed");
            h.Patch(AccessTools.Method(typeof(Game), nameof(Game.FinalizeInit)),
                postfix: new HarmonyMethod(typeof(AutoQuickTestMod), nameof(FinalizePostfix)));
            h.Patch(AccessTools.Method(typeof(TickManager), nameof(TickManager.DoSingleTick)),
                postfix: new HarmonyMethod(typeof(AutoQuickTestMod), nameof(TickPostfix)));
            h.Patch(AccessTools.Method(typeof(Root_Play), "Update"),
                postfix: new HarmonyMethod(typeof(AutoQuickTestMod), nameof(PlayPostfix)));
        }

        static void MenuPostfix()
        {
            if (started || Current.ProgramState != ProgramState.Entry || LongEventHandler.AnyEventNowOrWaiting) return;
            if (++menuFrames < 120) return;
            started = true;
            Log.Message("[autoqt] starting dev quick test");
            LongEventHandler.QueueLongEvent(delegate
            {
                Root_Play.SetupForQuickTestPlay();
                PageUtility.InitGameStart();
            }, "GeneratingMap", true, GameAndMapInitExceptionHandlers.ErrorWhileGeneratingMap);
        }

        static void FinalizePostfix()
        {
            Log.Message("[autoqt] map ready: " + (Find.CurrentMap?.ToString() ?? "no map"));
            if (Find.TickManager != null) Find.TickManager.CurTimeSpeed = TimeSpeed.Superfast;
        }

        static int playFrames, closed;

        // Mods open welcome windows that force a pause once the map is up. For the first
        // ~2 minutes in game, close windows that pause the game and keep it at superfast.
        static void PlayPostfix()
        {
            if (Current.ProgramState != ProgramState.Playing || ++playFrames > 7200 || playFrames % 30 != 0) return;
            var stack = Find.WindowStack;
            if (stack != null)
                foreach (var w in stack.Windows.ToList())
                    if (w.forcePause && !(w is Dialog_MessageBox && ((Dialog_MessageBox)w).buttonAText == null))
                    {
                        stack.TryRemove(w, doCloseSound: false);
                        if (closed++ < 20) Log.Message("[autoqt] closed pausing window " + w.GetType().FullName);
                    }
            if (Find.TickManager != null && Find.TickManager.CurTimeSpeed != TimeSpeed.Superfast)
                Find.TickManager.CurTimeSpeed = TimeSpeed.Superfast;
        }

        static void TickPostfix()
        {
            ticks++;
            if (ticks == 600 || ticks == 2500 || ticks == 15000 || ticks == 60000)
                Log.Message("[autoqt] " + ticks + " ticks, colonists=" + (Find.CurrentMap?.mapPawns?.FreeColonistsCount ?? -1));
        }
    }
}
