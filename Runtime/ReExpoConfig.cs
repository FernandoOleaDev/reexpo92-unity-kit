namespace ReExpo92.WorldKit
{
    /// <summary>
    /// Configuración pública del proyecto re-Expo92. NADA de esto es secreto:
    /// la URL y la anon key de Supabase son públicas por diseño (la seguridad
    /// real la imponen las políticas RLS del servidor, igual que en la web).
    /// La clave de Google Map Tiles NO vive aquí: se descarga al loguearse,
    /// ofuscada, vía la RPC get_my_unity_key (ver <see cref="ReExpo92.WorldKit.Supa"/>).
    /// </summary>
    public static class ReExpoConfig
    {
        /// URL del proyecto Supabase (pública).
        public const string SupabaseUrl = "https://wmygankuaoqqhnqqycvs.supabase.co";

        /// Clave anónima de Supabase (PÚBLICA por diseño — protegida por RLS).
        public const string SupabaseAnonKey = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZSIsInJlZiI6IndteWdhbmt1YW9xcWhucXF5Y3ZzIiwicm9sZSI6ImFub24iLCJpYXQiOjE3NTEwMTkyMzYsImV4cCI6MjA2NjU5NTIzNn0.kDd6P9TsQE-f7GYxHeftqu2yJ01pa1DXJ4FeZXu4FHs";

        // ----- Fundación geodésica del recinto -----
        // DEBE coincidir EXACTAMENTE con src/lib/geo.ts (RECINTO_ORIGIN) y con la
        // RPC export_map_geojson del backend. Convención: 1 unidad Unity = 1 metro.
        public const double RecintoLat = 37.4055;
        public const double RecintoLng = -6.0035;

        /// Altura por defecto (m) del origen y de los marcadores sobre el terreno.
        public const double DefaultHeight = 5.0;

        // ----- Entrega de la clave de Google -----
        /// Passphrase de la ofuscación XOR+base64 (anti-casual, NO es seguridad).
        /// Debe coincidir con la función _obfuscate del backend.
        public const string ObfuscationPass = "reExpo92";

        /// Plantilla de la URL del tileset de Google Photorealistic 3D Tiles.
        public const string GoogleTilesUrlTemplate =
            "https://tile.googleapis.com/v1/3dtiles/root.json?key={0}";

        /// Nombre de la skill que autoriza a descargar la clave de Google.
        public const string UnityDevSkill = "unity_dev";
    }
}
