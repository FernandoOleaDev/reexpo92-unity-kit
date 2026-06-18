using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace ReExpo92.WorldKit.Editor
{
    /// <summary>
    /// Asistente de configuración del package (UI Toolkit, estética Expo 92).
    /// Guía: bienvenida → requisitos → login → comprobar acceso → construir.
    /// Se abre solo en el primer arranque (<see cref="FirstRunBootstrap"/>) y se
    /// puede relanzar desde el panel de control.
    /// </summary>
    public class SetupWizardWindow : EditorWindow
    {
        [SerializeField] int _step;
        static readonly string[] Titles = { "Bienvenida", "Requisitos", "Tu cuenta", "Acceso a la clave", "Construir" };

        VisualElement _body, _footer, _stepsBar;
        Label _status;
        TextField _email, _password;

        [MenuItem("Tools/re-Expo92/Asistente de configuración")]
        public static void Open()
        {
            var w = GetWindow<SetupWizardWindow>(true, "re-Expo92 · Configuración");
            w.titleContent = new GUIContent("Configuración", ReExpoUI.LoadLogo());
            w.minSize = new Vector2(460, 440);
            ReExpoEditorService.Restore();
        }

        public void CreateGUI()
        {
            var root = rootVisualElement;
            ReExpoUI.ApplyStyle(root);
            root.Add(ReExpoUI.Header("Asistente de configuración"));

            var body = new VisualElement();
            body.AddToClassList("rx-body");
            body.style.flexGrow = 1;
            root.Add(body);

            _stepsBar = new VisualElement();
            _stepsBar.AddToClassList("rx-steps");
            body.Add(_stepsBar);

            _body = new VisualElement();
            _body.style.flexGrow = 1;
            body.Add(_body);

            _status = new Label(string.Empty);
            _status.AddToClassList("rx-feedback");
            body.Add(_status);

            _footer = new VisualElement();
            _footer.AddToClassList("rx-row");
            _footer.style.marginTop = 8;
            body.Add(_footer);

            Render();
        }

        void Render()
        {
            RenderSteps();
            _body.Clear();
            _footer.Clear();
            _status.text = string.Empty;

            var card = ReExpoUI.Card();
            var title = new Label($"Paso {_step + 1}/{Titles.Length} · {Titles[_step]}");
            title.AddToClassList("rx-steptitle");
            card.Add(title);
            _body.Add(card);

            switch (_step)
            {
                case 0: StepWelcome(card); break;
                case 1: StepRequirements(card); break;
                case 2: StepAccount(card); break;
                case 3: StepAccess(card); break;
                case 4: StepBuild(card); break;
            }

            if (_step > 0)
                _footer.Add(ReExpoUI.Secondary("Atrás", () => { _step--; Render(); }, "◂"));
            var spacer = new VisualElement { style = { flexGrow = 1 } };
            _footer.Add(spacer);

            if (_step < Titles.Length - 1)
                _footer.Add(ReExpoUI.Primary("Siguiente", () => { _step++; Render(); }, "▸"));
            else
                _footer.Add(ReExpoUI.Primary("Finalizar", () => { ReExpoEditorService.SetupDone = true; Close(); }, "✓"));
        }

        void RenderSteps()
        {
            _stepsBar.Clear();
            for (int i = 0; i < Titles.Length; i++)
            {
                var d = new Label((i + 1).ToString());
                d.AddToClassList("rx-step");
                if (i < _step) d.AddToClassList("rx-step--done");
                else if (i == _step) d.AddToClassList("rx-step--active");
                _stepsBar.Add(d);
            }
        }

        void StepWelcome(VisualElement c)
        {
            c.Add(ReExpoUI.Para(
                "Te dejamos listo para construir la recreación de la Expo 92 sobre una maqueta 3D " +
                "georreferenciada de la Isla de la Cartuja."));
            c.Add(ReExpoUI.Para(
                "Necesitarás tu cuenta de colaborador de re-Expo92 y el perfil de desarrollador Unity " +
                "(lo concede el administrador). La maqueta de Google es solo referencia de editor."));
        }

        void StepRequirements(VisualElement c)
        {
            c.Add(ReExpoUI.Check(true, "Unity 6.3 LTS (recomendado 6000.3.17f1)"));
            c.Add(ReExpoUI.Check(true, "Render pipeline URP"));
            bool cesium = WorldBuilderLocator.IsAvailable;
            c.Add(ReExpoUI.Check(cesium, cesium
                ? "Cesium for Unity instalado"
                : "Cesium for Unity NO detectado — instálalo (com.cesium.unity) para la maqueta 3D"));
            c.Add(ReExpoUI.Note("La verificación de Unity/URP es informativa; instala Cesium for Unity desde su instalador oficial si falta."));
        }

        void StepAccount(VisualElement c)
        {
            if (ReExpoEditorService.IsLoggedIn)
            {
                c.Add(ReExpoUI.Check(true, "Sesión iniciada: " + (ReExpoEditorService.Email ?? "(sin email)")));
                c.Add(ReExpoUI.Secondary("Cerrar sesión", () => { ReExpoEditorService.SignOut(); Render(); }, "⎋"));
                return;
            }
            c.Add(ReExpoUI.Para("Entra con tu cuenta de re-Expo92 (la misma que en la web):"));
            _email = new TextField("Email");
            _password = new TextField("Contraseña") { isPasswordField = true };
            c.Add(_email);
            c.Add(_password);
            var row = new VisualElement();
            row.AddToClassList("rx-row");
            var b1 = ReExpoUI.Primary("Entrar", SignInPassword, "🔑");
            var b2 = ReExpoUI.Secondary("Con Google", SignInGoogle, "🟢");
            b1.style.flexGrow = 1; b2.style.flexGrow = 1;
            row.Add(b1); row.Add(b2);
            c.Add(row);
        }

        void StepAccess(VisualElement c)
        {
            c.Add(ReExpoUI.Para("Comprueba que tienes acceso a la clave de Google (perfil de desarrollador Unity)."));
            c.Add(ReExpoUI.Primary("Comprobar acceso", CheckAccess, "🔓"));
        }

        void StepBuild(VisualElement c)
        {
            c.Add(ReExpoUI.Para("Todo listo. Construye el rig georreferenciado en la escena abierta:"));
            var b = ReExpoUI.Primary("Construir mundo de la Cartuja", BuildWorld, "🏗");
            b.SetEnabled(WorldBuilderLocator.IsAvailable && ReExpoEditorService.IsLoggedIn);
            c.Add(b);
            c.Add(ReExpoUI.Note("Después usa el Panel de control (Tools ▸ re-Expo92) para el día a día."));
        }

        // ---- handlers ----
        async void SignInPassword()
        {
            Status("busy", "Entrando…");
            var err = await ReExpoEditorService.SignInPassword(_email.value, _password.value);
            if (err != null) { Status("err", "Error: " + err); return; }
            Render();
        }

        async void SignInGoogle()
        {
            Status("busy", "Abriendo el navegador…");
            var err = await ReExpoEditorService.SignInGoogle();
            if (err != null) { Status("err", "Error: " + err); return; }
            Render();
        }

        async void CheckAccess()
        {
            if (!ReExpoEditorService.IsLoggedIn) { Status("err", "Inicia sesión primero (paso anterior)."); return; }
            Status("busy", "Comprobando…");
            var (key, err) = await ReExpoClient.FetchGoogleKey();
            if (err != null) { Status("err", "Sin acceso: " + err + "  ·  Pide al administrador el perfil de desarrollador Unity."); return; }
            if (string.IsNullOrEmpty(key)) { Status("err", "Tienes acceso, pero el administrador aún no ha fijado la clave (/admin/unity)."); return; }
            Status("ok", "✓ Acceso correcto. Clave de Google disponible.");
        }

        async void BuildWorld()
        {
            Status("busy", "Construyendo…");
            var msg = await ReExpoEditorService.BuildWorld(true, true, true);
            Status(msg != null && msg.StartsWith("OK") ? "ok" : "err", msg ?? "Listo.");
        }

        void Status(string kind, string text)
        {
            _status.text = text;
            _status.RemoveFromClassList("rx-feedback--ok");
            _status.RemoveFromClassList("rx-feedback--err");
            _status.RemoveFromClassList("rx-feedback--busy");
            _status.AddToClassList(kind == "ok" ? "rx-feedback--ok" : kind == "err" ? "rx-feedback--err" : "rx-feedback--busy");
        }
    }
}
