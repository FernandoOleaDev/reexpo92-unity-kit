using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace ReExpo92.WorldKit
{
    /// <summary>Una re-memoria del catálogo (para listarlas y descargarlas).</summary>
    public class ReMemoryItem
    {
        public string Id;
        public string Name;
        public string Status;
        public string Category;
        public string Description;
        public string ImageUrl;
    }

    /// <summary>Punto de interés (re-memoria situada en el mapa).</summary>
    public class MapPoi
    {
        public string ReMemoryId;
        public string Name;
        public double Lat;
        public double Lng;
        public double? Heading;
        public string Status;
        public string Category;
    }

    /// <summary>Zona del recinto (polígono de coordenadas WGS84).</summary>
    public class MapZone
    {
        public string Value;
        public readonly List<(double lat, double lng)> Ring = new List<(double, double)>();
    }

    /// <summary>Resultado de export_map_geojson ya parseado.</summary>
    public class MapData
    {
        public double OriginLat = ReExpoConfig.RecintoLat;
        public double OriginLng = ReExpoConfig.RecintoLng;
        public readonly List<MapPoi> Pois = new List<MapPoi>();
        public readonly List<MapZone> Zones = new List<MapZone>();
    }

    /// <summary>
    /// Parser del FeatureCollection que emite la RPC export_map_geojson.
    /// Recuerda: las coordenadas GeoJSON van en orden [lng, lat].
    /// </summary>
    public static class GeoJsonParser
    {
        public static MapData Parse(string json)
        {
            var data = new MapData();
            if (string.IsNullOrEmpty(json)) return data;

            var root = JToken.Parse(json) as JObject;
            if (root == null) return data;

            var origin = root["recinto_origin"] as JObject;
            if (origin != null)
            {
                data.OriginLat = origin.Value<double?>("lat") ?? data.OriginLat;
                data.OriginLng = origin.Value<double?>("lng") ?? data.OriginLng;
            }

            var features = root["features"] as JArray;
            if (features == null) return data;

            foreach (var f in features)
            {
                var props = f["properties"] as JObject;
                var geom = f["geometry"] as JObject;
                if (props == null || geom == null) continue;

                var kind = props.Value<string>("kind");
                var gtype = geom.Value<string>("type");
                var coords = geom["coordinates"] as JArray;
                if (coords == null) continue;

                if (kind == "poi" && gtype == "Point")
                {
                    data.Pois.Add(new MapPoi
                    {
                        ReMemoryId = props.Value<string>("re_memory_id"),
                        Name = props.Value<string>("name"),
                        Heading = props.Value<double?>("heading"),
                        Status = props.Value<string>("status"),
                        Category = props.Value<string>("category"),
                        Lng = coords.Count > 0 ? (double)coords[0] : 0,
                        Lat = coords.Count > 1 ? (double)coords[1] : 0,
                    });
                }
                else if (kind == "zone" && gtype == "Polygon")
                {
                    // Polygon coordinates = [ ring0, ring1, ... ]; usamos el exterior (ring0).
                    var ring = coords.Count > 0 ? coords[0] as JArray : null;
                    if (ring == null) continue;
                    var z = new MapZone { Value = props.Value<string>("value") };
                    foreach (var pt in ring)
                    {
                        var p = pt as JArray;
                        if (p == null || p.Count < 2) continue;
                        z.Ring.Add(((double)p[1], (double)p[0])); // (lat, lng)
                    }
                    if (z.Ring.Count >= 3) data.Zones.Add(z);
                }
            }
            return data;
        }
    }
}
