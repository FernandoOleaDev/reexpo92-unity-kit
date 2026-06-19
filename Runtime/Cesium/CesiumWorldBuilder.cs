#if REEXPO_CESIUM
using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using Unity.Mathematics;
using UnityEngine;
using CesiumForUnity;

namespace ReExpo92.WorldKit.Cesium
{
    /// <summary>
    /// Construye el rig georreferenciado con Cesium for Unity:
    /// CesiumGeoreference en el origen del recinto + Google Photorealistic 3D
    /// Tiles (referencia de editor, limitado a la Cartuja) + marcadores anclados
    /// (CesiumGlobeAnchor) para POIs (pin con bola Expo) y zonas.
    ///
    /// Las alturas se AJUSTAN al terreno real muestreando los tiles de Google
    /// (SampleHeightMostDetailed), así cada POI/zona queda exactamente a ras de
    /// suelo (no a una altura estimada).
    /// </summary>
    public class CesiumWorldBuilder : IWorldBuilder
    {
        static readonly Color PoiColor = new Color(1f, 0.42f, 0.21f);

        public string Name => "Cesium + Google Photorealistic 3D Tiles";

        public GameObject Build(WorldBuildOptions o)
        {
            o = o ?? new WorldBuildOptions();
            double originLat = o.Data?.OriginLat ?? ReExpoConfig.RecintoLat;
            double originLng = o.Data?.OriginLng ?? ReExpoConfig.RecintoLng;

            var root = new GameObject("ReExpo92 Rig");
            var georef = root.AddComponent<CesiumGeoreference>();
            georef.SetOriginLongitudeLatitudeHeight(originLng, originLat, ReExpoConfig.GroundHeightMeters);

            Cesium3DTileset tileset = null;

            // --- Maqueta fotorrealista de Google (referencia de editor) ---
            if (o.ShowGoogleTiles && !string.IsNullOrEmpty(o.GoogleApiKey))
            {
                var tilesGO = new GameObject("Google Photorealistic 3D Tiles");
                tilesGO.transform.SetParent(root.transform, false);
                tileset = tilesGO.AddComponent<Cesium3DTileset>();
                tileset.tilesetSource = CesiumDataSource.FromUrl;
                tileset.url = string.Format(ReExpoConfig.GoogleTilesUrlTemplate, o.GoogleApiKey);
                tileset.showCreditsOnScreen = true; // OBLIGATORIO por los términos de Google
                tileset.updateInEditor = true;

                // Limita la carga a la Cartuja + alrededores.
                var box = tilesGO.AddComponent<BoxCollider>();
                box.isTrigger = true;
                box.center = Vector3.zero;
                box.size = new Vector3(ReExpoConfig.WorldBoxSizeMeters,
                                       ReExpoConfig.WorldBoxHeightMeters,
                                       ReExpoConfig.WorldBoxSizeMeters);
                tilesGO.AddComponent<ReExpoBoxExcluder>();
            }

            var poiMat = MakeMat(PoiColor);
            var bolaMat = MakeBolaMat() ?? poiMat;

            // recopilación para ajustar alturas al terreno
            var samples = new List<(CesiumGlobeAnchor anchor, double lng, double lat, double offset)>();
            var zoneRecords = new List<(Mesh mesh, Transform owner, List<CesiumGlobeAnchor> verts, ReExpoZoneTicker ticker, string label)>();

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
                    // Cota REAL del suelo (Google Elevation, cacheada en BD). Si no
                    // hay, se queda en la altura base y se muestrea la malla (fallback).
                    double poiH = p.GroundEllip ?? ReExpoConfig.GroundHeightMeters;
                    anchor.SetPositionLongitudeLatitudeHeight(p.Lng, p.Lat, poiH);
                    if (p.Heading.HasValue)
                        go.transform.localRotation = Quaternion.Euler(0f, (float)p.Heading.Value, 0f);
                    BuildPin(go.transform, poiMat, bolaMat);
                    AddLabel(go.transform, p.Name ?? p.ReMemoryId);
                    var pref = go.AddComponent<ReExpoPoiRef>();
                    pref.ReMemoryId = p.ReMemoryId; pref.PoiName = p.Name; pref.Category = p.Category;
                    if (!p.GroundEllip.HasValue)
                        samples.Add((anchor, p.Lng, p.Lat, 0.0)); // sin cota cacheada → muestrear la malla
                }
            }

