using UnityEngine;

namespace ReExpo92.WorldKit
{
    /// <summary>
    /// Ajustes VIVOS del «cartel LED» que recorre el perímetro de cada zona
    /// (texto emisivo repitiendo el nombre, dando vueltas por las paredes).
    /// Los rellena el editor (EditorPrefs, ver ReExpoTicker) y los lee cada
    /// <see cref="ReExpo92.WorldKit.Cesium.ReExpoZoneTicker"/> cada frame, así
    /// se actualizan al instante al tocar los controles y quedan guardados.
    /// </summary>
    public static class ReExpoTickerConfig
    {
        /// Mostrar u ocultar todos los carteles LED de zona.
        public static bool Enabled = true;

        /// Altura de letra, en metros (tamaño real del LED en la pared).
        public static float LetterMeters = 50f;

        /// Altura ABSOLUTA de la banda sobre el suelo, en metros (puede superar el
        /// alto de la zona; así el cartel se ve desde lejos por encima del muro).
        public static float BandMeters = 16.7f;

        /// Velocidad del desplazamiento, en metros/segundo (las letras «giran»).
        public static float SpeedMetersPerSec = 4f;

        /// Fuerza del emisivo, en EV (el color final = colorZona × 2^Intensity).
        /// El COLOR de la letra es el de cada zona (no se fija aquí).
        public static float Intensity = 2.1f;
    }
}
