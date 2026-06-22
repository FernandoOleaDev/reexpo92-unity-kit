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

        /// Bearer a usar: el token de sesión si hay login, si no la anon key.
        /// (Las escrituras de Storage/RPC exigen el token real del colaborador.)
        public static string AccessTokenOrAnon =>
            (Session != null && Session.IsValid) ? Session.AccessToken : ReExpoConfig.SupabaseAnonKey;

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

        // ====================== ADDRESSABLES (recreación nivel B) ======================

        /// <summary>
        /// El último Addressable APROBADO de una pieza (o null si no hay), con su
        /// catálogo/clave y si está «desactualizado». RPC get_unity_build (devuelve
        /// 0 o 1 fila como array PostgREST).
        /// </summary>
        public static async Task<(UnityBuildInfo build, string error)> GetUnityBuild(string reMemoryId)
        {
            var body = new JObject { ["p_re_memory_id"] = reMemoryId }.ToString();
            var (json, err) = await RpcWithRefresh("get_unity_build", body);
            if (err != null) return (null, err);
            try
            {
                if (JToken.Parse(json) is JArray arr && arr.Count > 0)
                {
                    var o = (JObject)arr[0];
                    return (new UnityBuildInfo
                    {
                        Id = (string)o["id"],
                        CatalogUrl = (string)o["catalog_url"],
                        EntryKey = (string)o["entry_key"],
                        BundlePath = (string)o["bundle_path"],
                        Status = (string)o["status"],
                        HasScript = o["has_script"]?.Value<bool>() ?? false,
                        Stale = o["stale"]?.Value<bool>() ?? false,
                    }, null);
                }
            }
            catch (Exception e) { return (null, e.Message); }
            return (null, null); // no hay build aprobado todavía
        }

        /// <summary>
        /// URL pública del GLB de la última versión APROBADA/SELECCIONADA de una
        /// pieza (la materia prima para construir el Addressable). null si no hay.
        /// </summary>
        public static async Task<(string url, string error)> GetApprovedGlbUrl(string reMemoryId)
        {
            var path = "/re_memory_model_versions?re_memory_id=eq." + reMemoryId
                     + "&status=in.(aprobada,seleccionada)&select=file_url,version_number"
                     + "&order=version_number.desc&limit=1";
            var (json, err) = await RestGetWithRefresh(path);
            if (err != null) return (null, err);
            try
            {
                if (JToken.Parse(json) is JArray arr && arr.Count > 0)
                    return ((string)arr[0]["file_url"], null);
            }
            catch (Exception e) { return (null, e.Message); }
            return (null, null); // sin versión aprobada
        }

        /// <summary>
        /// Estado de recreación de TODAS las piezas (RPC get_unity_build_states):
        /// si tiene modelo aprobado, si tiene Addressable, y si está desactualizado.
        /// Para pintar los chips de la lista de Herramientas.
        /// </summary>
        public static async Task<(Dictionary<string, UnityBuildState> states, string error)> GetUnityBuildStates()
        {
            var (json, err) = await RpcWithRefresh("get_unity_build_states", "{}");
            if (err != null) return (null, err);
            var map = new Dictionary<string, UnityBuildState>();
            try
            {
                if (JToken.Parse(json) is JArray arr)
                    foreach (var o in arr)
                    {
                        var id = (string)o["reMemoryId"];
                        if (id == null) continue;
                        map[id] = new UnityBuildState
                        {
                            HasModel = o["hasModel"]?.Value<bool>() ?? false,
                            HasBuild = o["hasBuild"]?.Value<bool>() ?? false,
                            Stale = o["stale"]?.Value<bool>() ?? false,
                        };
                    }
            }
            catch (Exception e) { return (null, e.Message); }
            return (map, null);
        }

        /// <summary>¿El usuario logueado es revisor de Unity? (RPC can_review_subject).</summary>
        public static async Task<bool> CanReviewUnity()
        {
            if (!IsLoggedIn) return false;
            var (json, err) = await RpcWithRefresh("can_review_subject", new JObject { ["p_type"] = "unity_build" }.ToString());
            if (err != null) return false;
            try { return JToken.Parse(json)?.Value<bool>() ?? false; } catch { return false; }
        }

        /// <summary>Builds pendientes de revisión (solo revisores; gate en la RPC).</summary>
        public static async Task<(List<PendingBuild> builds, string error)> GetPendingUnityBuilds()
        {
            var (json, err) = await RpcWithRefresh("get_pending_unity_builds", "{}");
            if (err != null) return (null, err);
            var list = new List<PendingBuild>();
            try
            {
                if (JToken.Parse(json) is JArray arr)
                    foreach (var o in arr)
                        list.Add(new PendingBuild
                        {
                            Id = (string)o["id"],
                            ReMemoryId = (string)o["reMemoryId"],
                            ReMemoryName = (string)o["reMemoryName"],
                            BuildTarget = (string)o["buildTarget"],
                            CatalogUrl = (string)o["catalogUrl"],
                            EntryKey = (string)o["entryKey"],
                            HasScript = o["hasScript"]?.Value<bool>() ?? false,
                            ScriptStatus = (string)o["scriptStatus"],
                            BuiltBy = (string)o["builtBy"],
                            CreatedAt = (string)o["createdAt"],
                        });
            }
            catch (Exception e) { return (null, e.Message); }
            return (list, null);
        }

        /// <summary>
        /// Emite un voto de revisión por el motor de consenso (RPC cast_review_vote).
        /// El servidor exige rol revisor y bloquea auto-revisión. Devuelve (ok, error).
        /// </summary>
        public static async Task<(bool ok, string error)> CastReviewVote(string subjectType, string subjectId, string vote, string comment)
        {
            if (!IsLoggedIn) return (false, "Inicia sesión primero.");
            var body = new JObject
            {
                ["p_subject_type"] = subjectType,
                ["p_subject_id"] = subjectId,
                ["p_vote"] = vote,
                ["p_comment"] = comment,
            }.ToString();
            var (_, err) = await RpcWithRefresh("cast_review_vote", body);
            return (err == null, err);
        }

        /// <summary>
        /// Registra un Addressable construido (queda en revisión). Solo unity_dev/staff
        /// (lo gatea la RPC submit_unity_build). Devuelve el id del build o error.
        /// </summary>
        public static async Task<(string id, string error)> SubmitUnityBuild(string reMemoryId, JObject build)
        {
            if (!IsLoggedIn) return (null, "Inicia sesión primero.");
            var body = new JObject { ["p_re_memory_id"] = reMemoryId, ["p_build"] = build }.ToString();
            var (json, err) = await RpcWithRefresh("submit_unity_build", body);
            if (err != null) return (null, err);
            try
            {
                var token = JToken.Parse(json);
                return (token?.Value<string>(), null); // la RPC devuelve el uuid como JSON string
            }
            catch (Exception e) { return (null, e.Message); }
        }
    }

    /// <summary>Resumen de un Addressable aprobado de una pieza (RPC get_unity_build).</summary>
    public class UnityBuildInfo
    {
        public string Id;
        public string CatalogUrl;
        public string EntryKey;
        public string BundlePath;
        public string Status;
        public bool HasScript;
        public bool Stale;
    }

    /// <summary>Estado de recreación de una pieza para la lista de Herramientas.</summary>
    public class UnityBuildState
    {
        public bool HasModel;  // hay modelo 3D aprobado
        public bool HasBuild;  // hay Addressable aprobado (recreación)
        public bool Stale;     // hay modelo nuevo y el Addressable está desactualizado
    }

    /// <summary>Build pendiente de revisión (cola del revisor).</summary>
    public class PendingBuild
    {
        public string Id;
        public string ReMemoryId;
        public string ReMemoryName;
        public string BuildTarget;
        public string CatalogUrl;
        public string EntryKey;
        public bool HasScript;
        public string ScriptStatus;
        public string BuiltBy;
        public string CreatedAt;
    }
}