            // --- Zonas (polígonos del recinto) ---
            if (o.ShowZones && o.Data?.Zones != null && o.Data.Zones.Count > 0)
            {
                var zoneRoot = new GameObject("Zonas");
                zoneRoot.transform.SetParent(root.transform, false);
                foreach (var z in o.Data.Zones)
                {
                    var ring = z.Ring;
                    int n = ring.Count;
                    if (n >= 2 && System.Math.Abs(ring[0].lat - ring[n - 1].lat) < 1e-9
                                && System.Math.Abs(ring[0].lng - ring[n - 1].lng) < 1e-9) n--; // quita el cierre duplicado
                    if (n < 3) continue;

                    var zgo = new GameObject("Zona · " + z.Value);
                    zgo.transform.SetParent(zoneRoot.transform, false);
                    var mf = zgo.AddComponent<MeshFilter>();
                    var mr = zgo.AddComponent<MeshRenderer>();
                    mr.sharedMaterial = MakeZoneVolumeMat(ZoneColorFor(z.Value));
                    mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    mr.receiveShadows = false;

                    var verts = new List<CesiumGlobeAnchor>();
                    for (int i = 0; i < n; i++)
                    {
                        var v = ring[i];
                        var vgo = new GameObject("v" + i);
                        vgo.transform.SetParent(zgo.transform, false);
                        var a = vgo.AddComponent<CesiumGlobeAnchor>();
                        double vh = v.ground ?? ReExpoConfig.GroundHeightMeters; // cota real cacheada o fallback
                        a.SetPositionLongitudeLatitudeHeight(v.lng, v.lat, vh);
                        verts.Add(a);
                        if (!v.ground.HasValue)
                            samples.Add((a, v.lng, v.lat, 0.0)); // sin cota → muestrear la malla
                    }
                    var mesh = new Mesh { name = "ZoneVolume" };
                    mf.sharedMesh = mesh;
                    BuildZoneMesh(mesh, zgo.transform, verts, ReExpoConfig.ZoneHeightMeters);
                    var ticker = BuildZoneTicker(zgo.transform, verts, z.Value, ZoneColorFor(z.Value));
                    zoneRecords.Add((mesh, zgo.transform, verts, ticker, z.Value));
                }
            }

            // --- Ajuste de alturas al terreno real (async, si hay tiles) ---
            if (tileset != null && samples.Count > 0)
                AdjustHeightsAsync(tileset, samples, zoneRecords);

