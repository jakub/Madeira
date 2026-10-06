// TextProbe: diagnostic RimWorld mod. Logs translation, font and text-layout state to Player.log
// to find why no text renders under Madeira, and per-click mouse/window geometry to find why taps
// land off target. Read-only: changes no game state.
using System;
using System.Collections;
using System.Reflection;
using System.Runtime.InteropServices;
using UnityEngine;

namespace TextProbe
{
    [Verse.StaticConstructorOnStartup]
    public static class Probe
    {
        const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;

        static void L(string s) { Debug.Log("[textprobe] " + s); }

        static void Try(string what, Action a)
        {
            try { a(); }
            catch (Exception e) { L(what + " THREW " + e.GetType().Name + ": " + e.Message + " @ " + (e.StackTrace ?? "").Replace('\n', '|')); }
        }

        static Type T(string name) { return typeof(Verse.Log).Assembly.GetType(name); }

        static string Show(string s) { return s == null ? "<null>" : "\"" + s + "\" len=" + s.Length; }

        static Probe()
        {
            L("start");

            Try("language", () =>
            {
                var db = T("Verse.LanguageDatabase");
                object active = db.GetField("activeLanguage", All)?.GetValue(null);
                L("activeLanguage=" + (active == null ? "<null>" : active.GetType().Name));
                if (active != null)
                {
                    foreach (var f in new[] { "folderName", "info", "loadErrors", "keyedReplacements", "dataIsLoaded" })
                    {
                        var fi = active.GetType().GetField(f, All);
                        if (fi == null) continue;
                        object v = fi.GetValue(active);
                        string extra = v is ICollection c ? " count=" + c.Count : "";
                        L("  activeLanguage." + f + "=" + (v ?? "<null>") + extra);
                    }
                    var fn = active.GetType().GetProperty("FriendlyNameNative", All);
                    if (fn != null) L("  FriendlyNameNative=" + fn.GetValue(active, null));
                }
                object def = db.GetField("defaultLanguage", All)?.GetValue(null);
                L("defaultLanguage=" + (def == null ? "<null>" : def.GetType().Name));
            });

            Try("translate", () =>
            {
                var tr = T("Verse.Translator");
                foreach (var key in new[] { "NewColony", "LoadGame", "OK", "Cancel" })
                {
                    MethodInfo can = tr.GetMethod("CanTranslate", All, null, new[] { typeof(string) }, null);
                    MethodInfo t = tr.GetMethod("Translate", All, null, new[] { typeof(string) }, null);
                    object can_v = can?.Invoke(null, new object[] { key });
                    object res = t?.Invoke(null, new object[] { key });
                    L("Translate(" + key + ") can=" + can_v + " -> " + Show(res?.ToString()));
                }
            });

            Try("text", () =>
            {
                var text = T("Verse.Text");
                L("Text.Font=" + text.GetProperty("Font", All)?.GetValue(null, null));
                var styles = text.GetField("fontStyles", All)?.GetValue(null) as GUIStyle[];
                L("Text.fontStyles=" + (styles == null ? "<null>" : styles.Length.ToString()));
                if (styles != null)
                    for (int i = 0; i < styles.Length; i++) ProbeStyle("fontStyles[" + i + "]", styles[i]);
                var calc = text.GetMethod("CalcSize", All, null, new[] { typeof(string) }, null);
                if (calc != null) L("Text.CalcSize(\"New colony\")=" + calc.Invoke(null, new object[] { "New colony" }));
            });

            Try("guiskin", () =>
            {
                L("GUI.skin.font=" + (GUI.skin != null && GUI.skin.font != null ? GUI.skin.font.name : "<null>"));
            });

            Try("mouseprobe", () =>
            {
                var go = new GameObject("TextProbe.MouseProbe");
                UnityEngine.Object.DontDestroyOnLoad(go);
                go.AddComponent<MouseProbe>();
            });

            L("end");
        }

