using UnityEngine;

namespace ReExpo92.WorldKit
{
    /// <summary>
    /// Ajustes VIVOS de los POIs (bola) y los carteles. Los rellena el editor
    /// (desde EditorPrefs, ver ReExpoLabels) y los leen los billboards cada frame,
    /// así se actualizan al instante al tocar los sliders y quedan guardados.
    ///
    /// Radios: si la cámara está MÁS LEJOS que FarRadius el cartel NO existe
    /// (escala 0); MÁS CERCA que NearRadius → tamaño máximo; entre ambos, escala.
    /// FarRadius debe ser siempre > NearRadius.
    /// </summary>
    public static class ReExpoLabelConfig
    {
        public static bool Enabled = true;

        /// Offset de rotación de la bola (grados, sobre el "mirar a cámara").
        public static Vector3 BallRotOffset = new Vector3(120f, 35f, 0f);

        /// Offset de rotación del cartel (grados, sobre el "mirar a cámara").
        public static Vector3 LabelRotOffset = Vector3.zero;

        /// Offset de posición del cartel, en ejes de CÁMARA (x=derecha, y=arriba,
        /// z=hacia el fondo), en metros, respecto al centro de la bola.
        public static Vector3 LabelPosOffset = new Vector3(5f, 0f, 0f);

        /// Escala máxima del cartel (dentro del radio cercano).
        public static float LabelMaxSize = 0.83f;

        /// Tamaño de fuente del TextMeshPro (se aplica al cartel).
        public static float LabelFontSize = 36f;

        public static float NearRadius = 200f;
        public static float FarRadius = 450f;

        /// Elevar la cabeza+cartel por encima de un edificio actual que tape el POI
        /// (se detecta muestreando la malla de Google al construir). La base NO se mueve.
        public static bool RaiseOverBuildings = true;

        /// Holgura (m) entre el tejado que tapa y la BASE de la bola al elevarla.
        public static float PinClearanceMeters = 5f;
    }
}
