// Ventana guiada «Constructor de Addressables»: elegir pieza → bajar modelo →
// validar (avisos + auto-fix) → crear Addressable (grupo remoto auto) → construir
// y subir → (opcional) script como TextAsset. Requiere com.unity.addressables
// (asmdef con defineConstraint REEXPO_ADDR). IMGUI por robustez.
//
// ⚠️ Best-effort sin compilar en el agente: la API de Addressables-editor varía
// entre versiones; validar al abrir Unity. Lo más sensible es ConfigureAndMark().
#if REEXPO_ADDR
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;
using ReExpo92.WorldKit;

namespace ReExpo92.WorldKit.Editor
{
    public class AddressableBuilderWindow : EditorWindow
    {
        const string GroupName = "ReExpo92 Remote";
        const string DownloadsDir = "Assets/ReExpo92/Downloads";

        [MenuItem("re-Expo92/Constructor de Addressables")]
        public static void Open()
        {
            var w = GetWindow<AddressableBuilderWindow>("Addressables · re-Expo92");
            w.minSize = new Vector2(420, 560);
        }

        string _reMemoryId = "";
        string _reMemoryName = "";
        GameObject _target;
        TextAsset _script;
        Vector2 _scroll;

        // selector de pieza
        bool _pickerOpen;
        List<ReMemoryItem> _items;
        Vector2 _pickerScroll;
        string _pickerFilter = "";

        // estado del build existente de la pieza
        UnityBuildInfo _existing;
        bool _existingChecked;

        readonly List<Check> _checks = new List<Check>();
        string _status = "";
        string _statusKind = ""; // "", "busy", "ok", "err"
        bool _busy;

        struct Check { public Severity sev; public string label; public string fixLabel; public Action fix; }
        enum Severity { Ok, Warn, Fail }

        void OnEnable() => ReExpoEditorService.Restore();

        void SetStatus(string kind, string text) { _statusKind = kind; _status = text; Repaint(); }

        void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            EditorGUILayout.LabelField("CONSTRUCTOR DE ADDRESSABLES", EditorStyles.boldLabel);

            if (!ReExpoEditorService.IsLoggedIn)
            {
                EditorGUILayout.HelpBox("Inicia sesión en el Panel de control de re-Expo92 antes de subir Addressables.", MessageType.Warning);
                if (GUILayout.Button("Abrir Panel de control"))
                    EditorApplication.ExecuteMenuItem("re-Expo92/Panel de control");
                EditorGUILayout.EndScrollView();
                return;
            }

            EditorGUILayout.HelpBox(
                "Empaqueta una pieza para el recinto (Unity WebGL). Baja el modelo aprobado o usa tu propio prefab, " +
                "valida, crea el Addressable y súbelo a revisión. Los scripts viajan como TextAsset; el package no ejecuta código.",
                MessageType.Info);

            DrawPieceSection();
            DrawTargetSection();
            DrawValidateSection();
            DrawScriptSection();
            DrawBuildSection();

            if (!string.IsNullOrEmpty(_status))
            {
                var c = _statusKind == "err" ? Color.red : _statusKind == "ok" ? new Color(0.1f, 0.5f, 0.2f) : Color.gray;
                var prev = GUI.color; GUI.color = c;
                EditorGUILayout.LabelField(_status, EditorStyles.wordWrappedMiniLabel);
                GUI.color = prev;
            }
            EditorGUILayout.EndScrollView();
        }

        // ---- 1 · Pieza ----
        void DrawPieceSection()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("1 · Pieza (re-memoria)", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                _reMemoryId = EditorGUILayout.TextField("re_memory_id", _reMemoryId);
                if (GUILayout.Button(_pickerOpen ? "Cerrar" : "Elegir…", GUILayout.Width(70)))
                {
                    _pickerOpen = !_pickerOpen;
                    if (_pickerOpen && _items == null) LoadPieces();
                }
            }
            if (!string.IsNullOrEmpty(_reMemoryName))
                EditorGUILayout.LabelField("   " + _reMemoryName, EditorStyles.miniLabel);

            if (_pickerOpen) DrawPicker();

            if (_existingChecked && !string.IsNullOrEmpty(_reMemoryId))
            {
                if (_existing == null)
                    EditorGUILayout.HelpBox("Esta pieza aún no tiene Addressable. Vas a crear el primero.", MessageType.None);
                else if (_existing.Stale)
                    EditorGUILayout.HelpBox("Hay un Addressable aprobado pero está DESACTUALIZADO (modelo nuevo). Reconstrúyelo.", MessageType.Warning);
                else
                    EditorGUILayout.HelpBox("Ya hay un Addressable aprobado y al día. Si lo reconstruyes, sustituirá al anterior tras revisión.", MessageType.None);
            }

