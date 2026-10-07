// NoSteamNag: drops RimWorld's "Could not initialize Steam API" message box (key SteamClientMissing).
// Without the Steam client RimWorld shows it at every start; Ignore only closes it.
using HarmonyLib;
using Verse;

namespace NoSteamNag
{
    public class NoSteamNagMod : Mod
    {
        public NoSteamNagMod(ModContentPack content) : base(content)
        {
            new Harmony("local.nosteamnag").Patch(AccessTools.Method(typeof(WindowStack), nameof(WindowStack.Add)),
                prefix: new HarmonyMethod(typeof(NoSteamNagMod), nameof(Prefix)));
        }

        static bool Prefix(Window window)
        {
            if (window is Dialog_MessageBox box && box.text.RawText == "SteamClientMissing".Translate().RawText)
            {
                Log.Message("[nosteamnag] suppressed the Steam client dialog");
                return false;
            }
            return true;
        }
    }
}
