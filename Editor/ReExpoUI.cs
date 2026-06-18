using System;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEngine;
using UnityEngine.UIElements;

namespace ReExpo92.WorldKit.Editor
{
    /// <summary>
    /// Utilidades de UI compartidas por las herramientas de editor: resuelve las
    /// rutas del package (instalado o embebido), carga el logo y la hoja de
    /// estilos, y construye piezas de marca reutilizables (cabecera, botones,
    /// pastilla de estado). Mantiene un look profesional y coherente con la
    /// estética Expo 92.
    /// </summary>
    public static class ReExpoUI
    {
        const string Fallback = "Packages/com.reexpo92.worldkit";
        static string _root;

        /// Ruta raíz del package (funciona instalado o embebido en Assets).
        public static string Root
        {
            get
            {
                if (!string.IsNullOrEmpty(_root)) return _root;
                try
                {
                    var pi = PackageInfo.FindForAssembly(typeof(ReExpoUI).Assembly);
                    _root = pi != null ? pi.assetPath : Fallback;
                }
                catch { _root = Fallback; }
                return _root;
            }
        }

        public static StyleSheet LoadStyle() =>
            AssetDatabase.LoadAssetAtPath<StyleSheet>(Root + "/Editor/Styles/ReExpo.uss");

        public static Texture2D LoadLogo() =>
            AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Editor/Icons/reexpo-logo.png");

        /// Icono integrado del editor, tolerante a nombres ausentes.
        public static Texture2D Builtin(string name)
        {
            try { return EditorGUIUtility.IconContent(name)?.image as Texture2D; }
            catch { return null; }
        }

        /// Aplica la hoja de estilos a la raíz de una ventana.
        public static void ApplyStyle(VisualElement root)
        {
            var ss = LoadStyle();
            if (ss != null && !root.styleSheets.Contains(ss)) root.styleSheets.Add(ss);
            root.AddToClassList("rx-root");
        }

        /// Cabecera de marca: logo + título + subtítulo.
        public static VisualElement Header(string subtitle)
        {
            var h = new VisualElement();
            h.AddToClassList("rx-header");

            var logo = LoadLogo();
            if (logo != null)
            {
                var img = new Image { image = logo, scaleMode = ScaleMode.ScaleToFit };
                img.AddToClassList("rx-logo");
                h.Add(img);
            }

            var col = new VisualElement();
            var t = new Label("re-Expo92 · WorldKit");
            t.AddToClassList("rx-header-title");
            var s = new Label(subtitle);
            s.AddToClassList("rx-header-sub");
            col.Add(t);
            col.Add(s);
            h.Add(col);
            return h;
        }

        public static Label SectionTitle(string text)
        {
            var l = new Label(text);
            l.AddToClassList("rx-section-title");
            return l;
        }

        public static VisualElement Card()
        {
            var c = new VisualElement();
            c.AddToClassList("rx-card");
            return c;
        }

        /// Botón primario (naranja), con glifo opcional.
        public static Button Primary(string text, Action onClick, string glyph = null)
        {
            var b = new Button(onClick) { text = glyph != null ? glyph + "  " + text : text };
            b.AddToClassList("rx-btn");
            b.AddToClassList("rx-btn--primary");
            return b;
        }

        public static Button Secondary(string text, Action onClick, string glyph = null)
        {
            var b = new Button(onClick) { text = glyph != null ? glyph + "  " + text : text };
            b.AddToClassList("rx-btn");
            b.AddToClassList("rx-btn--secondary");
            return b;
        }

        public static Button Ghost(string text, Action onClick, string glyph = null)
        {
            var b = new Button(onClick) { text = glyph != null ? glyph + "  " + text : text };
            b.AddToClassList("rx-btn");
            b.AddToClassList("rx-btn--ghost");
            return b;
        }

        /// Pastilla de estado con punto de color. state: "on" | "off" | "warn".
        public static VisualElement StatusBar(string state, string text)
        {
            var bar = new VisualElement();
            bar.AddToClassList("rx-statusbar");
            var dot = new VisualElement();
            dot.AddToClassList("rx-dot");
            dot.AddToClassList(state == "on" ? "rx-dot--on" : state == "warn" ? "rx-dot--warn" : "rx-dot--off");
            var l = new Label(text);
            l.AddToClassList("rx-status-text");
            bar.Add(dot);
            bar.Add(l);
            return bar;
        }

        public static Label Note(string text)
        {
            var l = new Label(text);
            l.AddToClassList("rx-note");
            return l;
        }

        public static Label Para(string text)
        {
            var l = new Label(text);
            l.AddToClassList("rx-para");
            return l;
        }

        public static VisualElement Separator()
        {
            var s = new VisualElement();
            s.AddToClassList("rx-sep");
            return s;
        }

        public static VisualElement Check(bool ok, string text)
        {
            var r = new VisualElement();
            r.AddToClassList("rx-check");
            var mark = new Label(ok ? "✓" : "✗");
            mark.AddToClassList(ok ? "rx-check-ok" : "rx-check-no");
            var tx = new Label(text);
            tx.AddToClassList("rx-check-tx");
            r.Add(mark);
            r.Add(tx);
            return r;
        }
    }
}
