// HarmonyStress: diagnostic RimWorld mod. From its Mod constructor (where RimWorld's CreateModClasses
// runs every mod's Harmony.PatchAll) it applies no-op Harmony patches to a fixed, sorted sample of
// game methods, optionally unpatching and repatching for more rounds, and logs every failure with its
// innermost exception. Under Madeira, heavily modded loads fail at random patches with impossible
// errors (IndexOutOfRange in MonoMod's DMD emitter, Array.Copy bounds, OutOfMemory for tiny arrays)
// and wild jumps; this reproduces that without third-party mods. A failure that repeats on the same
// method every run is a real Harmony incompatibility; one that moves around is corruption.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace HarmonyStress
{
    public class StressMod : Verse.Mod
    {
        public StressMod(Verse.ModContentPack content) : base(content)
        {
            Stress.Run(content.RootDir);
        }
    }

    public static class Stress
    {
        static void L(string s) { Debug.Log("[harmonystress] " + s); }

        static Dictionary<string, string> Settings(string root)
        {
            var d = new Dictionary<string, string> { { "count", "400" }, { "rounds", "1" }, { "kinds", "prefix,postfix" } };
            var p = Path.Combine(root, "stress.txt");
            if (File.Exists(p))
                foreach (var line in File.ReadAllLines(p))
                {
                    var i = line.IndexOf('=');
                    if (i > 0) d[line.Substring(0, i).Trim()] = line.Substring(i + 1).Trim();
                }
            return d;
        }

        // Plain game methods with IL bodies: no generics, no byref-like or pointer signatures,
        // no compiler-generated or extern members. Sorted, so every run patches the same list.
        static List<MethodInfo> Candidates()
        {
            var asm = typeof(Verse.Log).Assembly;
            var list = new List<MethodInfo>();
            foreach (var t in asm.GetTypes())
            {
                if (t.Namespace == null || !(t.Namespace.StartsWith("Verse") || t.Namespace.StartsWith("RimWorld"))) continue;
                if (t.IsGenericTypeDefinition || t.ContainsGenericParameters || t.IsInterface || t.Name.Contains("<")) continue;
                foreach (var m in t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
                {
                    if (m.IsAbstract || m.IsGenericMethodDefinition || m.ContainsGenericParameters || m.Name.Contains("<")) continue;
                    if ((m.MethodImplementationFlags & (MethodImplAttributes.InternalCall | MethodImplAttributes.Native)) != 0) continue;
                    if (m.ReturnType.IsByRef || m.ReturnType.IsPointer) continue;
                    if (m.GetParameters().Any(p => p.ParameterType.IsPointer || p.ParameterType.IsByRef && p.ParameterType.GetElementType().IsByRefLike())) continue;
                    MethodBody body;
                    try { body = m.GetMethodBody(); } catch { continue; }
                    if (body == null || body.GetILAsByteArray().Length < 8) continue;
                    list.Add(m);
                }
            }
            return list.OrderBy(m => m.DeclaringType.FullName + "::" + m.ToString(), StringComparer.Ordinal).ToList();
        }

        static bool IsByRefLike(this Type t) => t.GetCustomAttributes(false).Any(a => a.GetType().Name == "IsByRefLikeAttribute");

        public static void Prefix() { }
        public static void Postfix() { }
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> insts) => insts;

        static string Describe(Exception e)
        {
            var inner = e;
            while (inner.InnerException != null) inner = inner.InnerException;
            var frames = (inner.StackTrace ?? "").Split('\n').Select(s => s.Trim()).Where(s => s.Length > 0).Take(3);
            return inner.GetType().Name + ": " + inner.Message + " @ " + string.Join(" | ", frames.ToArray());
        }

        public static void Run(string root)
        {
            var cfg = Settings(root);
            int count = int.Parse(cfg["count"]), rounds = int.Parse(cfg["rounds"]);
            var kinds = new HashSet<string>(cfg["kinds"].Split(',').Select(k => k.Trim()));
            var all = Candidates();
            // Evenly spaced, so the sample spans the whole assembly rather than one namespace.
            var targets = Enumerable.Range(0, Math.Min(count, all.Count)).Select(i => all[(int)((long)i * all.Count / Math.Min(count, all.Count))]).ToList();
            L($"start harmony={typeof(Harmony).Assembly.GetName().Version} candidates={all.Count} targets={targets.Count} rounds={rounds} kinds={string.Join(",", kinds.ToArray())}");

            var pre = kinds.Contains("prefix") ? new HarmonyMethod(typeof(Stress), nameof(Prefix)) : null;
            var post = kinds.Contains("postfix") ? new HarmonyMethod(typeof(Stress), nameof(Postfix)) : null;
            var trans = kinds.Contains("transpiler") ? new HarmonyMethod(typeof(Stress), nameof(Transpiler)) : null;
            var h = new Harmony("local.harmonystress");
            int totalFail = 0;
            for (int r = 1; r <= rounds; r++)
            {
                int ok = 0, fail = 0;
                var t0 = DateTime.UtcNow;
                for (int i = 0; i < targets.Count; i++)
                {
                    var m = targets[i];
                    try { h.Patch(m, prefix: pre, postfix: post, transpiler: trans); ok++; }
                    catch (Exception e)
                    {
                        fail++;
                        L($"FAIL r{r} #{i} {m.DeclaringType.FullName}::{m.Name} -> {Describe(e)}");
                    }
                    if ((i + 1) % 100 == 0) L($"progress r{r} {i + 1}/{targets.Count} ok={ok} fail={fail}");
                }
                L($"round {r} done ok={ok} fail={fail} in {(DateTime.UtcNow - t0).TotalSeconds:F1}s");
                totalFail += fail;
                if (r < rounds)
                {
                    try { h.UnpatchAll("local.harmonystress"); L($"round {r} unpatched"); }
                    catch (Exception e) { L($"UNPATCH FAIL r{r} -> {Describe(e)}"); }
                }
            }
            L($"DONE totalFail={totalFail}");
        }
    }
}
