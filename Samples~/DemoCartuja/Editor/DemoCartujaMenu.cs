using ReExpo92.WorldKit.Editor;
using UnityEditor;

namespace ReExpo92.WorldKit.Samples
{
    /// <summary>Atajo de menú para construir la demo de la Cartuja.</summary>
    static class DemoCartujaMenu
    {
        [MenuItem("re-Expo92/Samples/Construir demo Cartuja", false, 100)]
        static async void BuildDemo()
        {
            var msg = await ReExpoEditorService.BuildWorld(true, true, true);
            if (!string.IsNullOrEmpty(msg)) UnityEngine.Debug.Log("[re-Expo92] " + msg);
        }
    }
}
