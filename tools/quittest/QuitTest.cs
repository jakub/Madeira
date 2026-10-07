// QuitTest: 300 main-menu frames after the menu first draws, quits through Root.Shutdown(), the
// same call as the menu's Quit button, and logs it. A harness then checks the process exits.
using HarmonyLib;
using Verse;

namespace QuitTest
{
    public class QuitTestMod : Mod
    {
        static int frames;

        public QuitTestMod(ModContentPack content) : base(content)
        {
            new Harmony("local.quittest").Patch(AccessTools.Method(typeof(RimWorld.MainMenuDrawer), "MainMenuOnGUI"),
                postfix: new HarmonyMethod(typeof(QuitTestMod), nameof(Postfix)));
        }

        static void Postfix()
        {
            if (++frames == 1) Log.Message("[quittest] main menu reached");
            if (frames == 300)
            {
                Log.Message("[quittest] quitting via Root.Shutdown()");
                Root.Shutdown();
            }
        }
    }
}
