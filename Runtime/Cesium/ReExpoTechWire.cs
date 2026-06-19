#if REEXPO_CESIUM
using UnityEngine;
using CesiumForUnity;

namespace ReExpo92.WorldKit.Cesium
{
    /// <summary>
    /// «Modo técnico» de los tiles de Google para grabar vídeo EN EDITOR, lo más
    /// simple y robusto posible: solo ponemos NUESTRO material vía
    /// <c>Cesium3DTileset.opaqueMaterial</c>. Cesium lo aplica a TODOS los tiles
    /// (presentes, futuros y al cambiar de LOD) → sin agujeros, sin crash, sin
    /// procesar mallas (instantáneo) y siguiendo a Cesium solo.
    ///
    /// Pinta CARAS translúcidas arcoíris con degradado por distancia (rayos-X). NO
    /// son líneas de triángulo: eso exigiría reconstruir cada malla en CPU
    /// (lento) y no hay forma rápida en Mac sin Renderer Feature.
    ///
    /// Con el modo APAGADO no hace absolutamente nada con los tiles.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(Cesium3DTileset))]
    public class ReExpoTechWire : MonoBehaviour
    {
        Cesium3DTileset _tileset;
        Material _mat;
        Material _origOpaque;
        bool _active;

        void OnEnable()
        {
            _tileset = GetComponent<Cesium3DTileset>();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.update += EditorTick;
#endif
        }

        void OnDisable()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.update -= EditorTick;
#endif
            Deactivate();
        }

#if UNITY_EDITOR
        void EditorTick()
        {
            if (this == null) { UnityEditor.EditorApplication.update -= EditorTick; return; }
            if (!Application.isPlaying) Tick();
        }
#endif
        void LateUpdate() { if (Application.isPlaying) Tick(); }

        Material Mat()
        {
            if (_mat != null) return _mat;
            var sh = Shader.Find("ReExpo92/TechWire");
            if (sh == null) return null;
            _mat = new Material(sh) { name = "ReExpo92 Wireframe (auto)" };
            return _mat;
        }

        void PushProps()
        {
            var m = Mat(); if (m == null) return;
            if (m.HasProperty("_LineColor")) m.SetColor("_LineColor", ReExpoWireConfig.LineColor);
            if (m.HasProperty("_LineWidth")) m.SetFloat("_LineWidth", Mathf.Max(0.3f, ReExpoWireConfig.LineWidth));
            if (m.HasProperty("_Rainbow")) m.SetFloat("_Rainbow", ReExpoWireConfig.Rainbow ? 1f : 0f);
            if (m.HasProperty("_FaceAlpha")) m.SetFloat("_FaceAlpha", Mathf.Clamp01(ReExpoWireConfig.FaceAlpha));
            if (m.HasProperty("_FadeNear")) m.SetFloat("_FadeNear", ReExpoWireConfig.FadeNear);
            if (m.HasProperty("_FadeFar")) m.SetFloat("_FadeFar", Mathf.Max(ReExpoWireConfig.FadeNear + 1f, ReExpoWireConfig.FadeFar));
        }

        void Tick()
        {
            if (_tileset == null) { _tileset = GetComponent<Cesium3DTileset>(); if (_tileset == null) return; }
            bool want = ReExpoWireConfig.Enabled && Mat() != null;
            if (!want) { if (_active) Deactivate(); return; } // APAGADO → no tocamos nada
            if (!_active)
            {
                _origOpaque = _tileset.opaqueMaterial;
                _tileset.opaqueMaterial = Mat(); // Cesium aplica nuestro material a TODOS los tiles
                _active = true;
            }
            PushProps();
        }

        void Deactivate()
        {
            if (!_active) return;
            _active = false;
            if (_tileset != null) _tileset.opaqueMaterial = _origOpaque; // Cesium recarga tiles normales
        }
    }
}
#endif
