// Ventana de REVISIÓN de Addressables (solo revisor_unity/staff). Lista los builds
// pendientes, permite PREVISUALIZARLOS en la escena (cargar catálogo remoto +
// instanciar) y votar (aprobar/rechazar con motivo) por el motor de consenso.
//
// SEGURIDAD: la visibilidad de esta ventana es solo comodidad. La autoridad la pone
// el SERVIDOR: cast_review_vote exige rol revisor (JWT firmado) y bloquea la
// auto-revisión. Aunque alguien abra esta ventana sin ser revisor, el servidor le
// rechaza el voto y get_pending_unity_builds le devuelve vacío.
//
// Requiere com.unity.addressables (asmdef con defineConstraint REEXPO_ADDR).
#if REEXPO_ADDR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.AddressableAssets;
using ReExpo92.WorldKit;

namespace ReExpo92.WorldKit.Editor
{
    public class AddressableReviewerWindow : EditorWindow
    {
        const string PreviewRoot = "ReExpo92 Review Preview";

        [MenuItem("re-Expo92/Revisión de Addressables")]
        public static void Open()
        {
            var w = GetWindow<AddressableReviewerWindow>("Revisión · re-Expo92");
            w.minSize = new Vector2(440, 520);
        }

        bool? _isReviewer;          // null = comprobando
        List<PendingBuild> _builds;
        Vector2 _scroll;
        string _status = "";
        string _statusKind = "";
        bool _busy;
        string _rejecting;          // id del build con el formulario de rechazo abierto
        readonly Dictionary<string, string> _reason = new Dictionary<string, string>();

        void OnEnable()
        {
            ReExpoEditorService.Restore();
            CheckRole();
        }

        void SetStatus(string kind, string text) { _statusKind = kind; _status = text; Repaint(); }

        async void CheckRole()
        {
            if (!ReExpoEditorService.IsLoggedIn) { _isReviewer = false; Repaint(); return; }
            _isReviewer = await ReExpoClient.CanReviewUnity();
            if (_isReviewer == true) LoadPending();
            Repaint();
        }

        async void LoadPending()
        {
            var (builds, err) = await ReExpoClient.GetPendingUnityBuilds();
            if (err != null) { SetStatus("err", "No se pudieron cargar los pendientes: " + err); return; }
            _builds = builds;
            Repaint();
        }

        void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            EditorGUILayout.LabelField("REVISIÓN DE ADDRESSABLES", EditorStyles.boldLabel);

            if (!ReExpoEditorService.IsLoggedIn)
            {
                EditorGUILayout.HelpBox("Inicia sesión en el Panel de control de re-Expo92.", MessageType.Warning);
                if (GUILayout.Button("Abrir Panel de control")) EditorApplication.ExecuteMenuItem("re-Expo92/Panel de control");
                EditorGUILayout.EndScrollView();
                return;
            }
            if (_isReviewer == null) { EditorGUILayout.LabelField("Comprobando permisos…"); EditorGUILayout.EndScrollView(); return; }
            if (_isReviewer == false)
            {
                EditorGUILayout.HelpBox("No eres revisor de Addressables. Solicita la skill «Revisión de Addressables» en /colabora.", MessageType.Info);
                EditorGUILayout.EndScrollView();
                return;
            }

