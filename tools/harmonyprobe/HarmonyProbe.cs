// HarmonyProbe: diagnostic RimWorld mod. Applies Harmony patches of each kind to methods the game
// has already JIT-compiled and run (a postfix on Text.CalcSize, a prefix on the main menu's OnGUI,
// an identity transpiler plus postfix on Text.CalcHeight) and logs whether each applied and whether
// the patched code runs. Harmony rewrites methods at runtime by writing jumps into JIT'd code, the
// riskiest thing a mod does under Madeira (W^X code memory through FEX). Changes no game state.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace HarmonyProbe
{
    [Verse.StaticConstructorOnStartup]
    public static class Probe
    {
        static int calcSizeHits, calcHeightHits, menuHits, transpilerRuns;

        static void L(string s) { Debug.Log("[harmonyprobe] " + s); }

        static void Try(string what, Action a)
        {
            try { a(); L(what + " ok"); }
            catch (Exception e) { L(what + " THREW " + e.GetType().Name + ": " + e.Message + " @ " + (e.StackTrace ?? "").Replace('\n', '|')); }
        }

        static Probe()
        {
            L("start harmony=" + typeof(Harmony).Assembly.GetName().Version);
            var h = new Harmony("local.harmonyprobe");
            Try("postfix Text.CalcSize", () => h.Patch(AccessTools.Method(typeof(Verse.Text), "CalcSize", new[] { typeof(string) }),
                postfix: new HarmonyMethod(typeof(Probe), nameof(CalcSizePostfix))));
            Try("prefix MainMenuDrawer.MainMenuOnGUI", () => h.Patch(AccessTools.Method(typeof(RimWorld.MainMenuDrawer), "MainMenuOnGUI"),
                prefix: new HarmonyMethod(typeof(Probe), nameof(MenuPrefix))));
            Try("transpiler+postfix Text.CalcHeight", () => h.Patch(AccessTools.Method(typeof(Verse.Text), "CalcHeight", new[] { typeof(string), typeof(float) }),
                postfix: new HarmonyMethod(typeof(Probe), nameof(CalcHeightPostfix)),
                transpiler: new HarmonyMethod(typeof(Probe), nameof(Identity))));
            L("patched=" + string.Join(",", h.GetPatchedMethods().Select(m => m.DeclaringType.Name + "." + m.Name).ToArray()) + " transpilerRuns=" + transpilerRuns);

            // The patched methods called directly, right now: the patch must already be live.
            Try("direct calls", () =>
            {
                int s0 = calcSizeHits, h0 = calcHeightHits;
                Vector2 size = Verse.Text.CalcSize("Harmony probe");
                float height = Verse.Text.CalcHeight("Harmony probe", 300f);
                L("direct CalcSize=" + size + " postfixHits+" + (calcSizeHits - s0) + " CalcHeight=" + height + " postfixHits+" + (calcHeightHits - h0));
            });
        }

        static void CalcSizePostfix() { calcSizeHits++; }
        static void CalcHeightPostfix() { calcHeightHits++; }

        static void MenuPrefix()
        {
            menuHits++;
            if (menuHits == 1 || menuHits == 600)
                L("MainMenuOnGUI prefix hits=" + menuHits + " CalcSize postfix hits=" + calcSizeHits + " CalcHeight postfix hits=" + calcHeightHits);
        }

        static IEnumerable<CodeInstruction> Identity(IEnumerable<CodeInstruction> instructions)
        {
            transpilerRuns++;
            return instructions;
        }
    }
}