            return root;
        }

        /// Muestrea la altura del terreno de Google y recoloca POIs/zonas a ras.
        static async void AdjustHeightsAsync(
            Cesium3DTileset tileset,
            List<(CesiumGlobeAnchor anchor, double lng, double lat, double offset)> samples,
            List<(Mesh mesh, Transform owner, List<CesiumGlobeAnchor> verts, ReExpoZoneTicker ticker, string label)> zones)
        {
            if (tileset == null || samples.Count == 0) return;
            var positions = new double3[samples.Count];
            for (int i = 0; i < samples.Count; i++)
                positions[i] = new double3(samples[i].lng, samples[i].lat, 0);

            CesiumSampleHeightResult res;
            try { res = await tileset.SampleHeightMostDetailed(positions); }
            catch { return; }
            if (res?.longitudeLatitudeHeightPositions == null) return;

            for (int i = 0; i < samples.Count && i < res.longitudeLatitudeHeightPositions.Length; i++)
            {
                var s = samples[i];
                if (s.anchor == null) continue;
                bool ok = res.sampleSuccess != null && i < res.sampleSuccess.Length && res.sampleSuccess[i];
                double h = ok ? res.longitudeLatitudeHeightPositions[i].z : ReExpoConfig.GroundHeightMeters;
                s.anchor.SetPositionLongitudeLatitudeHeight(s.lng, s.lat, h + s.offset);
            }

            // reconstruir los volúmenes de zona (y el cartel LED) tras mover los
            // vértices al terreno
            foreach (var z in zones)
            {
                BuildZoneMesh(z.mesh, z.owner, z.verts, ReExpoConfig.ZoneHeightMeters);
                if (z.ticker != null)
                    z.ticker.Configure(TransformsOf(z.verts), z.owner, z.label, ReExpoConfig.ZoneHeightMeters, ZoneColorFor(z.label));
            }
        }

        /// Crea el cartel LED (TextMeshPro + ReExpoZoneTicker) que recorre el muro
        /// de la zona repitiendo su nombre, emisivo y en movimiento.
        static ReExpoZoneTicker BuildZoneTicker(Transform owner, List<CesiumGlobeAnchor> verts, string label, Color color)
        {
            var go = new GameObject("LED", typeof(RectTransform));
            go.transform.SetParent(owner, false);
            var rt = go.GetComponent<RectTransform>();
            rt.localPosition = Vector3.zero; rt.localRotation = Quaternion.identity; rt.localScale = Vector3.one;
            rt.sizeDelta = new Vector2(10f, 2f);

            var tmp = go.AddComponent<TextMeshPro>();
            tmp.fontSize = 18f; // tamaño en espacio de texto; el real lo fija ReExpoTickerConfig.LetterMeters

            var ticker = go.AddComponent<ReExpoZoneTicker>();
            ticker.Configure(TransformsOf(verts), owner, label, ReExpoConfig.ZoneHeightMeters, color);
            return ticker;
        }

        static List<Transform> TransformsOf(List<CesiumGlobeAnchor> verts)
        {
            var ts = new List<Transform>(verts.Count);
            foreach (var a in verts) ts.Add(a != null ? a.transform : null);
            return ts;
        }

        /// Malla del volumen de una zona: prisma extruido (paredes) desde los
        /// vértices (a ras de suelo) hasta +height. Doble cara (material Cull Off).
        static void BuildZoneMesh(Mesh mesh, Transform owner, List<CesiumGlobeAnchor> verts, float height)
        {
            int n = verts.Count;
            if (mesh == null || owner == null || n < 3) { if (mesh != null) mesh.Clear(); return; }
            var vv = new Vector3[n * 2];
            for (int i = 0; i < n; i++)
            {
                Vector3 w = verts[i] != null ? verts[i].transform.position : Vector3.zero;
                vv[2 * i] = owner.InverseTransformPoint(w);
                vv[2 * i + 1] = owner.InverseTransformPoint(w + Vector3.up * height);
            }
            var tris = new int[n * 6];
            int t = 0;
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                int i0 = 2 * i, i1 = 2 * i + 1, j0 = 2 * j, j1 = 2 * j + 1;
                tris[t++] = i0; tris[t++] = j0; tris[t++] = j1;
                tris[t++] = i0; tris[t++] = j1; tris[t++] = i1;
            }
            mesh.Clear();
            mesh.vertices = vv;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
        }

        /// Material translúcido de zona (URP/Unlit transparente, doble cara).
        static Material MakeZoneVolumeMat(Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            var m = new Material(shader);
            color.a = 0.22f;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Color")) m.SetColor("_Color", color);
            m.color = color;
            // transparencia + doble cara (propiedades estándar de URP)
            if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f);
            if (m.HasProperty("_Blend")) m.SetFloat("_Blend", 0f);
            if (m.HasProperty("_SrcBlend")) m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (m.HasProperty("_DstBlend")) m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            if (m.HasProperty("_ZWrite")) m.SetFloat("_ZWrite", 0f);
            if (m.HasProperty("_Cull")) m.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            return m;
        }

        /// Material del fondo del cartel (azul translúcido, doble cara, tras el texto).
        static Material MakeLabelBgMat()
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            var m = new Material(shader);
            var c = new Color(0.106f, 0.165f, 0.42f, 0.8f);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
            m.color = c;
            if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f);
            if (m.HasProperty("_SrcBlend")) m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (m.HasProperty("_DstBlend")) m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            if (m.HasProperty("_ZWrite")) m.SetFloat("_ZWrite", 0f);
            if (m.HasProperty("_Cull")) m.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent - 1; // detrás del texto
            return m;
        }

        /// Color (estable) por zona, derivado de su nombre.
        static Color ZoneColorFor(string value)
        {
            int h = string.IsNullOrEmpty(value) ? 0 : (value.GetHashCode() & 0x7fffffff);
            return Color.HSVToRGB((h % 360) / 360f, 0.65f, 0.95f);
        }

        /// Pin tipo chincheta: poste desde el 0 local (a ras) + bola Expo encima.
        static void BuildPin(Transform parent, Material poleMat, Material headMat)
        {
            var pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pole.name = "Pole";
            StripCollider(pole);
            pole.transform.SetParent(parent, false);
            pole.transform.localScale = new Vector3(0.8f, 6f, 0.8f); // 12 m de alto (mesh = 2 u)
            pole.transform.localPosition = new Vector3(0f, 6f, 0f);  // base en y=0
            pole.GetComponent<MeshRenderer>().sharedMaterial = poleMat;

            var head = new GameObject("Head");
            head.transform.SetParent(parent, false);
            head.transform.localScale = Vector3.one * 7f;             // diámetro 7 m
            head.transform.localPosition = new Vector3(0f, 15.5f, 0f); // apoyada sobre el poste (top=12 + radio 3.5)
            head.AddComponent<MeshFilter>().sharedMesh = BolaMesh();   // UV sphere de alta resolución
            head.AddComponent<MeshRenderer>().sharedMaterial = headMat;
            head.AddComponent<ReExpoBillboardBall>(); // mira a cámara + inclinación
        }

        static void StripCollider(GameObject go)
        {
            var col = go.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col);
        }

        /// Cartel 3D (TextMeshPro) hijo del POI, con billboard. Sale a la derecha
        /// de la bola (offset configurable) y se oculta tras zonas/edificios.
        static void AddLabel(Transform parent, string text)
        {
            var go = new GameObject("Label");
            go.transform.SetParent(parent, false);
            var tmp = go.AddComponent<TextMeshPro>();
            tmp.text = text ?? "";
            tmp.fontSize = ReExpoLabelConfig.LabelFontSize;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Left;
            tmp.fontStyle = FontStyles.Bold;
            tmp.enableWordWrapping = false;
            tmp.overflowMode = TextOverflowModes.Overflow;
            tmp.rectTransform.pivot = new Vector2(0f, 0.5f); // crece hacia la derecha
            tmp.rectTransform.sizeDelta = new Vector2(120f, 14f);
            tmp.outlineColor = new Color32(20, 30, 80, 255);
            tmp.outlineWidth = 0.2f;

            // fondo: quad azul translúcido detrás del texto, a su tamaño
            tmp.ForceMeshUpdate();
            var b = tmp.textBounds;
            var bg = GameObject.CreatePrimitive(PrimitiveType.Quad);
            bg.name = "Bg";
            StripCollider(bg);
            bg.transform.SetParent(go.transform, false);
            bg.transform.localPosition = new Vector3(b.center.x, b.center.y, 0.5f); // detrás del texto
            bg.transform.localScale = new Vector3(b.size.x + 4f, b.size.y + 2f, 1f);
            bg.GetComponent<MeshRenderer>().sharedMaterial = MakeLabelBgMat();

            go.AddComponent<ReExpoBillboardLabel>();
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

        static Mesh _bolaMesh;
        /// UV sphere de ALTA resolución con UVs equirectangulares: la textura (con
        /// polos) se mapea suave, sin los parches que da la esfera de baja malla.
        static Mesh BolaMesh()
        {
            if (_bolaMesh != null) return _bolaMesh;
            const int lon = 64, lat = 40;
            int stride = lon + 1;
            var verts = new Vector3[stride * (lat + 1)];
            var uvs = new Vector2[verts.Length];
            var norms = new Vector3[verts.Length];
            for (int y = 0; y <= lat; y++)
            {
                float v = (float)y / lat;
                float theta = v * Mathf.PI;
                float sinT = Mathf.Sin(theta), cosT = Mathf.Cos(theta);
                for (int x = 0; x <= lon; x++)
                {
                    float u = (float)x / lon;
                    float phi = u * 2f * Mathf.PI;
                    var p = new Vector3(sinT * Mathf.Cos(phi), cosT, sinT * Mathf.Sin(phi));
                    int i = y * stride + x;
                    verts[i] = p * 0.5f; // diámetro 1
                    norms[i] = p;
                    uvs[i] = new Vector2(u, 1f - v);
                }
            }
            var tris = new int[lon * lat * 6];
            int t = 0;
            for (int y = 0; y < lat; y++)
                for (int x = 0; x < lon; x++)
                {
                    int i0 = y * stride + x, i1 = i0 + 1, i2 = i0 + stride, i3 = i2 + 1;
                    tris[t++] = i0; tris[t++] = i2; tris[t++] = i1;
                    tris[t++] = i1; tris[t++] = i2; tris[t++] = i3;
                }
            _bolaMesh = new Mesh { name = "BolaUVSphere" };
            _bolaMesh.vertices = verts;
            _bolaMesh.normals = norms;
            _bolaMesh.uv = uvs;
            _bolaMesh.triangles = tris;
            _bolaMesh.RecalculateBounds();
            return _bolaMesh;
        }

        /// Material de la bola Expo: textura (Resources) sobre URP/Lit, smoothness 0.
        static Material MakeBolaMat()
        {
            var tex = Resources.Load<Texture2D>("ReExpo92/bola-expo");
            if (tex == null) return null;
            tex.wrapMode = TextureWrapMode.Repeat; // necesario para tiling > 1
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (shader == null) return null;
            var m = new Material(shader);
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
            if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", tex);
            m.mainTexture = tex;
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0f);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0f);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);

            // Tiling del material 1.635 en X (en base y emisión).
            var tiling = new Vector2(1.635f, 1f);
            if (m.HasProperty("_BaseMap")) m.SetTextureScale("_BaseMap", tiling);
            if (m.HasProperty("_MainTex")) m.SetTextureScale("_MainTex", tiling);
            m.mainTextureScale = tiling;

            // Emisión: misma textura + color HDR R191 G35 B0 a intensidad 3.9963 EV.
            // (color final = colorLDR * 2^intensidad, como hace el selector HDR de Unity)
            var emissive = new Color(191f / 255f, 35f / 255f, 0f) * Mathf.Pow(2f, 3.9963f);
            if (m.HasProperty("_EmissionMap")) { m.SetTexture("_EmissionMap", tex); m.SetTextureScale("_EmissionMap", tiling); }
            if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", emissive);
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
            return m;
        }
    }
}
#endif