            EditorGUILayout.HelpBox(
                "Builds propuestos pendientes de validar. Previsualízalos en la escena y aprueba o rechaza " +
                "(con motivo). El servidor exige rol revisor y no te deja revisar lo tuyo.", MessageType.Info);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("↻ Refrescar")) LoadPending();
                if (GUILayout.Button("Limpiar previsualización")) ClearPreview();
            }

            if (_builds == null) EditorGUILayout.LabelField("Cargando…");
            else if (_builds.Count == 0) EditorGUILayout.HelpBox("No hay builds pendientes de revisar.", MessageType.None);
            else foreach (var b in _builds) DrawBuild(b);

            if (!string.IsNullOrEmpty(_status))
            {
                var c = _statusKind == "err" ? Color.red : _statusKind == "ok" ? new Color(0.1f, 0.5f, 0.2f) : Color.gray;
                var prev = GUI.color; GUI.color = c;
                EditorGUILayout.LabelField(_status, EditorStyles.wordWrappedMiniLabel);
                GUI.color = prev;
            }
            EditorGUILayout.EndScrollView();
        }

        void DrawBuild(PendingBuild b)
        {
            EditorGUILayout.Space();
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(b.ReMemoryName ?? "Re-memoria", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"{b.BuildTarget} · por {b.BuiltBy} · {b.CreatedAt}", EditorStyles.miniLabel);
            if (b.HasScript)
                EditorGUILayout.HelpBox($"Incluye un SCRIPT (estado: {b.ScriptStatus}). Revisa con cuidado.", MessageType.Warning);
            EditorGUILayout.SelectableLabel("Clave: " + b.EntryKey, EditorStyles.miniLabel, GUILayout.Height(16));

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(_busy))
                    if (GUILayout.Button("Previsualizar en escena")) Preview(b);
                if (GUILayout.Button("Abrir catálogo")) Application.OpenURL(b.CatalogUrl);
            }

            if (_rejecting == b.Id)
            {
                _reason[b.Id] = EditorGUILayout.TextField("Motivo", _reason.TryGetValue(b.Id, out var rr) ? rr : "");
                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(_busy))
                        if (GUILayout.Button("Confirmar rechazo")) Vote(b, "rechazar");
                    if (GUILayout.Button("Cancelar")) { _rejecting = null; Repaint(); }
                }
            }
            else
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(_busy))
                    {
                        var prev = GUI.backgroundColor; GUI.backgroundColor = new Color(0.6f, 0.9f, 0.7f);
                        if (GUILayout.Button("APROBAR (integrable)")) Vote(b, "aprobar");
                        GUI.backgroundColor = prev;
                        if (GUILayout.Button("Rechazar…")) { _rejecting = b.Id; Repaint(); }
                    }
                }
            }
            EditorGUILayout.EndVertical();
        }

        async void Vote(PendingBuild b, string vote)
        {
            if (vote == "rechazar" && string.IsNullOrWhiteSpace(_reason.TryGetValue(b.Id, out var rr) ? rr : ""))
            { SetStatus("err", "El rechazo necesita un motivo."); return; }
            _busy = true; Repaint();
            var comment = _reason.TryGetValue(b.Id, out var c) ? c : null;
            var (ok, err) = await ReExpoClient.CastReviewVote("unity_build", b.Id, vote, comment);
            _busy = false;
            if (!ok) { SetStatus("err", "No se pudo registrar el voto: " + err); return; }
            _rejecting = null;
            SetStatus("ok", vote == "aprobar" ? "Voto de aprobación registrado." : "Build rechazado con motivo.");
            LoadPending();
        }

        // ---- previsualización: carga el catálogo remoto e instancia la pieza ----
        async void Preview(PendingBuild b)
        {
            _busy = true; SetStatus("busy", "Cargando el Addressable… (puede requerir modo Play en algunas versiones)");
            try
            {
                ClearPreview();
                var catHandle = Addressables.LoadContentCatalogAsync(b.CatalogUrl, false);
                await catHandle.Task;
                var instHandle = Addressables.InstantiateAsync(b.EntryKey);
                await instHandle.Task;
                var go = instHandle.Result;
                if (go != null)
                {
                    var root = GameObject.Find(PreviewRoot) ?? new GameObject(PreviewRoot);
                    go.transform.SetParent(root.transform, true);
                    Selection.activeGameObject = go;
                    if (SceneView.lastActiveSceneView != null) SceneView.lastActiveSceneView.FrameSelected();
                    SetStatus("ok", "Previsualización cargada. Inspecciónala y decide.");
                }
                else SetStatus("err", "El Addressable no devolvió ningún objeto.");
            }
            catch (System.Exception e)
            {
                SetStatus("err", "No se pudo previsualizar: " + e.Message + " · Prueba a entrar en modo Play o abre el catálogo.");
            }
            finally { _busy = false; Repaint(); }
        }

        void ClearPreview()
        {
            var root = GameObject.Find(PreviewRoot);
            if (root != null) DestroyImmediate(root);
        }
    }
}
#endif
