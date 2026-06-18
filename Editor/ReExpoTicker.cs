using UnityEditor;
using UnityEngine;

namespace ReExpo92.WorldKit.Editor
{
    /// <summary>
    /// Puente entre los ajustes guardados (EditorPrefs) y la config viva del
    /// cartel LED de zona (<see cref="ReExpoTickerConfig"/>), que leen cada frame
    /// los <c>ReExpoZoneTicker</c>. Al cargar el editor y al tocar un ajuste,
    /// vuelca los valores y repinta la Scene (se ve al momento).
    /// </summary>
    [InitializeOnLoad]
    static class ReExpoTicker
    {
        static ReExpoTicker() { Push(); }

        public static bool Enabled
        {
            get => EditorPrefs.GetBool("ReExpo92.TickerOn", true);
            set { EditorPrefs.SetBool("ReExpo92.TickerOn", value); Push(); }
        }

        public static float LetterMeters
        {
            get => EditorPrefs.GetFloat("ReExpo92.TickerLetter", 50f);
            set { EditorPrefs.SetFloat("ReExpo92.TickerLetter", Mathf.Clamp(value, 1f, 300f)); Push(); }
        }

        public static float BandMeters
        {
            get => EditorPrefs.GetFloat("ReExpo92.TickerBand", 16.7f);
            set { EditorPrefs.SetFloat("ReExpo92.TickerBand", Mathf.Clamp(value, 0f, 400f)); Push(); }
        }

        public static float Speed
        {
            get => EditorPrefs.GetFloat("ReExpo92.TickerSpeed", 4f);
            set { EditorPrefs.SetFloat("ReExpo92.TickerSpeed", Mathf.Clamp(value, 0f, 120f)); Push(); }
        }

        public static float Intensity
        {
            get => EditorPrefs.GetFloat("ReExpo92.TickerIntensity", 2.1f);
            set { EditorPrefs.SetFloat("ReExpo92.TickerIntensity", Mathf.Clamp(value, 0f, 10f)); Push(); }
        }

        /// Vuelca los ajustes a la config viva y repinta. Todos se aplican EN VIVO
        /// (el ticker detecta los cambios cada frame y se reconstruye solo).
        static void Push()
        {
            ReExpoTickerConfig.Enabled = Enabled;
            ReExpoTickerConfig.LetterMeters = LetterMeters;
            ReExpoTickerConfig.BandMeters = BandMeters;
            ReExpoTickerConfig.SpeedMetersPerSec = Speed;
            ReExpoTickerConfig.Intensity = Intensity;
            SceneView.RepaintAll();
        }
    }
}
