#if REEXPO_CESIUM
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace ReExpo92.WorldKit.Cesium
{
    /// <summary>
    /// Cartel «LED» que recorre el perímetro de una zona: un TextMeshPro con el
    /// nombre repetido, cuyos caracteres se DEFORMAN sobre las paredes del
    /// polígono (cada letra se coloca y orienta a lo largo del muro) y se
    /// DESPLAZAN con el tiempo, dando vueltas sin fin. Emisivo (look LED).
    ///
    /// Técnica: se mide el texto una vez (posiciones base en espacio de texto),
    /// se calcula la longitud del perímetro y, cada frame, cada vértice de cada
    /// letra se recoloca sobre el muro a su distancia-de-arco (con un offset
    /// animado). El bucle es CONTINUO: el largo del texto se mapea exactamente a
    /// la longitud del perímetro, así el final empalma con el principio.
    ///
    /// Todo en el espacio LOCAL del propio objeto (= local de la zona), para que
    /// siga al rig si se mueve. Se reconstruye tras ajustar alturas al terreno.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(TextMeshPro))]
    public class ReExpoZoneTicker : MonoBehaviour
    {
        const string Separator = "   •   ";

        TextMeshPro _tmp;
        MeshRenderer _mr;
        string _label = "";

        // Geometría del perímetro (espacio local), cerrada.
        Vector3[] _ring;          // puntos base del muro
        Vector3[] _dir;           // dirección unitaria de cada segmento
        float[] _cum;             // longitud acumulada por vértice (n+1)
        float _perimeter;         // longitud total
        Vector3 _up = Vector3.up; // «arriba» local
        float _wallY;             // altura de la banda (m, local)
        Color _zoneColor = Color.white; // color de la zona (= color del LED)

        // Caché del texto medido una sola vez.
        struct CharVerts { public int mat, vi; public Vector3 v0, v1, v2, v3; public float midX, baseY; }
        CharVerts[] _chars;
        float _scale;             // metros por unidad de texto (mapea texto→perímetro)
        float _minX;              // borde izquierdo del texto (espacio de texto)
        bool _ready;

        // Últimos valores de config aplicados (para detectar cambios EN VIVO).
        float _lastLetter = -1f, _lastIntensity = -1f;

        // ---------------------------------------------------------------- API
        /// (Re)configura el cartel a partir de los vértices del muro de la zona.
        /// <paramref name="verts"/> son los anclajes base (a ras de suelo) y
        /// <paramref name="owner"/> el transform de la zona (= padre del cartel).
        public void Configure(IReadOnlyList<Transform> verts, Transform owner, string label, float zoneHeight, Color color)
        {
            _tmp = GetComponent<TextMeshPro>();
            _mr = GetComponent<MeshRenderer>();
            _label = string.IsNullOrEmpty(label) ? "" : label.ToUpperInvariant();
            _zoneColor = color;
            _wallY = ReExpoTickerConfig.BandMeters;

            // descarta anclajes nulos por seguridad
            var pts = new List<Vector3>();
            if (verts != null)
                foreach (var v in verts)
                    if (v != null) pts.Add(transform.InverseTransformPoint(v.position));
            int n = pts.Count;
            if (n < 3) { _ready = false; if (_mr != null) _mr.enabled = false; return; }

            // anillo en espacio LOCAL de este objeto (idéntico al de la zona)
            _ring = pts.ToArray();
            _up = transform.InverseTransformDirection(Vector3.up).normalized;

            // longitudes acumuladas (cerrado: el último segmento vuelve al 0)
            _dir = new Vector3[n];
            _cum = new float[n + 1];
            _cum[0] = 0f;
            for (int i = 0; i < n; i++)
            {
                Vector3 a = _ring[i], b = _ring[(i + 1) % n];
                Vector3 e = b - a;
                float len = e.magnitude;
                _dir[i] = len > 1e-4f ? e / len : Vector3.right;
                _cum[i + 1] = _cum[i] + len;
            }
            _perimeter = _cum[n];
            if (_perimeter < 1f) { _ready = false; if (_mr != null) _mr.enabled = false; return; }

            BuildText();
            ApplyMaterial();
            _ready = _chars != null && _chars.Length > 0;
            if (_mr != null) _mr.enabled = _ready && ReExpoTickerConfig.Enabled;
            Warp();
        }

        // ----------------------------------------------------- ciclo de vida
        void OnEnable()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying) UnityEditor.EditorApplication.update += EditorTick;
#endif
        }

        void OnDisable()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.update -= EditorTick;
