using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace ReExpo92.WorldKit.Editor
{
    /// <summary>
    /// Asistente de configuración del package (UI Toolkit, estética Win95 + Expo
    /// 92, estilos inline). Guía: bienvenida → requisitos → login → comprobar
    /// acceso → construir. Se abre solo en el primer arranque
    /// (<see cref="FirstRunBootstrap"/>) y se relanza desde el panel de control.
    /// </summary>
    public class SetupWizardWindow : EditorWindow
    {
        [SerializeField] int _step;
        static readonly string[] Titles = { "Bienvenida", "Requisitos", "Tu cuenta", "Acceso de desarrollador", "Construir" };

        VisualElement _body, _footer, _stepsBar;
        Label _status;
        TextField _email, _password;

        [MenuItem("re-Expo92/Asistente de configuración", false, 1)]
        public static void Open()
        {
            var w = GetWindow<SetupWizardWindow>(true, "re-Expo92 · Configuración");
            w.titleContent = new GUIContent("Configuración", ReExpoUI.LoadLogo());
            w.minSize = new Vector2(460, 460);
            ReExpoEditorService.Restore();
        }

        public void CreateGUI()
        {
            var root = rootVisualElement;
            ReExpoUI.ApplyStyle(root);
            root.Add(ReExpoUI.Header("Asistente de configuración"));

            var body = ReExpoUI.Body();
            root.Add(body);

            _stepsBar = ReExpoUI.Row();
            _stepsBar.style.marginBottom = 8;
            body.Add(_stepsBar);

            _body = new VisualElement();
            _body.style.flexGrow = 1;
            body.Add(_body);

            _status = ReExpoUI.Feedback();
            body.Add(_status);

            _footer = ReExpoUI.Row();
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
            card.Add(ReExpoUI.StepTitle($"Paso {_step + 1}/{Titles.Length} · {Titles[_step]}"));
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
            {
                var next = ReExpoUI.Primary("Siguiente", () => { _step++; Render(); }, "▸");
                next.SetEnabled(CanAdvance());
                if (!CanAdvance()) next.tooltip = "Inicia sesión para continuar.";
                _footer.Add(next);
            }
            else
            {
                _footer.Add(ReExpoUI.Primary("Finalizar", () => { ReExpoEditorService.SetupDone = true; Close(); }, "✓"));
            }
        }

        /// No se puede pasar del paso «Tu cuenta» (2) sin sesión iniciada.
        bool CanAdvance() => _step != 2 || ReExpoEditorService.IsLoggedIn;

        void RenderSteps()
        {
            _stepsBar.Clear();
            for (int i = 0; i < Titles.Length; i++)
                _stepsBar.Add(ReExpoUI.StepDot(i + 1, i == _step, i < _step));
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
            c.Add(ReExpoUI.Check(cesium, cesium ? "Cesium for Unity instalado" : "Cesium for Unity NO detectado"));
            if (!cesium)
            {
                c.Add(ReExpoUI.Primary("Instalar Cesium for Unity", InstallCesium, "⬇"));
                c.Add(ReExpoUI.Note("Lo añadimos por ti (scoped registry + paquete). Unity lo descargará y recompilará solo; el check se pondrá en verde al terminar."));
            }
            else
            {
                c.Add(ReExpoUI.Note("Todo listo para la maqueta 3D."));
            }

            bool tmp = ReExpoTMPSetup.EssentialsPresent();
            c.Add(ReExpoUI.Check(tmp, tmp ? "TextMeshPro listo (carteles)" : "TextMeshPro: faltan recursos (los carteles no se ven)"));
            if (!tmp)
                c.Add(ReExpoUI.Primary("Instalar recursos de TextMeshPro", InstallTMP, "⬇"));
        }

        void InstallTMP()
        {
            if (ReExpoTMPSetup.EnsureEssentials())
                Status("ok", "Importando recursos de TextMeshPro… Unity los añade y recompila. Reconstruye el mundo al acabar.");
            else
                Status("err", "No pude importar TMP. Hazlo a mano: Window ▸ TextMeshPro ▸ Import TMP Essential Resources.");
        }

        void InstallCesium()
        {
            Status("busy", "Instalando Cesium for Unity… Unity lo descargará y recompilará. Espera unos segundos.");
            var err = ReExpoEditorService.InstallCesium();
            if (err != null) { Status("err", "No se pudo instalar Cesium: " + err); return; }
            Status("ok", "Cesium añadido. Unity lo está instalando y recompilará solo; el check se pondrá en verde al terminar.");
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
            _email = ReExpoUI.Field("Email");
            _password = ReExpoUI.Field("Contraseña", true);
            c.Add(_email);
            c.Add(_password);
            var row = ReExpoUI.Row();
            var b1 = ReExpoUI.Primary("Entrar", SignInPassword, "🔑");
            var b2 = ReExpoUI.Secondary("Con Google", SignInGoogle, "🟢");
            b1.style.flexGrow = 1; b2.style.flexGrow = 1;
            row.Add(b1); row.Add(b2);
            c.Add(row);
        }

        void StepAccess(VisualElement c)
        {
            c.Add(ReExpoUI.Para("Comprobando tu acceso de desarrollador…"));
            CheckAccess(); // automático al entrar al paso
        }

        void StepBuild(VisualElement c)
        {
            c.Add(ReExpoUI.Para("Todo listo. Construye el rig georreferenciado en la escena abierta:"));
            var b = ReExpoUI.Primary("Construir mundo de la Cartuja", BuildWorld, "🏗");
            b.SetEnabled(WorldBuilderLocator.IsAvailable && ReExpoEditorService.IsLoggedIn);
            c.Add(b);
            c.Add(ReExpoUI.Note("Después usa el Panel de control (re-Expo92 ▸ Panel de control) para el día a día."));
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
            Status("busy", "Comprobando tu acceso…");
            var (key, err) = await ReExpoClient.FetchGoogleKey();
            if (_step != 3) return; // el usuario ya navegó a otro paso
            if (err != null) { Status("err", "Aún no tienes acceso de desarrollador. Pídeselo al administrador."); return; }
            if (string.IsNullOrEmpty(key)) { Status("err", "Tu acceso está OK, pero el administrador aún no ha activado el entorno. Avísale."); return; }
            Status("ok", "✓ Acceso de desarrollador confirmado.");
        }

        async void BuildWorld()
        {
            Status("busy", "Construyendo…");
            var msg = await ReExpoEditorService.BuildWorld(true, true, true);
            Status(msg != null && msg.StartsWith("OK") ? "ok" : "err", msg ?? "Listo.");
        }

        void Status(string kind, string text) => ReExpoUI.SetFeedback(_status, kind, text);
    }
}
