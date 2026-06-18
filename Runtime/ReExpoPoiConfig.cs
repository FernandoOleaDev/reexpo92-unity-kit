using System.Collections.Generic;
using UnityEngine;

namespace ReExpo92.WorldKit
{
    /// <summary>Marca un POI con su re-memoria y categoría (para filtrar/buscar).</summary>
    public class ReExpoPoiRef : MonoBehaviour
    {
        public string ReMemoryId;
        public string PoiName;
        public string Category;
    }

    /// <summary>
    /// Qué categorías de POIs se muestran. Por defecto SOLO «Pabellones oficiales»
    /// para no mezclarlo todo al empezar. El editor rellena esto desde EditorPrefs.
    /// </summary>
    public static class ReExpoPoiConfig
    {
        public static bool AllCategories = false;
        public static HashSet<string> EnabledCategories = new HashSet<string> { "Pabellones oficiales" };

        public static bool IsEnabled(string category) =>
            AllCategories || (category != null && EnabledCategories.Contains(category));
    }
}
