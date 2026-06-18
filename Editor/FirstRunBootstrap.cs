using UnityEditor;

namespace ReExpo92.WorldKit.Editor
{
    /// <summary>
    /// Restaura la sesión al cargar el editor y abre el asistente en el primer
    /// arranque (una vez). Patrón recomendado: InitializeOnLoad + tick diferido
    /// (el editor aún no está listo dentro del constructor estático) + flags en
    /// SessionState (por sesión) y EditorPrefs (persistente).
    /// </summary>
    [InitializeOnLoad]
    static class FirstRunBootstrap
    {
        const string CheckedKey = "ReExpo92.FirstRunChecked";

        static FirstRunBootstrap()
        {
            ReExpoEditorService.Restore();
            if (SessionState.GetBool(CheckedKey, false)) return;
            SessionState.SetBool(CheckedKey, true);
            EditorApplication.update += Deferred;
        }

        static void Deferred()
        {
            EditorApplication.update -= Deferred;
            if (!ReExpoEditorService.SetupDone)
                SetupWizardWindow.Open();
        }
    }
}
