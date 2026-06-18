using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace ReExpo92.WorldKit
{
    /// <summary>
    /// Servicio de alto nivel del package. Mantiene la sesión EN MEMORIA y expone
    /// las dos operaciones que de verdad importan: descargar la clave de Google
    /// (ofuscada, solo si eres dev Unity autorizado) y descargar los datos del
    /// mapa (POIs + zonas). La persistencia del token entre recargas la hace la
    /// capa de editor (EditorPrefs); aquí solo vive el estado de la sesión.
    /// </summary>
    public static class ReExpoClient
    {
        /// Sesión activa (null si no hay login).
        public static AuthSession Session { get; set; }

        public static bool IsLoggedIn => Session != null && Session.IsValid;

        /// Se invoca al refrescar el token, para que el editor lo persista.
        public static System.Action<AuthSession> OnSessionRefreshed;

        static bool IsExpired(string error) =>
            !string.IsNullOrEmpty(error) &&
            (error.Contains("expired") || error.Contains("JWT") || error.Contains("PGRST301") || error.Contains("401"));

        /// Refresca el token con el refresh_token guardado. Devuelve si lo logró.
        static async Task<bool> TryRefresh()
        {
            if (Session == null || string.IsNullOrEmpty(Session.RefreshToken)) return false;
            var (s, e) = await SupabaseRest.Refresh(Session.RefreshToken);
            if (e != null || s == null || !s.IsValid) return false;
            Session = s;
            OnSessionRefreshed?.Invoke(s);
            return true;
        }

        /// RPC con reintento si el token ha caducado (refresca y reintenta una vez).
        static async Task<(string json, string error)> RpcWithRefresh(string fn, string body)
        {
            var bearer = IsLoggedIn ? Session.AccessToken : ReExpoConfig.SupabaseAnonKey;
            var (json, err) = await SupabaseRest.Rpc(fn, body, bearer);
            if (err != null && IsExpired(err) && await TryRefresh())
                (json, err) = await SupabaseRest.Rpc(fn, body, Session.AccessToken);
            return (json, err);
        }

        static async Task<(string json, string error)> RestGetWithRefresh(string pathAndQuery)
        {
            var bearer = IsLoggedIn ? Session.AccessToken : ReExpoConfig.SupabaseAnonKey;
            var (json, err) = await SupabaseRest.RestGet(pathAndQuery, bearer);
            if (err != null && IsExpired(err) && await TryRefresh())
                (json, err) = await SupabaseRest.RestGet(pathAndQuery, Session.AccessToken);
            return (json, err);
        }

        /// <summary>
        /// Descarga la Google Map Tiles API key. Requiere sesión con la skill
        /// `unity_dev` (o staff). Devuelve null en key si el admin aún no la fijó.
        /// </summary>
        public static async Task<(string key, string error)> FetchGoogleKey()
        {
            if (!IsLoggedIn) return (null, "Inicia sesión primero.");
            var (json, error) = await RpcWithRefresh("get_my_unity_key", "{}");
            if (error != null) return (null, error);

            // La RPC devuelve un text JSON: "<base64>" o null.
            var token = JToken.Parse(json);
            if (token == null || token.Type == JTokenType.Null) return (null, null);
            var obfuscated = token.Value<string>();
            if (string.IsNullOrEmpty(obfuscated)) return (null, null);
            return (Geo.Deobfuscate(obfuscated), null);
        }

        /// <summary>
        /// Lista las re-memorias del catálogo (id, nombre, estado, tipo) para la
        /// ventana de herramientas. La RLS decide cuáles ve cada quien.
        /// </summary>
        public static async Task<(List<ReMemoryItem> items, string error)> FetchReMemories()
        {
            var (json, err) = await RestGetWithRefresh(
                "/re_memories?select=id,name,status,description,main_image_url,model_categories(name)&order=name.asc");
            if (err != null) return (null, err);
            var list = new List<ReMemoryItem>();
            try
            {
                if (JToken.Parse(json) is JArray arr)
                    foreach (var it in arr)
                        list.Add(new ReMemoryItem
                        {
                            Id = (string)it["id"],
                            Name = (string)it["name"],
                            Status = (string)it["status"],
                            Category = (string)(it["model_categories"]?["name"]) ?? "—",
                            Description = (string)it["description"],
                            ImageUrl = (string)it["main_image_url"],
                        });
            }
            catch (Exception e) { return (null, e.Message); }
            return (list, null);
        }

        /// <summary>Descarga POIs + zonas (export_map_geojson) y los parsea.</summary>
        public static async Task<(MapData data, string error)> FetchMapData()
        {
            var (json, error) = await RpcWithRefresh("export_map_geojson", "{}");
            if (error != null) return (null, error);
            return (GeoJsonParser.Parse(json), null);
        }
    }
}
