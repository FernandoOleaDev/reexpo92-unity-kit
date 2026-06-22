using UnityEditor;
using UnityEngine;

namespace ReExpo92.WorldKit.Editor
{
    /// <summary>
    /// Punto de entrada para instalar las dependencias necesarias para construir
    /// Addressables (Unity Addressables + glTFast). Vive FUERA del define REEXPO_ADDR
    /// porque es justamente lo que activa ese define. Una vez instaladas, Unity
    /// recompila y aparece la ventana «re-Expo92 ▸ Constructor de Addressables».
    /// </summary>
    public static class BuildDepsInstaller
    {
        [MenuItem("re-Expo92/Instalar dependencias de Addressables")]
        public static void Install()
        {
            if (!EditorUtility.DisplayDialog(
                "re-Expo92 · Dependencias de Addressables",
                "Se instalarán dos paquetes del registro de Unity:\n\n" +
                "• com.unity.addressables (empaquetado del recinto)\n" +
                "• com.unity.cloud.gltfast (importar los GLB de la comunidad)\n\n" +
                "Unity los descargará y recompilará. Al terminar aparecerá la ventana " +
                "«Constructor de Addressables». ¿Instalar ahora?",
                "Instalar", "Cancelar"))
                return;

            var e1 = ReExpoEditorService.InstallAddressables();
            var e2 = ReExpoEditorService.InstallGltfast();
            if (e1 != null || e2 != null)
                Debug.LogError($"[re-Expo92] Error instalando dependencias: {e1} {e2}");
            else
                Debug.Log("[re-Expo92] Instalando Addressables + glTFast… Unity recompilará al terminar.");
        }
    }
}
