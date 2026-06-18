using TMPro;
using UnityEditor;
using UnityEngine;

namespace ReExpo92.WorldKit.Editor
{
    /// <summary>
    /// Puente entre los ajustes guardados (EditorPrefs) y la config viva que leen
    /// los billboards (<see cref="ReExpoLabelConfig"/>). Al cargar el editor y al
    /// tocar cualquier ajuste, vuelca los valores a la config y repinta la Scene.
    /// Los carteles ahora son TextMeshPro reales (no overlay), así que tienen
    /// profundidad correcta con zonas/edificios.
    /// </summary>
    [InitializeOnLoad]
    static class ReExpoLabels
    {
        static ReExpoLabels() { Push(); }

        public static bool Enabled
        {
            get => EditorPrefs.GetBool("ReExpo92.LabelsOn", true);
            set { EditorPrefs.SetBool("ReExpo92.LabelsOn", value); Push(); }
        }

        public static Vector3 BallRotOffset
        {
            get => GetVec("ReExpo92.BallRot", new Vector3(50f, -145f, 0f));
            set { SetVec("ReExpo92.BallRot", value); Push(); }
        }

        public static Vector3 LabelRotOffset
        {
            get => GetVec("ReExpo92.LabelRot", Vector3.zero);
            set { SetVec("ReExpo92.LabelRot", value); Push(); }
        }

        public static Vector3 LabelPosOffset
        {
            get => GetVec("ReExpo92.LabelPos", new Vector3(5f, 0f, 0f));
            set { SetVec("ReExpo92.LabelPos", value); Push(); }
        }

        public static float LabelMaxSize
        {
            get => EditorPrefs.GetFloat("ReExpo92.LabelMax", 0.83f);
            set { EditorPrefs.SetFloat("ReExpo92.LabelMax", Mathf.Max(0.01f, value)); Push(); }
        }

        public static float LabelFontSize
        {
            get => EditorPrefs.GetFloat("ReExpo92.LabelFont", 36f);
            set { EditorPrefs.SetFloat("ReExpo92.LabelFont", Mathf.Max(1f, value)); Push(); ApplyFontSize(); }
        }

        public static float NearRadius
        {
            get => EditorPrefs.GetFloat("ReExpo92.LabelNear", 200f);
            set { EditorPrefs.SetFloat("ReExpo92.LabelNear", Mathf.Max(1f, value)); Push(); }
        }

        public static float FarRadius
        {
            get => EditorPrefs.GetFloat("ReExpo92.LabelFar", 450f);
            // el lejano SIEMPRE mayor que el cercano
            set { EditorPrefs.SetFloat("ReExpo92.LabelFar", Mathf.Max(NearRadius + 1f, value)); Push(); }
        }

        /// Aplica el tamaño de fuente a los carteles ya creados (vivo) y reajusta su fondo.
        static void ApplyFontSize()
        {
            var labels = Object.FindObjectsByType<ReExpoBillboardLabel>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var l in labels)
            {
                var tmp = l.GetComponent<TextMeshPro>();
                if (tmp == null) continue;
                tmp.fontSize = LabelFontSize;
                tmp.ForceMeshUpdate();
                var bg = l.transform.Find("Bg");
                if (bg != null)
                {
                    var b = tmp.textBounds;
                    bg.localPosition = new Vector3(b.center.x, b.center.y, 0.5f);
                    bg.localScale = new Vector3(b.size.x + 4f, b.size.y + 2f, 1f);
                }
            }
            SceneView.RepaintAll();
        }

        /// Vuelca los ajustes a la config viva y repinta.
        static void Push()
        {
            ReExpoLabelConfig.Enabled = Enabled;
            ReExpoLabelConfig.BallRotOffset = BallRotOffset;
            ReExpoLabelConfig.LabelRotOffset = LabelRotOffset;
            ReExpoLabelConfig.LabelPosOffset = LabelPosOffset;
            ReExpoLabelConfig.LabelMaxSize = LabelMaxSize;
            ReExpoLabelConfig.LabelFontSize = LabelFontSize;
            ReExpoLabelConfig.NearRadius = NearRadius;
            ReExpoLabelConfig.FarRadius = Mathf.Max(NearRadius + 1f, FarRadius);
            SceneView.RepaintAll();
        }

        static Vector3 GetVec(string key, Vector3 d) => new Vector3(
            EditorPrefs.GetFloat(key + ".x", d.x),
            EditorPrefs.GetFloat(key + ".y", d.y),
            EditorPrefs.GetFloat(key + ".z", d.z));

        static void SetVec(string key, Vector3 v)
        {
            EditorPrefs.SetFloat(key + ".x", v.x);
            EditorPrefs.SetFloat(key + ".y", v.y);
            EditorPrefs.SetFloat(key + ".z", v.z);
        }
    }
}
