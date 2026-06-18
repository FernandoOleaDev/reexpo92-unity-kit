using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace ReExpo92.WorldKit.Editor
{
    /// <summary>
    /// Panel de control principal del package: estado de sesión, login, y la
    /// construcción del mundo georreferenciado con sus capas. UI Toolkit con la
    /// estética Expo 92 (ver <see cref="ReExpoUI"/>).
    /// </summary>
    public class ControlPanelWindow : EditorWindow
    {
        TextField _email;
        TextField _password;
        Label _status;
        Toggle _tiles, _pois, _zones;

        [MenuItem("Tools/re-Expo92/Panel de control")]
        public static void Open()
        {
            var w = GetWindow<ControlPanelWindow>();
            w.titleContent = new GUIContent("re-Expo92", ReExpoUI.LoadLogo());
            w.minSize = new Vector2(360, 420);
            ReExpoEditorService.Restore();
        }

        public void CreateGUI()
        {
            ReExpoEditorService.Restore();
            Render();
        }

        void Render()
        {
            var root = rootVisualElement;
            root.Clear();
            ReExpoUI.ApplyStyle(root);

            root.Add(ReExpoUI.Header("Mundo georreferenciado de la Cartuja"));

            var body = new VisualElement();
            body.AddToClassList("rx-body");
            root.Add(body);

            // ---- sesión ----
            body.Add(ReExpoUI.SectionTitle("Tu cuenta"));
            var account = ReExpoUI.Card();
            body.Add(account);

            if (ReExpoEditorService.IsLoggedIn)
            {
                account.Add(ReExpoUI.StatusBar("on", "Sesión: " + (ReExpoEditorService.Email ?? "(sin email)")));
                account.Add(ReExpoUI.Secondary("Cerrar sesión", () => { ReExpoEditorService.SignOut(); Render(); }, "⎋"));
            }
            else
            {
                account.Add(ReExpoUI.StatusBar("off", "No has iniciado sesión."));
                _email = new TextField("Email");
                _password = new TextField("Contraseña") { isPasswordField = true };
                account.Add(_email);
                account.Add(_password);
                var row = new VisualElement();
                row.AddToClassList("rx-row");
                var bPwd = ReExpoUI.Primary("Entrar", SignInPassword, "🔑");
                var bGoogle = ReExpoUI.Secondary("Con Google", SignInGoogle, "🟢");
                bPwd.style.flexGrow = 1; bGoogle.style.flexGrow = 1;
                row.Add(bPwd); row.Add(bGoogle);
                account.Add(row);
            }

            // ---- mundo ----
            body.Add(ReExpoUI.SectionTitle("Construir mundo"));
            var world = ReExpoUI.Card();
            body.Add(world);

            bool cesium = WorldBuilderLocator.IsAvailable;
            world.Add(ReExpoUI.StatusBar(cesium ? "on" : "warn",
                cesium ? "Cesium for Unity detectado." : "Cesium for Unity NO instalado (com.cesium.unity)."));

            _tiles = new Toggle("🌍  Maqueta de Google (referencia)") { value = true };
            _pois = new Toggle("📌  POIs (re-memorias)") { value = true };
            _zones = new Toggle("▰  Zonas del recinto") { value = true };
            world.Add(_tiles); world.Add(_pois); world.Add(_zones);

            var build = ReExpoUI.Primary("Descargar datos y construir mundo", BuildWorld, "🏗");
            build.SetEnabled(cesium && ReExpoEditorService.IsLoggedIn);
            world.Add(build);

            _status = new Label(string.Empty);
            _status.AddToClassList("rx-feedback");
            world.Add(_status);

            // ---- pie ----
            body.Add(ReExpoUI.Separator());
            var foot = new VisualElement();
            foot.AddToClassList("rx-row");
            foot.Add(ReExpoUI.Ghost("Re-ejecutar asistente", SetupWizardWindow.Open, "🧭"));
            foot.Add(ReExpoUI.Ghost("Documentación", () =>
                Application.OpenURL("https://github.com/FernandoOleaDev/reexpo92-unity-kit#readme"), "❔"));
            body.Add(foot);
        }

        // ---- handlers ----
        async void SignInPassword()
        {
            SetStatus("busy", "Entrando…");
            var err = await ReExpoEditorService.SignInPassword(_email.value, _password.value);
            if (err != null) { SetStatus("err", "Error: " + err); return; }
            Render();
        }

        async void SignInGoogle()
        {
            SetStatus("busy", "Abriendo el navegador para Google…");
            var err = await ReExpoEditorService.SignInGoogle();
            if (err != null) { SetStatus("err", "Error: " + err); return; }
            Render();
        }

        async void BuildWorld()
        {
            SetStatus("busy", "Descargando datos y construyendo…");
            var msg = await ReExpoEditorService.BuildWorld(_tiles.value, _pois.value, _zones.value);
            bool ok = msg != null && msg.StartsWith("OK");
            SetStatus(ok ? "ok" : "err", msg ?? "Listo.");
        }

        void SetStatus(string kind, string text)
        {
            if (_status == null) return;
            _status.text = text;
            _status.RemoveFromClassList("rx-feedback--ok");
            _status.RemoveFromClassList("rx-feedback--err");
            _status.RemoveFromClassList("rx-feedback--busy");
            _status.AddToClassList(kind == "ok" ? "rx-feedback--ok" : kind == "err" ? "rx-feedback--err" : "rx-feedback--busy");
        }
    }
}
