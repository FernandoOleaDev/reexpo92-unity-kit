using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace ReExpo92.WorldKit.Editor
{
    /// <summary>
    /// Importa automáticamente los "TMP Essential Resources" si faltan (sin ellos
    /// TextMeshPro no tiene fuente por defecto y los carteles no se ven). Se
    /// comprueba al cargar el editor y desde el asistente.
    /// </summary>
    [InitializeOnLoad]
    static class ReExpoTMPSetup
    {
        static ReExpoTMPSetup() { EditorApplication.delayCall += AutoEnsure; }

        static void AutoEnsure()
        {
            if (!EssentialsPresent()) EnsureEssentials();
        }

        public static bool EssentialsPresent() => TMP_Settings.defaultFontAsset != null;

        /// Importa los recursos esenciales de TextMeshPro si faltan.
        public static bool EnsureEssentials()
        {
            if (EssentialsPresent()) return true;
            var pkg = FindEssentials();
            if (pkg == null)
            {
                Debug.LogWarning("[re-Expo92] No encuentro «TMP Essential Resources.unitypackage». " +
                                 "Impórtalo a mano: Window ▸ TextMeshPro ▸ Import TMP Essential Resources.");
                return false;
            }
            Debug.Log("[re-Expo92] Importando TMP Essential Resources…");
            AssetDatabase.ImportPackage(pkg, false);
            return true;
        }

        static string FindEssentials()
        {
            const string rel = "Package Resources/TMP Essential Resources.unitypackage";
            foreach (var p in new[] { "Packages/com.unity.ugui/" + rel, "Packages/com.unity.textmeshpro/" + rel })
            {
                var f = Path.GetFullPath(p);
                if (File.Exists(f)) return f;
            }
            var cache = Path.GetFullPath("Library/PackageCache");
            if (Directory.Exists(cache))
                foreach (var name in new[] { "com.unity.ugui", "com.unity.textmeshpro" })
                    foreach (var dir in Directory.GetDirectories(cache, name + "@*"))
                    {
                        var f = Path.Combine(dir, "Package Resources", "TMP Essential Resources.unitypackage");
                        if (File.Exists(f)) return f;
                    }
            return null;
        }
    }
}
