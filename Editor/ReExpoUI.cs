using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace ReExpo92.WorldKit.Editor
{
    /// <summary>
    /// Piezas de UI con estética Windows 95 + Expo 92, construidas con estilos
    /// INLINE en C# (no dependen del .uss). Motivo: con el package por symlink,
    /// Unity recompila los .cs pero a veces NO reimporta los assets (.uss), así
    /// que los estilos en código son los únicos que se aplican siempre. Además
    /// los inline tienen prioridad sobre USS, así que mandan sí o sí.
    /// </summary>
    public static class ReExpoUI
    {
        // ---- paleta ----
        static readonly Color Grey = new Color(0.753f, 0.753f, 0.753f); // #c0c0c0
        static readonly Color Light = Color.white;
        static readonly Color Dark = new Color(0.502f, 0.502f, 0.502f);  // #808080
        static readonly Color Blue = new Color(0.106f, 0.165f, 0.420f);  // #1B2A6B
        static readonly Color Blue2 = new Color(0.184f, 0.267f, 0.627f); // #2f44a0
        static readonly Color Orange = new Color(1f, 0.420f, 0.208f);    // #FF6B35
        static readonly Color OrangeLt = new Color(1f, 0.710f, 0.569f);
        static readonly Color OrangeDk = new Color(0.659f, 0.227f, 0.071f);
        static readonly Color Ink = new Color(0.102f, 0.090f, 0.078f);   // casi negro
        static readonly Color Paper = new Color(0.957f, 0.918f, 0.839f); // #f4ead6
        static readonly Color Green = new Color(0.180f, 0.490f, 0.310f);
        static readonly Color Sun = new Color(0.969f, 0.659f, 0.106f);
        static readonly Color Brown = new Color(0.357f, 0.227f, 0.102f);
        static readonly Color Red = new Color(0.690f, 0.094f, 0.063f);
        static readonly Color Sub = new Color(1f, 0.851f, 0.659f);       // subtítulo header
        static readonly Color WarnBg = new Color(1f, 0.965f, 0.839f);

        // ---- helpers de estilo ----
        static void Pad(VisualElement e, float v, float h)
        {
            e.style.paddingTop = v; e.style.paddingBottom = v;
            e.style.paddingLeft = h; e.style.paddingRight = h;
        }

        static void Square(VisualElement e)
        {
            e.style.borderTopLeftRadius = 0; e.style.borderTopRightRadius = 0;
            e.style.borderBottomLeftRadius = 0; e.style.borderBottomRightRadius = 0;
        }

        /// Bisel Win95: raised = saliente (luz arriba-izq), si no = hundido.
        static void Bevel(VisualElement e, bool raised, float w = 2)
        {
            var s = e.style;
            s.borderTopWidth = w; s.borderLeftWidth = w; s.borderRightWidth = w; s.borderBottomWidth = w;
            s.borderTopColor = raised ? Light : Dark;
            s.borderLeftColor = raised ? Light : Dark;
            s.borderRightColor = raised ? Dark : Light;
            s.borderBottomColor = raised ? Dark : Light;
            Square(e);
        }

        // ---- recursos del package ----
        const string Fallback = "Packages/com.reexpo92.worldkit";
        static string _root;
        public static string Root
        {
            get
            {
                if (!string.IsNullOrEmpty(_root)) return _root;
                try { var pi = PackageInfo.FindForAssembly(typeof(ReExpoUI).Assembly); _root = pi != null ? pi.assetPath : Fallback; }
                catch { _root = Fallback; }
                return _root;
            }
        }
        public static Texture2D LoadLogo() => AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Editor/Icons/reexpo-logo.png");

        // ---- raíz ----
        public static void ApplyStyle(VisualElement root)
        {
            root.style.backgroundColor = Grey;
            root.style.flexGrow = 1;
        }

        // ---- cabecera de marca ----
        public static VisualElement Header(string subtitle)
        {
            var h = new VisualElement();
            h.style.flexDirection = FlexDirection.Row;
            h.style.alignItems = Align.Center;
            h.style.backgroundColor = Blue;
            h.style.borderBottomWidth = 3; h.style.borderBottomColor = Orange;
            Pad(h, 8, 10);

            var logo = LoadLogo();
            if (logo != null)
            {
                var img = new Image { image = logo, scaleMode = ScaleMode.ScaleToFit };
                img.style.width = 34; img.style.height = 34; img.style.marginRight = 8;
                h.Add(img);
            }
            var col = new VisualElement();
            var t = new Label("re-Expo92 · WorldKit");
            t.style.color = Light; t.style.fontSize = 14; t.style.unityFontStyleAndWeight = FontStyle.Bold;
            var s = new Label(subtitle);
            s.style.color = Sub; s.style.fontSize = 11;
            col.Add(t); col.Add(s);
            h.Add(col);
            return h;
        }

        public static VisualElement Body()
        {
            var b = new VisualElement();
            b.style.backgroundColor = Grey; b.style.flexGrow = 1; Pad(b, 10, 12);
            return b;
        }

        public static VisualElement Row()
        {
            var r = new VisualElement();
            r.style.flexDirection = FlexDirection.Row;
            return r;
        }

        public static Label SectionTitle(string text)
        {
            var l = new Label(text);
            l.style.color = Color.black; l.style.fontSize = 13; l.style.unityFontStyleAndWeight = FontStyle.Bold;
            l.style.marginTop = 10; l.style.marginBottom = 5;
            return l;
        }

        public static VisualElement Card()
        {
            var c = new VisualElement();
            c.style.backgroundColor = Paper; Bevel(c, false); Pad(c, 10, 12); c.style.marginBottom = 10;
            return c;
        }

        public static Label Para(string text)
        {
            var l = new Label(text);
            l.style.color = Ink; l.style.whiteSpace = WhiteSpace.Normal; l.style.fontSize = 12; l.style.marginBottom = 8;
            return l;
        }

        public static Label Note(string text)
        {
            var l = new Label(text);
            l.style.color = Brown; l.style.whiteSpace = WhiteSpace.Normal; l.style.fontSize = 11; l.style.marginTop = 4; l.style.marginBottom = 4;
            return l;
        }

        public static Label Warn(string text)
        {
            var l = new Label(text);
            l.style.color = Brown; l.style.backgroundColor = WarnBg; l.style.whiteSpace = WhiteSpace.Normal; l.style.fontSize = 11;
            Bevel(l, false); Pad(l, 8, 10); l.style.marginTop = 6; l.style.marginBottom = 6;
            return l;
        }

        public static VisualElement Separator()
        {
            var s = new VisualElement();
            s.style.height = 2; s.style.marginTop = 10; s.style.marginBottom = 10;
            s.style.borderTopWidth = 1; s.style.borderTopColor = Dark;
            s.style.borderBottomWidth = 1; s.style.borderBottomColor = Light;
            return s;
        }

        // ---- botones (relieve Win95) ----
        static Button MkBtn(string text, Action onClick, string glyph)
        {
            var b = new Button(onClick) { text = glyph != null ? glyph + "  " + text : text };
            b.style.unityFontStyleAndWeight = FontStyle.Bold;
            Pad(b, 5, 12); b.style.marginTop = 4; b.style.marginRight = 4;
            return b;
        }
        static void BtnBevel(Button b, Color light, Color dark)
        {
            b.style.borderTopWidth = 2; b.style.borderLeftWidth = 2; b.style.borderRightWidth = 2; b.style.borderBottomWidth = 2;
            b.style.borderTopColor = light; b.style.borderLeftColor = light;
            b.style.borderRightColor = dark; b.style.borderBottomColor = dark;
            Square(b);
        }
        public static Button Primary(string text, Action onClick, string glyph = null)
        {
            var b = MkBtn(text, onClick, glyph); b.style.backgroundColor = Orange; b.style.color = Light;
            BtnBevel(b, OrangeLt, OrangeDk); return b;
        }
        public static Button Secondary(string text, Action onClick, string glyph = null)
        {
            var b = MkBtn(text, onClick, glyph); b.style.backgroundColor = Grey; b.style.color = Color.black;
            BtnBevel(b, Light, Dark); return b;
        }
        public static Button Ghost(string text, Action onClick, string glyph = null)
        {
            var b = MkBtn(text, onClick, glyph); b.style.backgroundColor = Grey; b.style.color = Blue2;
            BtnBevel(b, Light, Dark); return b;
        }

        // ---- campos de texto y toggles (legibles) ----
        public static TextField Field(string label, bool password = false)
        {
            var f = new TextField(label) { isPasswordField = password };
            f.style.marginBottom = 4;
            f.style.color = Ink; // hereda al label y al texto escrito
            var lbl = f.Q<Label>(); if (lbl != null) { lbl.style.color = Ink; lbl.style.minWidth = 84; lbl.style.fontSize = 12; }
            foreach (var cn in new[] { "unity-base-text-field__input", "unity-text-field__input" })
            {
                var input = f.Q(className: cn);
                if (input != null) { input.style.backgroundColor = Light; input.style.color = Color.black; }
            }
            return f;
        }

        public static Toggle Toggle(string label, bool value)
        {
            var t = new Toggle(label) { value = value };
            t.style.marginTop = 2; t.style.marginBottom = 2;
            var lbl = t.Q<Label>(); if (lbl != null) { lbl.style.color = Ink; lbl.style.fontSize = 12; }
            return t;
        }

        // ---- estado con punto ----
        public static VisualElement StatusBar(string state, string text)
        {
            var bar = Row(); bar.style.alignItems = Align.Center; bar.style.marginBottom = 8;
            var dot = new VisualElement();
            dot.style.width = 10; dot.style.height = 10; dot.style.marginRight = 7;
            dot.style.backgroundColor = state == "on" ? Green : state == "warn" ? Sun : Red;
            dot.style.borderTopWidth = 1; dot.style.borderLeftWidth = 1; dot.style.borderRightWidth = 1; dot.style.borderBottomWidth = 1;
            dot.style.borderTopColor = dot.style.borderLeftColor = dot.style.borderRightColor = dot.style.borderBottomColor = new Color(0.25f, 0.25f, 0.25f);
            var l = new Label(text); l.style.color = Ink; l.style.fontSize = 12; l.style.whiteSpace = WhiteSpace.Normal; l.style.flexShrink = 1;
            bar.Add(dot); bar.Add(l);
            return bar;
        }

        public static VisualElement Check(bool ok, string text)
        {
            var r = Row(); r.style.alignItems = Align.FlexStart; r.style.marginBottom = 5;
            var mark = new Label(ok ? "✓" : "✗");
            mark.style.color = ok ? Green : Red; mark.style.unityFontStyleAndWeight = FontStyle.Bold; mark.style.marginRight = 6;
            var tx = new Label(text); tx.style.color = Ink; tx.style.whiteSpace = WhiteSpace.Normal; tx.style.flexGrow = 1; tx.style.fontSize = 12;
            r.Add(mark); r.Add(tx);
            return r;
        }

        // ---- pasos del asistente (cuadritos con relieve) ----
        public static Label StepDot(int n, bool active, bool done)
        {
            var d = new Label(n.ToString());
            d.style.width = 22; d.style.height = 22; d.style.marginRight = 5;
            d.style.unityTextAlign = TextAnchor.MiddleCenter; d.style.fontSize = 11; d.style.unityFontStyleAndWeight = FontStyle.Bold;
            Bevel(d, true);
            if (done) { d.style.backgroundColor = Green; d.style.color = Light; }
            else if (active) { d.style.backgroundColor = Blue; d.style.color = Light; }
            else { d.style.backgroundColor = Grey; d.style.color = new Color(0.25f, 0.25f, 0.25f); }
            return d;
        }

        public static Label StepTitle(string text)
        {
            var l = new Label(text);
            l.style.color = Blue2; l.style.unityFontStyleAndWeight = FontStyle.Bold; l.style.fontSize = 13; l.style.marginBottom = 8;
            return l;
        }

        // ---- feedback ----
        public static Label Feedback()
        {
            var l = new Label(string.Empty);
            l.style.whiteSpace = WhiteSpace.Normal; l.style.fontSize = 12; l.style.marginTop = 8; l.style.color = Ink;
            return l;
        }
        public static void SetFeedback(Label l, string kind, string text)
        {
            if (l == null) return;
            l.text = text;
            l.style.color = kind == "ok" ? Green : kind == "err" ? Red : kind == "busy" ? Blue2 : Ink;
        }
    }
}
