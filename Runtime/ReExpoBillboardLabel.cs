using UnityEngine;
using UnityEngine.Rendering;

namespace ReExpo92.WorldKit
{
    /// <summary>
    /// Cartel 3D (TextMeshPro) que mira a cámara, se coloca respecto a la bola
    /// con offsets de posición/rotación (en ejes de cámara) y escala con la
    /// distancia según los radios: fuera del lejano desaparece, dentro del
    /// cercano tamaño máximo. Lee todo de <see cref="ReExpoLabelConfig"/> (vivo).
    /// Al ser geometría real, las zonas/edificios delante lo ocultan bien.
    /// </summary>
    [ExecuteAlways]
    public class ReExpoBillboardLabel : MonoBehaviour
    {
        /// Centro de la bola en local del POI (ver BuildPin: y = 15.5).
        public Vector3 ballCenterLocal = new Vector3(0f, 15.5f, 0f);

        Renderer[] _renderers;

        void OnEnable() { _renderers = GetComponentsInChildren<Renderer>(true); RenderPipelineManager.beginCameraRendering += OnCam; }
        void OnDisable() { RenderPipelineManager.beginCameraRendering -= OnCam; }

        void SetVisible(bool v)
        {
            if (_renderers == null) _renderers = GetComponentsInChildren<Renderer>(true);
            foreach (var r in _renderers) if (r != null) r.enabled = v;
        }

        void OnCam(ScriptableRenderContext ctx, Camera cam)
        {
            if (cam == null || transform.parent == null) return;
            // solo Scene view y cámara de juego (evita previews = parpadeo)
            if (cam.cameraType != CameraType.SceneView && cam.cameraType != CameraType.Game) return;

            var camT = cam.transform;
            Vector3 center = transform.parent.TransformPoint(ballCenterLocal);
            float d = Vector3.Distance(camT.position, center);
            float t = Mathf.InverseLerp(ReExpoLabelConfig.FarRadius, ReExpoLabelConfig.NearRadius, d);

            bool visible = ReExpoLabelConfig.Enabled && t > 0f;
            SetVisible(visible);
            if (!visible) return;

            float scale = ReExpoLabelConfig.LabelMaxSize * Mathf.SmoothStep(0f, 1f, t);
            Vector3 o = ReExpoLabelConfig.LabelPosOffset;
            transform.position = center + camT.right * o.x + camT.up * o.y + camT.forward * o.z;
            transform.rotation = Quaternion.LookRotation(transform.position - camT.position, camT.up)
                               * Quaternion.Euler(ReExpoLabelConfig.LabelRotOffset);
            transform.localScale = Vector3.one * Mathf.Max(0.0001f, scale);
        }
    }
}
