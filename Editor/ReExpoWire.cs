using UnityEditor;
using UnityEngine;

namespace ReExpo92.WorldKit.Editor
{
    /// <summary>
    /// Puente entre los ajustes guardados (EditorPrefs) y la config viva del
    /// wireframe técnico (<see cref="ReExpoWireConfig"/>), que lee el componente
    /// <c>ReExpoTechWire</c> del tileset. Al tocar un ajuste, vuelca y repinta.
    /// </summary>
    [InitializeOnLoad]
    static class ReExpoWire
    {
        static ReExpoWire() { Push(); }

        public static bool Enabled
        {
            get => EditorPrefs.GetBool("ReExpo92.WireOn", false);
            set { EditorPrefs.SetBool("ReExpo92.WireOn", value); Push(); }
        }

        public static bool Rainbow
        {
            get => EditorPrefs.GetBool("ReExpo92.WireRainbow", true);
            set { EditorPrefs.SetBool("ReExpo92.WireRainbow", value); Push(); }
        }

        public static float LineWidth
        {
            get => EditorPrefs.GetFloat("ReExpo92.WireWidth", 1.4f);
            set { EditorPrefs.SetFloat("ReExpo92.WireWidth", Mathf.Clamp(value, 0.3f, 8f)); Push(); }
        }

        public static Color LineColor
        {
            get => new Color(
                EditorPrefs.GetFloat("ReExpo92.WireR", 0.4f),
                EditorPrefs.GetFloat("ReExpo92.WireG", 0.9f),
                EditorPrefs.GetFloat("ReExpo92.WireB", 1f),
                EditorPrefs.GetFloat("ReExpo92.WireA", 1f));
            set
            {
                EditorPrefs.SetFloat("ReExpo92.WireR", value.r);
                EditorPrefs.SetFloat("ReExpo92.WireG", value.g);
                EditorPrefs.SetFloat("ReExpo92.WireB", value.b);
                EditorPrefs.SetFloat("ReExpo92.WireA", value.a);
                Push();
            }
        }

        public static float FaceAlpha
        {
            get => EditorPrefs.GetFloat("ReExpo92.WireFace", 0f);
            set { EditorPrefs.SetFloat("ReExpo92.WireFace", Mathf.Clamp01(value)); Push(); }
        }

        public static float FadeNear
        {
            get => EditorPrefs.GetFloat("ReExpo92.WireNear", 40f);
            set { EditorPrefs.SetFloat("ReExpo92.WireNear", Mathf.Max(0f, value)); Push(); }
        }

        public static float FadeFar
        {
            get => EditorPrefs.GetFloat("ReExpo92.WireFar", 700f);
            set { EditorPrefs.SetFloat("ReExpo92.WireFar", Mathf.Max(FadeNear + 1f, value)); Push(); }
        }

        static void Push()
        {
            ReExpoWireConfig.Enabled = Enabled;
            ReExpoWireConfig.Rainbow = Rainbow;
            ReExpoWireConfig.LineWidth = LineWidth;
            ReExpoWireConfig.LineColor = LineColor;
            ReExpoWireConfig.FaceAlpha = FaceAlpha;
            ReExpoWireConfig.FadeNear = FadeNear;
            ReExpoWireConfig.FadeFar = Mathf.Max(FadeNear + 1f, FadeFar);
            SceneView.RepaintAll();
            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        }
    }
}
