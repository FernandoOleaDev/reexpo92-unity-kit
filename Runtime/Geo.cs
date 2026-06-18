using System;
using System.Text;

namespace ReExpo92.WorldKit
{
    /// <summary>
    /// Fundación geodésica (puerto C# de src/lib/geo.ts). Todo lo persistido está
    /// en WGS84 (lat/lng reales); para Unity se proyecta a un plano tangente local
    /// ENU en metros respecto a <see cref="ReExpoConfig.RecintoLat"/>/Lng, a escala
    /// 1 unidad = 1 metro. Es la MISMA fórmula que la web y la que replicará el
    /// importer/AR, para que una coordenada caiga siempre en el mismo sitio.
    /// </summary>
    public static class Geo
    {
        const double EarthR = 6378137.0; // radio WGS84 en metros
        const double Deg = Math.PI / 180.0;

        /// Proyecta (lat,lng) -> metros locales ENU (este, norte) respecto al origen.
        public static (double east, double north) ToLocalMeters(
            double lat, double lng, double originLat, double originLng)
        {
            double east = (lng - originLng) * Deg * EarthR * Math.Cos(originLat * Deg);
            double north = (lat - originLat) * Deg * EarthR;
            return (east, north);
        }

        /// Inverso: metros locales ENU -> (lat,lng).
        public static (double lat, double lng) FromLocalMeters(
            double east, double north, double originLat, double originLng)
        {
            double lat = originLat + north / (Deg * EarthR);
            double lng = originLng + east / (Deg * EarthR * Math.Cos(originLat * Deg));
            return (lat, lng);
        }

        /// <summary>
        /// Revierte la ofuscación XOR+base64 del backend (_obfuscate). Anti-casual,
        /// NO es seguridad real: el seguro es el tope de gasto en Google Cloud.
        /// </summary>
        public static string Deobfuscate(string b64, string pass = null)
        {
            if (string.IsNullOrEmpty(b64)) return null;
            pass = pass ?? ReExpoConfig.ObfuscationPass;
            byte[] data = Convert.FromBase64String(b64);
            byte[] p = Encoding.UTF8.GetBytes(pass);
            for (int i = 0; i < data.Length; i++)
                data[i] = (byte)(data[i] ^ p[i % p.Length]);
            return Encoding.UTF8.GetString(data);
        }
    }
}
