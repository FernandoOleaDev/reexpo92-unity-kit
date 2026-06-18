using System;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine.Networking;

namespace ReExpo92.WorldKit
{
    /// <summary>Sesión autenticada de un colaborador de re-Expo92.</summary>
    public class AuthSession
    {
        public string AccessToken;
        public string RefreshToken;
        public string UserId;
        public string Email;

        public bool IsValid => !string.IsNullOrEmpty(AccessToken);
    }

    /// <summary>
    /// Cliente REST mínimo de Supabase sobre UnityWebRequest (sin el SDK pesado):
    /// solo lo que necesita el package — login y llamadas a RPC. La RLS/JWT se
    /// aplican solas en el servidor a partir del Bearer token. Funciona en el
    /// editor y en players (AOT-safe, sin link.xml).
    /// </summary>
    public static class SupabaseRest
    {
        static string Auth => ReExpoConfig.SupabaseUrl + "/auth/v1";
        static string Rest => ReExpoConfig.SupabaseUrl + "/rest/v1";

        // ---- helper: await de UnityWebRequest ----
        static Task<UnityWebRequest> SendAsync(UnityWebRequest req)
        {
            var tcs = new TaskCompletionSource<UnityWebRequest>();
            var op = req.SendWebRequest();
            op.completed += _ => tcs.TrySetResult(req);
            return tcs.Task;
        }

        static UnityWebRequest Post(string url, string jsonBody, string bearer = null)
        {
            var req = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST);
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(jsonBody ?? "{}"));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("apikey", ReExpoConfig.SupabaseAnonKey);
            req.SetRequestHeader("Authorization", "Bearer " + (bearer ?? ReExpoConfig.SupabaseAnonKey));
            return req;
        }

        static bool Ok(UnityWebRequest req) => req.result == UnityWebRequest.Result.Success;

        static string ErrorText(UnityWebRequest req)
        {
            var body = req.downloadHandler != null ? req.downloadHandler.text : null;
            if (!string.IsNullOrEmpty(body))
            {
                try
                {
                    var o = JToken.Parse(body) as JObject;
                    var msg = o?.Value<string>("error_description")
                              ?? o?.Value<string>("msg")
                              ?? o?.Value<string>("message")
                              ?? o?.Value<string>("error");
                    if (!string.IsNullOrEmpty(msg)) return msg;
                }
                catch { /* body no era JSON */ }
                return body;
            }
            return req.error ?? "Error desconocido";
        }

        static AuthSession ParseSession(string json)
        {
            var o = JToken.Parse(json) as JObject;
            if (o == null) return null;
            return new AuthSession
            {
                AccessToken = o.Value<string>("access_token"),
                RefreshToken = o.Value<string>("refresh_token"),
                UserId = (o["user"] as JObject)?.Value<string>("id"),
                Email = (o["user"] as JObject)?.Value<string>("email"),
            };
        }

        // ---- email + contraseña ----
        public static async Task<(AuthSession session, string error)> SignInPassword(string email, string password)
        {
            var body = new JObject { ["email"] = email, ["password"] = password }.ToString();
            using (var req = Post(Auth + "/token?grant_type=password", body))
            {
                await SendAsync(req);
                if (!Ok(req)) return (null, ErrorText(req));
                return (ParseSession(req.downloadHandler.text), null);
            }
        }

        // ---- refresh ----
        public static async Task<(AuthSession session, string error)> Refresh(string refreshToken)
        {
            var body = new JObject { ["refresh_token"] = refreshToken }.ToString();
            using (var req = Post(Auth + "/token?grant_type=refresh_token", body))
            {
                await SendAsync(req);
                if (!Ok(req)) return (null, ErrorText(req));
                return (ParseSession(req.downloadHandler.text), null);
            }
        }

        // ---- Google OAuth (PKCE) ----
        /// URL del navegador para iniciar el login con Google (flujo PKCE).
        public static string BuildGoogleAuthorizeUrl(string codeChallenge, string redirectTo)
        {
            return Auth + "/authorize?provider=google&flow_type=pkce"
                 + "&code_challenge=" + UnityWebRequest.EscapeURL(codeChallenge)
                 + "&code_challenge_method=s256"
                 + "&redirect_to=" + UnityWebRequest.EscapeURL(redirectTo);
        }

        /// Intercambia el code del loopback por una sesión.
        public static async Task<(AuthSession session, string error)> ExchangePkce(string authCode, string codeVerifier)
        {
            var body = new JObject { ["auth_code"] = authCode, ["code_verifier"] = codeVerifier }.ToString();
            using (var req = Post(Auth + "/token?grant_type=pkce", body))
            {
                await SendAsync(req);
                if (!Ok(req)) return (null, ErrorText(req));
                return (ParseSession(req.downloadHandler.text), null);
            }
        }

        // ---- RPC ----
        /// Llama a una función RPC y devuelve su JSON crudo (o error).
        public static async Task<(string json, string error)> Rpc(string fn, string jsonBody, string bearer)
        {
            using (var req = Post(Rest + "/rpc/" + fn, jsonBody ?? "{}", bearer))
            {
                await SendAsync(req);
                if (!Ok(req)) return (null, ErrorText(req));
                return (req.downloadHandler.text, null);
            }
        }
    }
}