#endif
        }

        // LateUpdate (no Update): así pisamos cualquier regeneración propia de TMP.
        void LateUpdate() { if (Application.isPlaying) Warp(); }

#if UNITY_EDITOR
        double _lastTick;
        void EditorTick()
        {
            if (this == null) { UnityEditor.EditorApplication.update -= EditorTick; return; }
            double now = UnityEditor.EditorApplication.timeSinceStartup;
            if (now - _lastTick < 1.0 / 30.0) return; // ~30 fps, no fundir el editor
            _lastTick = now;
            if (Warp()) UnityEditor.SceneView.RepaintAll();
        }
#endif

        // ----------------------------------------------------------- montaje
        void BuildText()
        {
            // CLAVE para la nitidez: el texto se genera a su TAMAÑO REAL (fontSize
            // en metros, 1 unidad = 1 m) y se deforma SIN escalar (_scale = 1). Si
            // se generase pequeño y se escalara la malla, el SDF de TMP calcularía
            // el suavizado de borde para el tamaño pequeño → se difumina de lejos.
            // El 0.7 pasa de "alto de mayúscula deseado" a fontSize.
            _tmp.fontSize = Mathf.Max(1f, ReExpoTickerConfig.LetterMeters / 0.7f);
            _scale = 1f;

            _tmp.enableWordWrapping = false;
            _tmp.overflowMode = TextOverflowModes.Overflow;
            _tmp.alignment = TextAlignmentOptions.Left;
            _tmp.fontStyle = FontStyles.Bold;

            string unit = _label + Separator;
            float unitW = _tmp.GetPreferredValues(unit).x; // en metros
            // sin fuente (TMP Essentials sin importar) GetPreferredValues da ~0:
            // no construimos para no generar una cadena gigante.
            if (_tmp.font == null || unitW < 0.01f) { _chars = null; return; }

            // nº de repeticiones ENTERAS que caben en el perímetro (mín 1; si una
            // sola no cabe, se solapará algo: es el máx físico). El sobrante del
            // perímetro queda como hueco que gira (separación natural).
            int reps = Mathf.Clamp(Mathf.FloorToInt(_perimeter / unitW), 1, 200);

            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < reps; i++) sb.Append(unit);
            _tmp.text = sb.ToString();
            _tmp.ForceMeshUpdate();

            var ti = _tmp.textInfo;

            var list = new List<CharVerts>(ti.characterCount);
            _minX = float.MaxValue;
            for (int c = 0; c < ti.characterCount; c++)
            {
                var ch = ti.characterInfo[c];
                if (!ch.isVisible) continue;
                int mat = ch.materialReferenceIndex, vi = ch.vertexIndex;
                var v = ti.meshInfo[mat].vertices;
                var cv = new CharVerts
                {
                    mat = mat, vi = vi,
                    v0 = v[vi], v1 = v[vi + 1], v2 = v[vi + 2], v3 = v[vi + 3],
                    baseY = ch.baseLine,
                };
                cv.midX = (cv.v0.x + cv.v2.x) * 0.5f;
                if (cv.v0.x < _minX) _minX = cv.v0.x;
                list.Add(cv);
            }
            if (_minX == float.MaxValue) _minX = 0f;
            _chars = list.ToArray();
            _lastLetter = ReExpoTickerConfig.LetterMeters;
        }

        void ApplyMaterial()
        {
            RefreshEmission();
            _tmp.color = Color.white;
            if (_mr != null)
            {
                _mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                _mr.receiveShadows = false;
            }
        }

        /// Color/fuerza emisiva del LED: el color es el de la ZONA, escalado en
        /// HDR por la fuerza (EV) de la config. Se puede llamar en vivo.
        void RefreshEmission()
        {
            if (_tmp == null) return;
            try
            {
                var mat = _tmp.fontMaterial; // instancia propia
                Color led = _zoneColor * Mathf.Pow(2f, ReExpoTickerConfig.Intensity);
                led.a = 1f;
                mat.EnableKeyword("GLOW_ON");
                mat.SetColor(ShaderUtilities.ID_FaceColor, led);
                // glow CONTENIDO: un halo fino (más ancho difumina las letras de lejos)
                if (mat.HasProperty(ShaderUtilities.ID_GlowColor)) mat.SetColor(ShaderUtilities.ID_GlowColor, led);
                if (mat.HasProperty(ShaderUtilities.ID_GlowPower)) mat.SetFloat(ShaderUtilities.ID_GlowPower, 0.25f);
                if (mat.HasProperty(ShaderUtilities.ID_GlowOuter)) mat.SetFloat(ShaderUtilities.ID_GlowOuter, 0.1f);
            }
            catch { /* fuente sin material SDF estándar: nos quedamos con el color de vértice */ }
            _lastIntensity = ReExpoTickerConfig.Intensity;
        }

        // ------------------------------------------------------------- warp
        /// Recoloca cada letra sobre el muro a su distancia animada. Devuelve si
        /// realmente pintó algo (para decidir si repintar la Scene).
        bool Warp()
        {
            if (_tmp == null) return false;

            bool show = ReExpoTickerConfig.Enabled;
            if (_mr != null && _mr.enabled != show) _mr.enabled = show;
            if (!show) return false;

            // --- parámetros VIVOS (se aplican al instante, sin reconstruir) ---
            if (_perimeter > 1f && !Mathf.Approximately(_lastLetter, ReExpoTickerConfig.LetterMeters))
            {
                BuildText(); // recalcula repeticiones/escala para el nuevo tamaño
                _ready = _chars != null && _chars.Length > 0;
            }
            if (!Mathf.Approximately(_lastIntensity, ReExpoTickerConfig.Intensity))
                RefreshEmission();
            _wallY = ReExpoTickerConfig.BandMeters;

            // estado incompleto (recién creado / deserializado tras recompilar) → nada que pintar
            if (!_ready || _chars == null || _ring == null || _dir == null || _cum == null) return false;

            float now = Application.isPlaying ? Time.time : Time.realtimeSinceStartup;
            float scroll = (now * ReExpoTickerConfig.SpeedMetersPerSec) % _perimeter;

            var ti = _tmp.textInfo;
            if (ti == null || ti.meshInfo == null) return false;

            for (int i = 0; i < _chars.Length; i++)
            {
                var c = _chars[i];
                if (c.mat >= ti.meshInfo.Length) continue;
                var verts = ti.meshInfo[c.mat].vertices;
                if (verts == null || c.vi + 3 >= verts.Length) continue;

                float s = (c.midX - _minX) * _scale + scroll;
                Sample(s, out Vector3 pos, out Vector3 tan);

                verts[c.vi + 0] = Place(c.v0, c, pos, tan);
                verts[c.vi + 1] = Place(c.v1, c, pos, tan);
                verts[c.vi + 2] = Place(c.v2, c, pos, tan);
                verts[c.vi + 3] = Place(c.v3, c, pos, tan);
            }

            _tmp.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices);
            return true;
        }

        Vector3 Place(Vector3 baseV, in CharVerts c, Vector3 pos, Vector3 tan)
        {
            float vx = (baseV.x - c.midX) * _scale;
            float vy = (baseV.y - c.baseY) * _scale;
            return pos + tan * vx + _up * (_wallY + vy);
        }

        /// Punto y tangente del perímetro a la distancia de arco <paramref name="s"/>.
        void Sample(float s, out Vector3 pos, out Vector3 tan)
        {
            int n = _ring.Length;
            s = s % _perimeter; if (s < 0f) s += _perimeter;
            // busca el segmento (lineal; pocos vértices por zona)
            int seg = 0;
            while (seg < n - 1 && _cum[seg + 1] <= s) seg++;
            float segLen = _cum[seg + 1] - _cum[seg];
            float t = segLen > 1e-4f ? (s - _cum[seg]) / segLen : 0f;
            Vector3 a = _ring[seg], b = _ring[(seg + 1) % n];
            pos = Vector3.Lerp(a, b, t);
            tan = _dir[seg];
        }
    }
}
#endif
