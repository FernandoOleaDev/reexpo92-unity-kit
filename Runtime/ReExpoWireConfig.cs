using UnityEngine;

namespace ReExpo92.WorldKit
{
    /// <summary>
    /// Ajustes VIVOS del wireframe técnico (líneas baricéntricas sobre los tiles,
    /// para grabar vídeo en editor). Los rellena el editor (EditorPrefs, ver
    /// ReExpoWire) y los lee el componente <c>ReExpoTechWire</c> del tileset.
    /// </summary>
    public static class ReExpoWireConfig
    {
        /// Modo wireframe activo (hornea baricéntricas en los tiles y pone el material).
        public static bool Enabled = false;

        /// Color de las líneas por distancia tipo arcoíris (true) o color fijo (false).
        public static bool Rainbow = true;

        /// Grosor de línea (px).
        public static float LineWidth = 1.4f;

        /// Color de línea cuando NO es arcoíris (su alpha = opacidad de la línea siempre).
        public static Color LineColor = new Color(0.4f, 0.9f, 1f, 1f);

        /// Velo de cara (0 = solo aristas).
        public static float FaceAlpha = 0f;

        /// Rango de distancia del degradado: en <see cref="FadeNear"/> empieza el
        /// arcoíris y en <see cref="FadeFar"/> termina (y más allá se desvanece).
        public static float FadeNear = 40f;
        public static float FadeFar = 700f;
    }
}
