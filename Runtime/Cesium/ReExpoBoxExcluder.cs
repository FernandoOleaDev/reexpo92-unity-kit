#if REEXPO_CESIUM
using UnityEngine;
using CesiumForUnity;

namespace ReExpo92.WorldKit.Cesium
{
    /// <summary>
    /// Excluye de la carga cualquier tile de Google que quede FUERA de una caja
    /// (la Isla de la Cartuja + alrededores). Así la maqueta no carga el mundo
    /// entero: mejor rendimiento y el usuario se mantiene en el recinto.
    ///
    /// Patrón oficial de Cesium (CesiumBoxExcluder): un BoxCollider define el
    /// área; los tiles que no intersecan la caja se descartan.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class ReExpoBoxExcluder : CesiumTileExcluder
    {
        BoxCollider _box;

        protected override void OnEnable()
        {
            base.OnEnable();
            _box = GetComponent<BoxCollider>();
        }

        public override bool ShouldExclude(Cesium3DTile tile)
        {
            if (_box == null) _box = GetComponent<BoxCollider>();
            if (_box == null) return false;
            return !_box.bounds.Intersects(tile.bounds);
        }
    }
}
#endif
