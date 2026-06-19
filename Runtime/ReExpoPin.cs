using UnityEngine;

namespace ReExpo92.WorldKit
{
    /// <summary>
    /// Chincheta de un POI: poste + bola (cabeza). La BASE no se mueve (queda a
    /// ras de suelo real); este componente solo permite ELEVAR la cabeza —y con
    /// ella el cartel— alargando el poste, para que asomen por encima de un
    /// edificio actual que tape el POI. Ver el muestreo DSM en CesiumWorldBuilder.
    /// </summary>
    public class ReExpoPin : MonoBehaviour
    {
        public Transform pole;
        public Transform head;
        public ReExpoBillboardLabel label;

        public const float DefaultHeadY = 15.5f; // altura local por defecto del centro de la bola
        public const float HeadRadius = 3.5f;    // radio de la bola (diámetro 7)

        /// Coloca el centro de la bola a la altura local <paramref name="headY"/>
        /// (nunca por debajo de la por defecto) y estira el poste hasta ella; el
        /// cartel sigue a la bola.
        public void SetHeadCenterY(float headY)
        {
            headY = Mathf.Max(DefaultHeadY, headY);
            if (head != null) head.localPosition = new Vector3(0f, headY, 0f);
            if (pole != null)
            {
                float poleTop = Mathf.Max(0.2f, headY - HeadRadius); // el poste llega justo bajo la bola
                pole.localScale = new Vector3(0.8f, poleTop * 0.5f, 0.8f); // mesh = 2 u → escala = alto/2
                pole.localPosition = new Vector3(0f, poleTop * 0.5f, 0f);  // base en y=0
            }
            if (label != null) label.ballCenterLocal = new Vector3(0f, headY, 0f);
        }
    }
}