        static void ProbeStyle(string label, GUIStyle st)
        {
            Try(label, () =>
            {
                if (st == null) { L(label + " <null>"); return; }
                Font f = st.font;
                L(label + " style.fontSize=" + st.fontSize + " font=" + (f == null ? "<null>" : f.name + " dynamic=" + f.dynamic + " fontSize=" + f.fontSize + " lineHeight=" + f.lineHeight));
                if (f == null) return;
                L(label + "   fontNames=" + (f.fontNames == null ? "<null>" : string.Join(",", f.fontNames)));
                var tex = f.material != null ? f.material.mainTexture : null;
                L(label + "   material=" + (f.material == null ? "<null>" : f.material.name) + " tex=" + (tex == null ? "<null>" : tex.width + "x" + tex.height));
                int size = st.fontSize > 0 ? st.fontSize : f.fontSize;
                f.RequestCharactersInTexture("New colony", size, st.fontStyle);
                CharacterInfo ci;
                bool got = f.GetCharacterInfo('N', out ci, size, st.fontStyle);
                L(label + "   GetCharacterInfo('N', " + size + ")=" + got + " advance=" + ci.advance + " glyph=" + ci.glyphWidth + "x" + ci.glyphHeight + " min=(" + ci.minX + "," + ci.minY + ") uvBL=" + ci.uvBottomLeft + " uvTR=" + ci.uvTopRight);
                var content = new GUIContent("New colony");
                L(label + "   style.CalcSize=" + st.CalcSize(content) + " CalcHeight(300)=" + st.CalcHeight(content, 300f));

                var gen = new TextGenerator();
                var s = new TextGenerationSettings
                {
                    font = f, fontSize = size, fontStyle = st.fontStyle, color = Color.white,
                    lineSpacing = 1f, richText = true, scaleFactor = 1f, textAnchor = TextAnchor.UpperLeft,
                    generationExtents = new Vector2(400f, 100f), pivot = Vector2.zero,
                    horizontalOverflow = HorizontalWrapMode.Wrap, verticalOverflow = VerticalWrapMode.Overflow,
                    updateBounds = true, generateOutOfBounds = true
                };
                bool ok = gen.Populate("New colony", s);
                L(label + "   TextGenerator.Populate=" + ok + " chars=" + gen.characterCount + " visible=" + gen.characterCountVisible + " verts=" + gen.vertexCount + " lines=" + gen.lineCount + " prefW=" + gen.GetPreferredWidth("New colony", s));
            });
        }
    }

    // Logs, for each left click, Unity's view of the mouse next to user32's, so an offset between
    // where the OS cursor is drawn and where the game hit-tests can be attributed to the window's
    // client origin, Unity's Screen size or RimWorld's UI scale.
    public class MouseProbe : MonoBehaviour
    {
        [StructLayout(LayoutKind.Sequential)] struct POINT { public int x, y; }
        [StructLayout(LayoutKind.Sequential)] struct RECT { public int l, t, r, b; public override string ToString() { return "{" + l + "," + t + "," + r + "," + b + "}"; } }
        [DllImport("user32.dll")] static extern bool GetCursorPos(out POINT p);
        [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] static extern IntPtr GetActiveWindow();
        [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
        [DllImport("user32.dll")] static extern bool GetClientRect(IntPtr h, out RECT r);
        [DllImport("user32.dll")] static extern bool ClientToScreen(IntPtr h, ref POINT p);
        [DllImport("user32.dll")] static extern bool ScreenToClient(IntPtr h, ref POINT p);
        [DllImport("user32.dll", EntryPoint = "GetWindowLongW")] static extern int GetWindowLong(IntPtr h, int i);

        int clicks;

        static void L(string s) { Debug.Log("[mouseprobe] " + s); }

        void Update()
        {
            if (clicks >= 40 || !Input.GetMouseButtonDown(0)) return;
            clicks++;
            try
            {
                Vector3 m = Input.mousePosition;
                IntPtr h = GetActiveWindow();
                if (h == IntPtr.Zero) h = GetForegroundWindow();
                POINT c; GetCursorPos(out c);
                POINT cc = c; ScreenToClient(h, ref cc);
                POINT o = new POINT(); ClientToScreen(h, ref o);
                RECT wr, cr; GetWindowRect(h, out wr); GetClientRect(h, out cr);
                L("#" + clicks + " unity=(" + m.x + "," + m.y + ") gui=(" + m.x + "," + (Screen.height - m.y) + ")"
                  + " screen=" + Screen.width + "x" + Screen.height + " cur=" + Screen.currentResolution + " mode=" + Screen.fullScreenMode
                  + " | cursor=(" + c.x + "," + c.y + ") client=(" + cc.x + "," + cc.y + ") origin=(" + o.x + "," + o.y + ")"
                  + " wr=" + wr + " cr=" + cr + " hwnd=0x" + h.ToString("x")
                  + " style=0x" + GetWindowLong(h, -16).ToString("x8") + " ex=0x" + GetWindowLong(h, -20).ToString("x8")
                  + " uiScale=" + Verse.Prefs.UIScale + " uiMouse=" + Verse.UI.MousePositionOnUIInverted);
            }
            catch (Exception e) { L("#" + clicks + " THREW " + e.GetType().Name + ": " + e.Message); }
        }
    }
}
