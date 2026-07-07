using UnityEditor;
using UnityEngine;

namespace ReExpo92.WorldKit.Editor
{
    /// <summary>
    /// Acceso DIRECTO (menú) para instalar las dependencias de Addressables
    /// (Unity Addressables + glTFast). El camino principal es el Asistente de
    /// configuración (paso «Requisitos»), que ofrece lo mismo; esto queda como
    /// atajo / reinstalación. Vive FUERA del define REEXPO_ADDR porque es justo
    /// lo que activa ese define. Al terminar, Unity recompila y aparecen las
    /// ventanas «Constructor de Addressables» y «Revisión de Addressables».
    /// </summary>
    public static class BuildDepsInstaller
    {
        [MenuItem("re-Expo92/Instalar dependencias de Addressables")]
        public static void Install()
        {
            if (ReExpoEditorService.AddressableDepsReady)
            {
                EditorUtility.DisplayDialog(
                    "re-Expo92 · Dependencias de Addressables",
                    "Addressables + glTFast ya están instalados. Ya puedes usar el " +
                    "«Constructor de Addressables».",
                    "Vale");
                return;
            }

            if (!EditorUtility.DisplayDialog(
                "re-Expo92 · Dependencias de Addressables",
                "Se instalarán dos paquetes del registro de Unity:\n\n" +
                "• com.unity.addressables (empaquetado del recinto)\n" +
                "• com.unity.cloud.gltfast (importar los GLB de la comunidad)\n\n" +
                "Unity los descargará y recompilará. Al terminar aparecerá la ventana " +
                "«Constructor de Addressables». ¿Instalar ahora?",
                "Instalar", "Cancelar"))
                return;

            var err = ReExpoEditorService.InstallAddressableDeps();
            if (err != null)
                Debug.LogError($"[re-Expo92] Error instalando dependencias: {err}");
            else
                Debug.Log("[re-Expo92] Instalando Addressables + glTFast… Unity recompilará al terminar.");
        }
    }
}
