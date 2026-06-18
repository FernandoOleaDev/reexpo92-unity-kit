#if REEXPO_CESIUM
using UnityEngine;
using CesiumForUnity;

namespace ReExpo92.WorldKit.Cesium
{
    /// <summary>
    /// Construye el rig georreferenciado con Cesium for Unity:
    /// CesiumGeoreference en el origen del recinto + Google Photorealistic 3D
    /// Tiles (FromUrl con la clave) como referencia de editor + marcadores
    /// anclados (CesiumGlobeAnchor) para POIs y zonas reales de re-Expo92.
    ///
    /// Este tipo se compila SOLO si está instalado com.cesium.unity (el define
    /// REEXPO_CESIUM lo activa el versionDefine del asmdef). El editor lo localiza
    /// por reflexión vía <see cref="WorldBuilderLocator"/>.
    /// </summary>
    public class CesiumWorldBuilder : IWorldBuilder
    {
        // Paleta Expo 92.
        static readonly Color PoiColor = new Color(1f, 0.42f, 0.21f);   // naranja
        static readonly Color ZoneColor = new Color(0.18f, 0.27f, 0.62f); // azul

        public string Name => "Cesium + Google Photorealistic 3D Tiles";

        public GameObject Build(WorldBuildOptions o)
        {
            o = o ?? new WorldBuildOptions();
            double originLat = o.Data?.OriginLat ?? ReExpoConfig.RecintoLat;
            double originLng = o.Data?.OriginLng ?? ReExpoConfig.RecintoLng;

            var root = new GameObject("ReExpo92 Rig");

            // --- Georreferencia en el origen del recinto (1u = 1m) ---
            var georef = root.AddComponent<CesiumGeoreference>();
            georef.SetOriginLongitudeLatitudeHeight(originLng, originLat, ReExpoConfig.DefaultHeight);

            // --- Maqueta fotorrealista de Google (referencia de editor) ---
            if (o.ShowGoogleTiles && !string.IsNullOrEmpty(o.GoogleApiKey))
            {
                var tilesGO = new GameObject("Google Photorealistic 3D Tiles");
                tilesGO.transform.SetParent(root.transform, false);
                var tileset = tilesGO.AddComponent<Cesium3DTileset>();
                tileset.tilesetSource = CesiumDataSource.FromUrl;
                tileset.url = string.Format(ReExpoConfig.GoogleTilesUrlTemplate, o.GoogleApiKey);
                tileset.showCreditsOnScreen = true; // OBLIGATORIO por los términos de Google
                tileset.updateInEditor = true;      // cargar en modo edición (referencia)
            }

            var poiMat = MakeMat(PoiColor);
            var zoneMat = MakeMat(ZoneColor);

            // --- POIs (re-memorias situadas) ---
            if (o.ShowPois && o.Data?.Pois != null && o.Data.Pois.Count > 0)
            {
                var poiRoot = new GameObject("POIs");
                poiRoot.transform.SetParent(root.transform, false);
                foreach (var p in o.Data.Pois)
                {
                    var go = new GameObject("POI · " + (p.Name ?? p.ReMemoryId));
                    go.transform.SetParent(poiRoot.transform, false);
                    var anchor = go.AddComponent<CesiumGlobeAnchor>();
                    anchor.SetPositionLongitudeLatitudeHeight(p.Lng, p.Lat, ReExpoConfig.DefaultHeight);
                    if (p.Heading.HasValue)
                        go.transform.localRotation = Quaternion.Euler(0f, (float)p.Heading.Value, 0f);
                    BuildPin(go.transform, poiMat);
                }
            }

            // --- Zonas (polígonos del recinto) ---
            if (o.ShowZones && o.Data?.Zones != null && o.Data.Zones.Count > 0)
            {
                var zoneRoot = new GameObject("Zonas");
                zoneRoot.transform.SetParent(root.transform, false);
                foreach (var z in o.Data.Zones)
                {
                    var zgo = new GameObject("Zona · " + z.Value);
                    zgo.transform.SetParent(zoneRoot.transform, false);

                    // Cada vértice como hijo anclado a su lat/lng real; trazamos el
                    // contorno con un LineRenderer en espacio de mundo (robusto, sin
                    // asumir la orientación local del anclaje).
                    var line = zgo.AddComponent<LineRenderer>();
                    line.useWorldSpace = true;
                    line.loop = true;
                    line.widthMultiplier = 4f;
                    line.numCapVertices = 2;
                    line.sharedMaterial = zoneMat;
                    line.startColor = line.endColor = ZoneColor;
                    line.positionCount = z.Ring.Count;
                    for (int i = 0; i < z.Ring.Count; i++)
                    {
                        var v = z.Ring[i];
                        var vgo = new GameObject("v" + i);
                        vgo.transform.SetParent(zgo.transform, false);
                        var a = vgo.AddComponent<CesiumGlobeAnchor>();
                        a.SetPositionLongitudeLatitudeHeight(v.lng, v.lat, ReExpoConfig.DefaultHeight);
                        line.SetPosition(i, vgo.transform.position);
                    }
                }
            }

            return root;
        }

        /// Pin tipo chincheta: poste fino + bola, anclado por su base.
        static void BuildPin(Transform parent, Material mat)
        {
            var pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pole.name = "Pole";
            StripCollider(pole);
            pole.transform.SetParent(parent, false);
            pole.transform.localScale = new Vector3(0.8f, 6f, 0.8f); // ~12 m de alto
            pole.transform.localPosition = new Vector3(0f, 12f, 0f);
            pole.GetComponent<MeshRenderer>().sharedMaterial = mat;

            var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "Head";
            StripCollider(head);
            head.transform.SetParent(parent, false);
            head.transform.localScale = Vector3.one * 7f;
            head.transform.localPosition = new Vector3(0f, 26f, 0f);
            head.GetComponent<MeshRenderer>().sharedMaterial = mat;
        }

        static void StripCollider(GameObject go)
        {
            var col = go.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col);
        }

        /// Material no iluminado con color, robusto entre pipelines.
        static Material MakeMat(Color c)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit")
                      ?? Shader.Find("Unlit/Color")
                      ?? Shader.Find("Sprites/Default");
            var m = new Material(shader);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
            m.color = c;
            return m;
        }
    }
}
#endif
