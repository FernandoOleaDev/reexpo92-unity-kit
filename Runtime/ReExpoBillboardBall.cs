using UnityEngine;
using UnityEngine.Rendering;

namespace ReExpo92.WorldKit
{
    /// <summary>
    /// Hace que la bola mire SIEMPRE a cámara con una inclinación fija (45° en X),
    /// para que la rejilla del logo Expo se vea siempre igual de inclinada desde
    /// cualquier ángulo. Funciona en el editor ([ExecuteAlways] + hook de cámara
    /// de URP). Geometría real, así que la profundidad con zonas/edificios es
    /// correcta.
    /// </summary>
    [ExecuteAlways]
    public class ReExpoBillboardBall : MonoBehaviour
    {
        void OnEnable() { RenderPipelineManager.beginCameraRendering += OnCam; }
        void OnDisable() { RenderPipelineManager.beginCameraRendering -= OnCam; }

        void OnCam(ScriptableRenderContext ctx, Camera cam)
        {
            if (cam == null) return;
            var camT = cam.transform;
            transform.rotation = Quaternion.LookRotation(transform.position - camT.position, camT.up)
                               * Quaternion.Euler(ReExpoLabelConfig.BallRotOffset);
        }
    }
}