            using (new EditorGUI.DisabledScope(_busy || string.IsNullOrEmpty(_reMemoryId)))
            {
                if (GUILayout.Button("Bajar modelo aprobado (GLB)"))
                    DownloadGlb();
            }
            // Cualquier unity_dev puede cargar el Addressable YA validado de una pieza
            // (para inspeccionarlo o partir de él al actualizarlo).
            if (_existing != null)
                using (new EditorGUI.DisabledScope(_busy))
                    if (GUILayout.Button("Cargar Addressable aprobado en escena"))
                        LoadApproved();
        }

        async void LoadApproved()
        {
            if (_existing == null) return;
            _busy = true; SetStatus("busy", "Cargando el Addressable aprobado…");
            try
            {
                var cat = UnityEngine.AddressableAssets.Addressables.LoadContentCatalogAsync(_existing.CatalogUrl, false);
                await cat.Task;
                var inst = UnityEngine.AddressableAssets.Addressables.InstantiateAsync(_existing.EntryKey);
                await inst.Task;
                if (inst.Result != null)
                {
                    var root = GameObject.Find("ReExpo92 Loaded") ?? new GameObject("ReExpo92 Loaded");
                    inst.Result.transform.SetParent(root.transform, true);
                    Selection.activeGameObject = inst.Result;
                    SetStatus("ok", "Addressable cargado. Puedes inspeccionarlo o construir una mejora.");
                }
                else SetStatus("err", "El Addressable no devolvió ningún objeto.");
            }
            catch (Exception e) { SetStatus("err", "No se pudo cargar: " + e.Message + " · prueba en modo Play."); }
            finally { _busy = false; Repaint(); }
        }

        void DrawPicker()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            _pickerFilter = EditorGUILayout.TextField("Buscar", _pickerFilter);
            if (_items == null) { EditorGUILayout.LabelField("Cargando…"); }
            else
            {
                _pickerScroll = EditorGUILayout.BeginScrollView(_pickerScroll, GUILayout.Height(160));
                foreach (var it in _items)
                {
                    if (!string.IsNullOrEmpty(_pickerFilter) &&
                        (it.Name == null || it.Name.IndexOf(_pickerFilter, StringComparison.OrdinalIgnoreCase) < 0)) continue;
                    if (GUILayout.Button($"{it.Name}  ·  {it.Category}", EditorStyles.miniButton))
                    {
                        _reMemoryId = it.Id; _reMemoryName = it.Name; _pickerOpen = false;
                        _existing = null; _existingChecked = false;
                        CheckExisting();
                    }
                }
                EditorGUILayout.EndScrollView();
            }
            EditorGUILayout.EndVertical();
        }

        async void LoadPieces()
        {
            var (items, err) = await ReExpoClient.FetchReMemories();
            if (err != null) { SetStatus("err", "No se pudieron cargar las piezas: " + err); return; }
            _items = items; Repaint();
        }

        async void CheckExisting()
        {
            var (b, err) = await ReExpoClient.GetUnityBuild(_reMemoryId);
            _existing = err == null ? b : null;
            _existingChecked = true;
            Repaint();
        }

        async void DownloadGlb()
        {
            SetStatus("busy", "Buscando el modelo aprobado…");
            var (url, err) = await ReExpoClient.GetApprovedGlbUrl(_reMemoryId);
            if (err != null) { SetStatus("err", "Error: " + err); return; }
            if (string.IsNullOrEmpty(url)) { SetStatus("err", "Esta pieza no tiene ninguna versión de modelo aprobada todavía."); return; }
            SetStatus("busy", "Descargando GLB…");
            var (bytes, derr) = await SupabaseRest.Download(url);
            if (derr != null) { SetStatus("err", "Descarga: " + derr); return; }
            Directory.CreateDirectory(DownloadsDir);
            string path = $"{DownloadsDir}/rememoria_{_reMemoryId}.glb";
            File.WriteAllBytes(path, bytes);
            AssetDatabase.Refresh();
            SetStatus("ok", $"Descargado en {path}. glTFast lo importa solo; arrástralo al campo «Objeto» de abajo.");
        }

        // ---- 2 · Objeto ----
        void DrawTargetSection()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("2 · Objeto a empaquetar", EditorStyles.boldLabel);
            _target = (GameObject)EditorGUILayout.ObjectField("Prefab / modelo", _target, typeof(GameObject), false);
            EditorGUILayout.LabelField("   Debe ser un asset del proyecto (prefab o el GLB importado), no un objeto de escena.", EditorStyles.miniLabel);
        }

        // ---- 3 · Validar ----
        void DrawValidateSection()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("3 · Validar", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(_target == null))
                if (GUILayout.Button("Validar (avisos + cómo arreglar)")) RunChecks();

            foreach (var c in _checks)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    var mt = c.sev == Severity.Fail ? MessageType.Error : c.sev == Severity.Warn ? MessageType.Warning : MessageType.Info;
                    EditorGUILayout.HelpBox(c.label, mt);
                    if (c.fix != null && GUILayout.Button(c.fixLabel, GUILayout.Width(150), GUILayout.Height(38))) { c.fix(); RunChecks(); }
                }
            }
        }

        void RunChecks()
        {
            _checks.Clear();
            if (_target == null) { Add(Severity.Fail, "No hay objeto seleccionado."); return; }
            var rends = _target.GetComponentsInChildren<Renderer>(true);
            if (rends.Length == 0) { Add(Severity.Fail, "El objeto no tiene Renderers (¿es el asset correcto?)."); return; }

            var b = rends[0].bounds; foreach (var r in rends) b.Encapsulate(r.bounds);
            if (Mathf.Abs(b.min.y - _target.transform.position.y) > 0.05f)
                Add(Severity.Warn, "El pivote no parece estar en la base.", "Centrar pivote en base", () => CenterPivot(b));

            var size = b.size;
            if (size.magnitude < 0.5f || size.magnitude > 800f)
                Add(Severity.Warn, $"Escala sospechosa: {size.x:F1}×{size.y:F1}×{size.z:F1} m (¿1u=1m?).");

            long tris = 0;
            foreach (var mf in _target.GetComponentsInChildren<MeshFilter>(true))
                if (mf.sharedMesh) tris += mf.sharedMesh.triangles.Length / 3;
            if (tris > 300000) Add(Severity.Fail, $"{tris:N0} triángulos: muy por encima del presupuesto (150k). Abre una tarea «optimizar» en la web.");
            else if (tris > 150000) Add(Severity.Warn, $"{tris:N0} triángulos: supera el presupuesto orientativo (150k).");

            if (_target.GetComponentInChildren<LODGroup>() == null)
                Add(Severity.Warn, "Sin LODs (mejora el rendimiento en el recinto).", "Generar LODGroup", GenerateLods);

            foreach (var r in rends)
                foreach (var m in r.sharedMaterials)
                    if (m != null && m.shader != null && !m.shader.name.Contains("Universal Render Pipeline"))
                    { Add(Severity.Warn, $"Material no-URP: «{m.name}». El recinto usa URP.", "Convertir a URP/Lit", () => ToUrp(r)); break; }

            if (_checks.Count == 0) Add(Severity.Ok, "Todo correcto. Listo para crear el Addressable.");
        }

        void Add(Severity s, string label, string fixLabel = null, Action fix = null)
            => _checks.Add(new Check { sev = s, label = label, fixLabel = fixLabel, fix = fix });

        void CenterPivot(Bounds b)
        {
            var delta = new Vector3(0, _target.transform.position.y - b.min.y, 0);
            foreach (Transform c in _target.transform) c.position += delta;
        }
        void GenerateLods()
        {
            var g = _target.GetComponent<LODGroup>() ?? _target.AddComponent<LODGroup>();
            var rends = _target.GetComponentsInChildren<Renderer>(true);
            g.SetLODs(new[] { new LOD(0.5f, rends), new LOD(0.15f, rends), new LOD(0.02f, rends) });
            g.RecalculateBounds();
        }
        void ToUrp(Renderer r)
        {
            var urp = Shader.Find("Universal Render Pipeline/Lit");
            if (urp == null) return;
            foreach (var m in r.sharedMaterials) if (m != null) { m.shader = urp; EditorUtility.SetDirty(m); }
        }

        // ---- 4 · Script opcional ----
        void DrawScriptSection()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("4 · Script (opcional, viaja como TextAsset)", EditorStyles.boldLabel);
            _script = (TextAsset)EditorGUILayout.ObjectField(".cs / .dll.bytes", _script, typeof(TextAsset), false);
            EditorGUILayout.LabelField("   El package no compila ni ejecuta: lo carga el proyecto «player» (HybridCLR).", EditorStyles.miniLabel);
        }

        // ---- 5 · Crear / construir / subir ----
        void DrawBuildSection()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("5 · Crear, construir y subir", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(_busy || _target == null || string.IsNullOrEmpty(_reMemoryId)))
            {
                if (GUILayout.Button("Crear Addressable (configura el grupo remoto)")) ConfigureAndMark();
                if (GUILayout.Button("Construir y subir a revisión")) BuildAndUpload();
            }
        }

        string Prefix => "rememoria_" + _reMemoryId;
        string EntryKey => "rememoria_" + _reMemoryId;

        /// Configura perfil + grupo remoto (RemoteLoadPath al bucket, catálogo remoto,
        /// Append Hash, LZ4) y marca el prefab (+ script) como Addressable.
        void ConfigureAndMark()
        {
            try
            {
                var settings = AddressableAssetSettingsDefaultObject.Settings
                               ?? AddressableAssetSettingsDefaultObject.GetSettings(true);

                string path = AssetDatabase.GetAssetPath(_target);
                if (string.IsNullOrEmpty(path)) { SetStatus("err", "El objeto debe ser un asset del proyecto (prefab o GLB importado)."); return; }

                // Perfil: rutas remotas hacia el bucket (token [BuildTarget] → WebGL).
                string profileId = settings.activeProfileId;
                string loadPath = $"{ReExpoConfig.SupabaseUrl}/storage/v1/object/public/{AddressableUploadService.Bucket}/{Prefix}/[BuildTarget]";
                settings.profileSettings.SetValue(profileId, "RemoteLoadPath", loadPath);
                settings.profileSettings.SetValue(profileId, "RemoteBuildPath", "ServerData/[BuildTarget]");

                // Catálogo remoto ON, apuntando a las rutas remotas.
                settings.BuildRemoteCatalog = true;
                settings.RemoteCatalogBuildPath.SetVariableByName(settings, "RemoteBuildPath");
                settings.RemoteCatalogLoadPath.SetVariableByName(settings, "RemoteLoadPath");

                // Grupo remoto (crea si falta) con compresión LZ4 + Append Hash.
                var group = settings.FindGroup(GroupName)
                            ?? settings.CreateGroup(GroupName, false, false, true, null,
                                   typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));
                var schema = group.GetSchema<BundledAssetGroupSchema>() ?? group.AddSchema<BundledAssetGroupSchema>();
                schema.BuildPath.SetVariableByName(settings, "RemoteBuildPath");
                schema.LoadPath.SetVariableByName(settings, "RemoteLoadPath");
                schema.Compression = BundledAssetGroupSchema.BundleCompressionMode.LZ4;
                schema.BundleNaming = BundledAssetGroupSchema.BundleNamingStyle.AppendHash;
                schema.IncludeInBuild = true;

                // Marcar el prefab.
                string guid = AssetDatabase.AssetPathToGUID(path);
                var entry = settings.CreateOrMoveEntry(guid, group);
                entry.address = EntryKey;

                // Script opcional como TextAsset en el mismo grupo.
                if (_script != null)
                {
                    string sp = AssetDatabase.GetAssetPath(_script);
                    var sentry = settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(sp), group);
                    sentry.address = EntryKey + "_script";
                }

                EditorUtility.SetDirty(settings);
                AssetDatabase.SaveAssets();
                SetStatus("ok", $"Addressable «{EntryKey}» configurado en el grupo remoto. Ya puedes construir y subir.");
            }
            catch (Exception e)
            {
                SetStatus("err", "Config de Addressables: " + e.Message + " (revisa la versión del paquete Addressables).");
            }
        }

        async void BuildAndUpload()
        {
            _busy = true;
            try
            {
                SetStatus("busy", "Construyendo el contenido Addressables…");
                AddressableAssetSettings.BuildPlayerContent(out var result);
                if (!string.IsNullOrEmpty(result.Error)) { SetStatus("err", "Build: " + result.Error); return; }

                string serverData = Path.Combine("ServerData", EditorUserBuildSettings.activeBuildTarget.ToString());
                SetStatus("busy", "Subiendo bundles al bucket…");
                var (catalogUrl, uerr) = await AddressableUploadService.UploadFolder(serverData, Prefix);
                if (uerr != null) { SetStatus("err", uerr); return; }

                var build = new JObject
                {
                    ["build_target"] = EditorUserBuildSettings.activeBuildTarget.ToString(),
                    ["bundle_path"] = Prefix,
                    ["catalog_url"] = catalogUrl,
                    ["entry_key"] = EntryKey,
                    ["has_script"] = _script != null,
                };
                SetStatus("busy", "Registrando el build en revisión…");
                var (id, serr) = await ReExpoClient.SubmitUnityBuild(_reMemoryId, build);
                if (serr != null) { SetStatus("err", "Registro: " + serr); return; }
                SetStatus("ok", "✓ Build subido y en cola de revisión (UNITY). Cuando lo aprueben, entrará al recinto.");
            }
            catch (Exception e) { SetStatus("err", e.Message); }
            finally { _busy = false; Repaint(); }
        }
    }
}
#endif
