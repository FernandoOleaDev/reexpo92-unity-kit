using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ReExpo92.WorldKit.Editor
{
    /// <summary>
    /// Qué categorías de POIs se ven (guardado en EditorPrefs). Por defecto solo
    /// «Pabellones oficiales». Aplica en vivo activando/desactivando los POIs por
    /// su <see cref="ReExpoPoiRef"/>.
    /// </summary>
    [InitializeOnLoad]
    static class ReExpoPoiFilter
    {
        const string KAll = "ReExpo92.PoiAll";
        const string KCats = "ReExpo92.PoiCats";

        static ReExpoPoiFilter() { Load(); }

        public static bool AllCategories
        {
            get => EditorPrefs.GetBool(KAll, false);
            set { EditorPrefs.SetBool(KAll, value); Load(); Apply(); }
        }

        public static HashSet<string> Enabled
        {
            get => new HashSet<string>(
                EditorPrefs.GetString(KCats, "Pabellones oficiales").Split('|').Where(x => !string.IsNullOrEmpty(x)));
        }

        public static bool IsOn(string cat) => AllCategories || Enabled.Contains(cat);

        public static void SetCategory(string cat, bool on)
        {
            var set = Enabled;
            if (on) set.Add(cat); else set.Remove(cat);
            EditorPrefs.SetString(KCats, string.Join("|", set));
            Load();
            Apply();
        }

        static void Load()
        {
            ReExpoPoiConfig.AllCategories = AllCategories;
            ReExpoPoiConfig.EnabledCategories = Enabled;
        }

        public static void Apply()
        {
            var pois = Object.FindObjectsByType<ReExpoPoiRef>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var p in pois)
            {
                bool on = ReExpoPoiConfig.IsEnabled(p.Category);
                if (p.gameObject.activeSelf != on) p.gameObject.SetActive(on);
            }
            SceneView.RepaintAll();
        }
    }
}
