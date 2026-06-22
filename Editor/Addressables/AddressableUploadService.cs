// Sube los artefactos de Addressables (ServerData/<target>/**) al bucket
// `model-addressables` de Supabase, con el token real del colaborador (unity_dev).
// async/await, igual que el resto del package. Requiere com.unity.addressables
// (asmdef con defineConstraint REEXPO_ADDR).
#if REEXPO_ADDR
using System;
using System.IO;
using System.Threading.Tasks;
using UnityEditor;
using ReExpo92.WorldKit;

namespace ReExpo92.WorldKit.Editor
{
    public static class AddressableUploadService
    {
        public const string Bucket = "model-addressables";

        /// Sube todos los ficheros de `serverDataDir` preservando las rutas relativas
        /// bajo model-addressables/&lt;prefix&gt;/. Devuelve la URL pública del catálogo (o error).
        public static async Task<(string catalogUrl, string error)> UploadFolder(string serverDataDir, string prefix)
        {
            if (!Directory.Exists(serverDataDir))
                return (null, $"No existe la carpeta de build: {serverDataDir}. ¿Construiste el contenido?");
            var files = Directory.GetFiles(serverDataDir, "*", SearchOption.AllDirectories);
            if (files.Length == 0)
                return (null, "ServerData vacío: ¿activaste «Build Remote Catalog» y construiste?");

            string bearer = ReExpoClient.AccessTokenOrAnon;
            string catalogUrl = null;
            try
            {
                for (int i = 0; i < files.Length; i++)
                {
                    string rel = files[i].Substring(serverDataDir.Length).TrimStart('/', '\\').Replace('\\', '/');
                    string objectPath = $"{Bucket}/{prefix}/{rel}";
                    EditorUtility.DisplayProgressBar("Subiendo Addressable", rel, (i + 1f) / files.Length);

                    var err = await SupabaseRest.StoragePut(objectPath, File.ReadAllBytes(files[i]), bearer);
                    if (err != null) return (null, $"Error subiendo {rel}: {err}");

                    // El catálogo binario (o json) es el punto de entrada que el player carga.
                    if ((rel.StartsWith("catalog") && (rel.EndsWith(".bin") || rel.EndsWith(".json"))))
                        catalogUrl = $"{ReExpoConfig.SupabaseUrl}/storage/v1/object/public/{Bucket}/{prefix}/{rel}";
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            if (catalogUrl == null)
                return (null, "Subido, pero no encontré el catálogo (catalog*.bin/.json). Revisa «Build Remote Catalog».");
            return (catalogUrl, null);
        }
    }
}
#endif
