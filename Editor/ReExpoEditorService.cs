using System;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ReExpo92.WorldKit.Editor
{
    /// <summary>
    /// Capa de editor: persiste la sesión en EditorPrefs (machine-local, nunca en
    /// el repo) y orquesta login / descarga de clave / construcción del mundo.
    /// </summary>
    public static class ReExpoEditorService
    {
        const string KAccess = "ReExpo92.AccessToken";
        const string KRefresh = "ReExpo92.RefreshToken";
        const string KEmail = "ReExpo92.Email";
        const string KUser = "ReExpo92.UserId";
        const string KSetupDone = "ReExpo92.SetupDone";

        const int LoopbackPort = 54321; // debe estar en las Redirect URLs de Supabase

        public static bool SetupDone
        {
            get => EditorPrefs.GetBool(KSetupDone, false);
            set => EditorPrefs.SetBool(KSetupDone, value);
        }

        public static bool IsLoggedIn => ReExpoClient.IsLoggedIn;
        public static string Email => ReExpoClient.Session?.Email;

        /// Restaura la sesión guardada (llamar al cargar el editor / abrir ventana).
        public static void Restore()
        {
            var access = EditorPrefs.GetString(KAccess, null);
            if (string.IsNullOrEmpty(access)) return;
            ReExpoClient.Session = new AuthSession
            {
                AccessToken = access,
                RefreshToken = EditorPrefs.GetString(KRefresh, null),
                Email = EditorPrefs.GetString(KEmail, null),
                UserId = EditorPrefs.GetString(KUser, null),
            };
        }

        static void Persist(AuthSession s)
        {
            ReExpoClient.Session = s;
            EditorPrefs.SetString(KAccess, s.AccessToken ?? "");
            EditorPrefs.SetString(KRefresh, s.RefreshToken ?? "");
            EditorPrefs.SetString(KEmail, s.Email ?? "");
            EditorPrefs.SetString(KUser, s.UserId ?? "");
        }

        public static void SignOut()
        {
            ReExpoClient.Session = null;
            EditorPrefs.DeleteKey(KAccess);
            EditorPrefs.DeleteKey(KRefresh);
            EditorPrefs.DeleteKey(KEmail);
            EditorPrefs.DeleteKey(KUser);
        }

        // ---- login email + contraseña ----
        public static async Task<string> SignInPassword(string email, string password)
        {
            var (session, error) = await SupabaseRest.SignInPassword(email, password);
            if (error != null) return error;
            Persist(session);
            return null;
        }

        // ---- login con Google (PKCE + loopback local) ----
        public static async Task<string> SignInGoogle()
        {
            string verifier = RandomUrlToken(64);
            string challenge = Base64Url(SHA256.Create().ComputeHash(Encoding.ASCII.GetBytes(verifier)));
            string redirect = $"http://127.0.0.1:{LoopbackPort}/callback";

            HttpListener listener = null;
            try
            {
                listener = new HttpListener();
                listener.Prefixes.Add($"http://127.0.0.1:{LoopbackPort}/");
                listener.Start();
            }
            catch (Exception e)
            {
                return $"No se pudo abrir el puerto {LoopbackPort}: {e.Message}";
            }

            Application.OpenURL(SupabaseRest.BuildGoogleAuthorizeUrl(challenge, redirect));

            string code;
            try
            {
                var ctx = await listener.GetContextAsync();
                code = ctx.Request.QueryString["code"];
                var html = Encoding.UTF8.GetBytes(
                    "<html><body style='font-family:sans-serif;text-align:center;padding-top:3rem'>" +
                    "<h2>re-Expo92</h2><p>Sesion iniciada. Puedes cerrar esta pestana y volver a Unity.</p></body></html>");
                ctx.Response.ContentType = "text/html; charset=utf-8";
                ctx.Response.OutputStream.Write(html, 0, html.Length);
                ctx.Response.Close();
            }
            finally
            {
                listener.Stop();
            }

            if (string.IsNullOrEmpty(code)) return "No se recibió el código de Google.";
            var (session, error) = await SupabaseRest.ExchangePkce(code, verifier);
            if (error != null) return error;
            Persist(session);
            return null;
        }

        // ---- construir el mundo en la escena activa ----
        public static async Task<string> BuildWorld(bool tiles, bool pois, bool zones)
        {
            var builder = WorldBuilderLocator.Find();
            if (builder == null)
                return "Instala Cesium for Unity (com.cesium.unity) para construir el mundo 3D.";

            string apiKey = null;
            if (tiles)
            {
                var (key, ek) = await ReExpoClient.FetchGoogleKey();
                if (ek != null) return "Clave de Google: " + ek;
                if (string.IsNullOrEmpty(key))
                    return "El administrador aún no ha configurado la clave de Google (/admin/unity).";
                apiKey = key;
            }

            var (data, ed) = await ReExpoClient.FetchMapData();
            if (ed != null) return "Datos del mapa: " + ed;

            var go = builder.Build(new WorldBuildOptions
            {
                GoogleApiKey = apiKey,
                Data = data,
                ShowGoogleTiles = tiles,
                ShowPois = pois,
                ShowZones = zones,
            });

            Selection.activeGameObject = go;
            EditorSceneManager.MarkSceneDirty(go.scene);
            int pc = data?.Pois?.Count ?? 0, zc = data?.Zones?.Count ?? 0;
            return $"OK · {pc} POIs y {zc} zonas. {(apiKey != null ? "Maqueta de Google activa." : "Sin maqueta de Google.")}";
        }

        // ---- helpers PKCE ----
        static string RandomUrlToken(int bytes)
        {
            var buf = new byte[bytes];
            using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(buf);
            return Base64Url(buf);
        }

        static string Base64Url(byte[] data) =>
            Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}
