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

        /// <summary>
        /// Descarga la Google Map Tiles API key. Requiere sesión con la skill
        /// `unity_dev` (o staff). Devuelve null en key si el admin aún no la fijó.
        /// </summary>
        public static async Task<(string key, string error)> FetchGoogleKey()
        {
            if (!IsLoggedIn) return (null, "Inicia sesión primero.");
            var (json, error) = await SupabaseRest.Rpc("get_my_unity_key", "{}", Session.AccessToken);
            if (error != null) return (null, error);

            // La RPC devuelve un text JSON: "<base64>" o null.
            var token = JToken.Parse(json);
            if (token == null || token.Type == JTokenType.Null) return (null, null);
            var obfuscated = token.Value<string>();
            if (string.IsNullOrEmpty(obfuscated)) return (null, null);
            return (Geo.Deobfuscate(obfuscated), null);
        }

        /// <summary>Descarga POIs + zonas (export_map_geojson) y los parsea.</summary>
        public static async Task<(MapData data, string error)> FetchMapData()
        {
            var bearer = IsLoggedIn ? Session.AccessToken : ReExpoConfig.SupabaseAnonKey;
            var (json, error) = await SupabaseRest.Rpc("export_map_geojson", "{}", bearer);
            if (error != null) return (null, error);
            return (GeoJsonParser.Parse(json), null);
        }
    }
}
