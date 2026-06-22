using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UIElements;

namespace ReExpo92.WorldKit.Editor
{
    /// <summary>
    /// Ventana-herramienta principal de re-Expo92 (la que queda abierta).
    /// Toolbar NATIVA (botones de Unity, fiables) para capas + pestañas:
    ///   • Re-memorias — listado con miniatura, título y resumen; filtro por tipo.
    ///   • Configuración — activar/desactivar el límite de carga a la Cartuja.
    ///   • Ayuda — qué es y qué viene (descarga/aportaciones en desarrollo).
    /// </summary>
    public class ToolsWindow : EditorWindow
    {
        static readonly Color Ink = new Color(0.102f, 0.090f, 0.078f);
        static readonly Color RowAlt = new Color(0f, 0f, 0f, 0.04f);
        static readonly Color Line = new Color(0f, 0f, 0f, 0.12f);
        static readonly Color ThumbBg = new Color(0.80f, 0.80f, 0.80f);

        static readonly string[] TabNames = { "Re-memorias", "Configuración", "Ayuda" };
        [SerializeField] int _tab;
        Toggle _tiles, _pois, _zones; // capas a construir (mismo set que el Panel de control)

        List<ReMemoryItem> _items;
        List<ReMemoryItem> _filtered = new List<ReMemoryItem>();
        // Estado de recreación por pieza (modelo / Addressable / desactualizado).
        Dictionary<string, UnityBuildState> _states = new Dictionary<string, UnityBuildState>();
        string _loadError;
        bool _loading;
        string _typeFilter = "Todos";
        string _search = "";

        Label _status;
        ListView _list;
        static readonly Dictionary<string, Texture2D> _texCache = new Dictionary<string, Texture2D>();

        [MenuItem("re-Expo92/Herramientas", false, 2)]
        public static void Open()
        {
            var w = GetWindow<ToolsWindow>();
            w.titleContent = new GUIContent("re-Expo92 Tools", ReExpoUI.LoadLogo());
            w.minSize = new Vector2(500, 560);
            ReExpoEditorService.Restore();
        }

        public void CreateGUI()
        {
            ReExpoEditorService.Restore();
            Rebuild();
            if (_items == null) LoadItems();
        }

        void Rebuild()
        {
            var root = rootVisualElement;
            root.Clear();
            ReExpoUI.ApplyStyle(root);
            root.Add(ReExpoUI.Header("Herramientas del recinto"));
            root.Add(Toolbar());
            root.Add(TabStrip());

            var body = ReExpoUI.Body();
            body.style.paddingTop = 8;
            root.Add(body);

            if (_tab == 0)
            {
                // Re-memorias gestiona su propio scroll (ListView virtualizado).
                RenderRememorias(body);
            }
            else
            {
                // Configuración/Ayuda: scroll vertical para que nada quede cortado.
                var scroll = new ScrollView(ScrollViewMode.Vertical);
                scroll.style.flexGrow = 1;
                scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
                scroll.verticalScrollerVisibility = ScrollerVisibility.Auto;
                scroll.contentContainer.style.paddingRight = 2; // aire junto a la barra
                body.Add(scroll);
                if (_tab == 1) RenderConfig(scroll.contentContainer);
                else RenderAyuda(scroll.contentContainer);
            }

            _status = ReExpoUI.Feedback();
            _status.style.marginLeft = 12; _status.style.marginRight = 12; _status.style.marginBottom = 6;
            root.Add(_status);
        }

        // ---------- toolbar nativa ----------
        VisualElement Toolbar()
        {
            var tb = new Toolbar();
            tb.Add(LayerToggle("📌 POIs", "POIs"));
            tb.Add(LayerToggle("▰ Zonas", "Zonas"));
            tb.Add(LayerToggle("🌍 Tiles", "Google Photorealistic 3D Tiles"));
            tb.Add(new ToolbarSpacer());
            var wf = new ToolbarToggle { text = "▦ Wireframe", value = ReExpoWire.Enabled };
            wf.RegisterValueChangedCallback(e => { ReExpoWire.Enabled = e.newValue; Status("ok", e.newValue ? "Modo técnico ON (Scene + Game)." : "Modo técnico OFF."); });
            tb.Add(wf);
            tb.Add(new ToolbarSpacer());
            tb.Add(new ToolbarButton(Recenter) { text = "🎯 Recentrar" });
            tb.Add(new ToolbarButton(LoadItems) { text = "🔄 Refrescar" });
            return tb;
        }

        ToolbarToggle LayerToggle(string label, string child)
        {
            var t = new ToolbarToggle { text = label, value = LayerActive(child) };
            t.RegisterValueChangedCallback(e => SetLayer(child, e.newValue));
            return t;
        }

        // ---------- tira de pestañas (toggles nativos) ----------
        VisualElement TabStrip()
        {
            var tb = new Toolbar();
            for (int i = 0; i < TabNames.Length; i++)
            {
                int idx = i;
                var t = new ToolbarToggle { text = TabNames[i], value = i == _tab };
                t.style.unityFontStyleAndWeight = FontStyle.Bold;
                t.RegisterValueChangedCallback(e => { if (e.newValue) { _tab = idx; Rebuild(); } });
                tb.Add(t);
            }
            return tb;
        }

        // ---------- pestaña Re-memorias ----------
        void RenderRememorias(VisualElement c)
        {
            var filters = ReExpoUI.Row();
            filters.style.alignItems = Align.Center; filters.style.marginBottom = 6; filters.style.flexWrap = Wrap.Wrap;

            var choices = TypeChoices();
            var dd = new DropdownField("Tipo", choices, Mathf.Max(0, choices.IndexOf(_typeFilter)));
            dd.style.minWidth = 240; dd.style.marginRight = 8; StyleDropdown(dd);
            dd.RegisterValueChangedCallback(e => { _typeFilter = e.newValue; ApplyFilter(); });
            filters.Add(dd);

            var search = ReExpoUI.Field("Buscar");
            search.value = _search; search.style.flexGrow = 1; search.style.minWidth = 160;
            search.RegisterValueChangedCallback(e => { _search = e.newValue; ApplyFilter(); });
            filters.Add(search);
            c.Add(filters);

            var count = new Label(); count.name = "count"; count.style.color = Ink; count.style.fontSize = 11; count.style.marginBottom = 4;
            c.Add(count);

            _list = new ListView
            {
                fixedItemHeight = 74,
                selectionType = SelectionType.None,
                makeItem = MakeRow,
                bindItem = BindRow,
                itemsSource = _filtered,
            };
            _list.style.flexGrow = 1;
            _list.style.backgroundColor = Color.white;
            _list.style.borderTopWidth = 1; _list.style.borderLeftWidth = 1; _list.style.borderRightWidth = 1; _list.style.borderBottomWidth = 1;
            _list.style.borderTopColor = _list.style.borderLeftColor = _list.style.borderRightColor = _list.style.borderBottomColor = new Color(0.5f, 0.5f, 0.5f);
            c.Add(_list);

            ApplyFilter();
        }

        VisualElement MakeRow()
        {
            var r = new VisualElement();
            r.style.flexDirection = FlexDirection.Row; r.style.alignItems = Align.Center;
            r.style.height = 74; r.style.overflow = Overflow.Hidden;
            r.style.paddingTop = 6; r.style.paddingBottom = 6; r.style.paddingLeft = 8; r.style.paddingRight = 8;
            r.style.borderBottomWidth = 1; r.style.borderBottomColor = Line;

            var img = new Image { name = "thumb", scaleMode = ScaleMode.ScaleAndCrop };
            img.style.width = 58; img.style.height = 58; img.style.marginRight = 10; img.style.flexShrink = 0;
            img.style.backgroundColor = ThumbBg;
            img.style.borderTopWidth = 1; img.style.borderLeftWidth = 1; img.style.borderRightWidth = 1; img.style.borderBottomWidth = 1;
            img.style.borderTopColor = img.style.borderLeftColor = img.style.borderRightColor = img.style.borderBottomColor = new Color(0, 0, 0, 0.18f);
            r.Add(img);

            // columna de texto (minWidth 0 = permite elipsis al encoger)
            var col = new VisualElement(); col.style.flexGrow = 1; col.style.flexShrink = 1; col.style.minWidth = 0; col.style.overflow = Overflow.Hidden;

            var title = new Label { name = "title" };
            title.style.color = Ink; title.style.unityFontStyleAndWeight = FontStyle.Bold; title.style.fontSize = 12;
            title.style.whiteSpace = WhiteSpace.NoWrap; title.style.overflow = Overflow.Hidden; title.style.textOverflow = TextOverflow.Ellipsis;

            var chips = new VisualElement { name = "chips" }; chips.style.flexDirection = FlexDirection.Row; chips.style.marginTop = 2; chips.style.marginBottom = 2; chips.style.overflow = Overflow.Hidden;

            var summary = new Label { name = "summary" };
            summary.style.color = new Color(0.34f, 0.31f, 0.27f); summary.style.fontSize = 10;
            summary.style.whiteSpace = WhiteSpace.NoWrap; summary.style.overflow = Overflow.Hidden; summary.style.textOverflow = TextOverflow.Ellipsis;

            col.Add(title); col.Add(chips); col.Add(summary);
            r.Add(col);

            var go = new Button { name = "go", text = "🎯" };
            go.style.flexShrink = 0; go.style.marginLeft = 6; go.style.width = 30; go.style.height = 26;
            go.tooltip = "Llevar la cámara de Scene a este POI";
            go.clicked += () => { if (r.userData is ReMemoryItem it) GoToPoi(it); };
            r.Add(go);

            var dl = new Button { name = "dl", text = "⬇" };
            dl.style.flexShrink = 0; dl.style.marginLeft = 4; dl.style.width = 30; dl.style.height = 26;
            dl.tooltip = "Descargar el GLB aprobado de esta re-memoria a Assets/";
            dl.clicked += () => { if (r.userData is ReMemoryItem it) DownloadGlb(it); };
            r.Add(dl);
            return r;
        }

        void BindRow(VisualElement r, int i)
        {
            if (i < 0 || i >= _filtered.Count) return;
            var it = _filtered[i];
            r.userData = it;
            r.style.backgroundColor = (i % 2 == 0) ? Color.white : RowAlt;
            r.Q<Label>("title").text = string.IsNullOrEmpty(it.Name) ? "(sin nombre)" : it.Name;
            r.Q<Label>("summary").text = ShortDesc(it.Description);

            var chips = r.Q("chips"); chips.Clear();
            chips.Add(ReExpoUI.Chip(it.Category ?? "—", "type"));
            chips.Add(ReExpoUI.Chip(StatusLabel(it.Status), StatusKind(it.Status)));
            // Chip de recreación: desactualizado > recreado > solo modelo.
            if (_states.TryGetValue(it.Id, out var st))
            {
                if (st.Stale) chips.Add(ReExpoUI.Chip("desactualizado", "warn"));
                else if (st.HasBuild) chips.Add(ReExpoUI.Chip("recreado ✓", "ok"));
                else if (st.HasModel) chips.Add(ReExpoUI.Chip("modelo 3D", "type"));
            }

            var img = r.Q<Image>("thumb"); img.image = null;
            LoadThumb(img, it.ImageUrl);

            // el botón «ir al POI» solo si está colocado en la escena
            var goBtn = r.Q<Button>("go");
            if (goBtn != null) goBtn.SetEnabled(FindPoi(it.Name) != null);
        }

        static GameObject FindPoi(string name)
        {
            var rig = Rig(); if (rig == null) return null;
            var poiRoot = rig.transform.Find("POIs"); if (poiRoot == null) return null;
            var t = poiRoot.Find("POI · " + name);
            return t != null ? t.gameObject : null;
        }

        void GoToPoi(ReMemoryItem it)
        {
            var poi = FindPoi(it.Name);
            if (poi == null) { Status("err", "Ese POI no está colocado en el mapa."); return; }
            Selection.activeGameObject = poi;
            if (SceneView.lastActiveSceneView != null) SceneView.lastActiveSceneView.FrameSelected();
            Status("ok", "Cámara movida a «" + it.Name + "».");
        }

        // Descarga el GLB de la última versión aprobada a Assets/ (glTFast lo importa
        // solo si está instalado). Para construir el Addressable, usa el menú
        // «re-Expo92 ▸ Constructor de Addressables».
        async void DownloadGlb(ReMemoryItem it)
        {
            Status("busy", $"Buscando el modelo aprobado de «{it.Name}»…");
            var (url, err) = await ReExpoClient.GetApprovedGlbUrl(it.Id);
            if (err != null) { Status("err", "Error: " + err); return; }
            if (string.IsNullOrEmpty(url)) { Status("err", "Esta pieza aún no tiene ninguna versión de modelo aprobada."); return; }
            Status("busy", "Descargando GLB…");
            var (bytes, derr) = await SupabaseRest.Download(url);
            if (derr != null) { Status("err", "Descarga: " + derr); return; }
            const string dir = "Assets/ReExpo92/Downloads";
            Directory.CreateDirectory(dir);
            var path = $"{dir}/rememoria_{it.Id}.glb";
            File.WriteAllBytes(path, bytes);
            AssetDatabase.Refresh();
            Status("ok", $"Descargado en {path}. Con glTFast instalado se importa solo. Para empaquetarlo: menú «Constructor de Addressables».");
        }

        void ApplyFilter()
        {
            _filtered = Filtered();
            if (_list != null) { _list.itemsSource = _filtered; _list.Rebuild(); }
            var count = rootVisualElement.Q<Label>("count");
            if (count != null)
                count.text = _loading ? "Cargando…"
                    : _loadError != null ? "Error: " + _loadError
                    : $"{_filtered.Count} re-memoria(s)";
        }

        static void LoadThumb(Image img, string url)
        {
            if (img == null || string.IsNullOrEmpty(url)) return;
            if (_texCache.TryGetValue(url, out var cached)) { img.image = cached; return; }
            var req = UnityWebRequestTexture.GetTexture(url);
            var op = req.SendWebRequest();
            op.completed += _ =>
            {
                if (req.result == UnityWebRequest.Result.Success)
                {
                    var tex = DownloadHandlerTexture.GetContent(req);
                    if (tex != null) { _texCache[url] = tex; img.image = tex; }
                }
                req.Dispose();
            };
        }

        // ---------- pestaña Configuración ----------
        void RenderConfig(VisualElement c)
        {
            // ---- Construir mundo (mismo bloque/función que el Panel de control) ----
            var cardBuild = ReExpoUI.Card();
            cardBuild.Add(ReExpoUI.SectionTitle("Construir mundo"));
            bool cesium = WorldBuilderLocator.IsAvailable;
            cardBuild.Add(ReExpoUI.StatusBar(cesium ? "on" : "warn",
                cesium ? "Cesium for Unity detectado." : "Cesium for Unity NO instalado (com.cesium.unity)."));
            _tiles = ReExpoUI.Toggle("🌍  Maqueta de Google (referencia)", true);
            _pois = ReExpoUI.Toggle("📌  POIs (re-memorias)", true);
            _zones = ReExpoUI.Toggle("▰  Zonas del recinto", true);
            cardBuild.Add(_tiles); cardBuild.Add(_pois); cardBuild.Add(_zones);
            var build = ReExpoUI.Primary("Descargar datos y construir mundo", BuildWorldFromTools, "🏗");
            build.SetEnabled(cesium && ReExpoEditorService.IsLoggedIn);
            cardBuild.Add(build);
            if (!ReExpoEditorService.IsLoggedIn)
                cardBuild.Add(ReExpoUI.Note("Inicia sesión en el Panel de control para poder construir."));
            c.Add(cardBuild);

            var card = ReExpoUI.Card();
            card.Add(ReExpoUI.SectionTitle("Carga de la maqueta"));
            var t = ReExpoUI.Toggle("Limitar la carga a la Cartuja + alrededores (recomendado)", BoundsEnabled());
            t.RegisterValueChangedCallback(e => SetBounds(e.newValue));
            card.Add(t);
            card.Add(ReExpoUI.Note(
                "Con el límite activo, Google solo carga los tiles de la zona de la Expo (mejor rendimiento). " +
                "Desactívalo si necesitas navegar fuera del recinto. El cambio se aplica al momento."));
            c.Add(card);

            // ---- Wireframe técnico (líneas baricéntricas + arcoíris, para vídeo) ----
            var cardW = ReExpoUI.Card();
            cardW.Add(ReExpoUI.SectionTitle("Wireframe técnico"));
            var wOn = ReExpoUI.Toggle("Activar wireframe (se ve en Scene y Game)", ReExpoWire.Enabled);
            wOn.RegisterValueChangedCallback(e => ReExpoWire.Enabled = e.newValue);
            cardW.Add(wOn);

            var wRainbow = ReExpoUI.Toggle("Color arcoíris por distancia", ReExpoWire.Rainbow);
            wRainbow.RegisterValueChangedCallback(e => ReExpoWire.Rainbow = e.newValue);
            cardW.Add(wRainbow);

            var wWidth = new UnityEngine.UIElements.FloatField("Grosor de línea (px)") { value = ReExpoWire.LineWidth };
            StyleFieldLabel(wWidth);
            wWidth.RegisterValueChangedCallback(e => ReExpoWire.LineWidth = e.newValue);
            cardW.Add(wWidth);

            var wCol = new UnityEditor.UIElements.ColorField("Color de línea (si no arcoíris)") { value = ReExpoWire.LineColor, showAlpha = true, hdr = false };
            StyleFieldLabel(wCol);
            wCol.RegisterValueChangedCallback(e => ReExpoWire.LineColor = e.newValue);
            cardW.Add(wCol);

            var wFace = new UnityEngine.UIElements.FloatField("Opacidad de cara (0 = solo aristas)") { value = ReExpoWire.FaceAlpha };
            StyleFieldLabel(wFace);
            wFace.RegisterValueChangedCallback(e => ReExpoWire.FaceAlpha = e.newValue);
            cardW.Add(wFace);

            var wNear = new UnityEngine.UIElements.FloatField("Arcoíris: cerca (m)") { value = ReExpoWire.FadeNear };
            StyleFieldLabel(wNear);
            wNear.RegisterValueChangedCallback(e => ReExpoWire.FadeNear = e.newValue);
            cardW.Add(wNear);

            var wFar = new UnityEngine.UIElements.FloatField("Arcoíris: lejos (m)") { value = ReExpoWire.FadeFar };
            StyleFieldLabel(wFar);
            wFar.RegisterValueChangedCallback(e => ReExpoWire.FadeFar = e.newValue);
            cardW.Add(wFar);

            cardW.Add(ReExpoUI.Note(
                "Líneas reales del triángulo (baricéntricas horneadas por tile) → van en Game, con grosor y degradado arcoíris por distancia. " +
                "Al activarlo, los tiles se procesan poco a poco (puede dar micro-tirones); es para grabar en editor."));
            c.Add(cardW);

            var cardCat = ReExpoUI.Card();
            cardCat.Add(ReExpoUI.SectionTitle("POIs por categoría"));
            var allTog = ReExpoUI.Toggle("Mostrar TODAS las categorías", ReExpoPoiFilter.AllCategories);
            allTog.RegisterValueChangedCallback(e => { ReExpoPoiFilter.AllCategories = e.newValue; Rebuild(); });
            cardCat.Add(allTog);
            if (!ReExpoPoiFilter.AllCategories)
            {
                foreach (var cat in TypeChoices().Skip(1))
                {
                    var ct = ReExpoUI.Toggle(cat, ReExpoPoiFilter.IsOn(cat));
                    ct.RegisterValueChangedCallback(e => ReExpoPoiFilter.SetCategory(cat, e.newValue));
                    cardCat.Add(ct);
                }
                cardCat.Add(ReExpoUI.Note("Solo se ven los POIs de las categorías marcadas (por defecto, solo Pabellones). Se aplica al momento."));
            }
            c.Add(cardCat);

            var cardL = ReExpoUI.Card();
            cardL.Add(ReExpoUI.SectionTitle("POIs y carteles"));
            var tl = ReExpoUI.Toggle("Mostrar carteles (nombre) sobre cada POI", ReExpoLabels.Enabled);
            tl.RegisterValueChangedCallback(e => ReExpoLabels.Enabled = e.newValue);
            cardL.Add(tl);

            var ballRot = new UnityEngine.UIElements.Vector3Field("Rotación bola (XYZ)") { value = ReExpoLabels.BallRotOffset };
            StyleFieldLabel(ballRot);
            ballRot.RegisterValueChangedCallback(e => ReExpoLabels.BallRotOffset = e.newValue);
            cardL.Add(ballRot);

            var labRot = new UnityEngine.UIElements.Vector3Field("Rotación cartel (XYZ)") { value = ReExpoLabels.LabelRotOffset };
            StyleFieldLabel(labRot);
            labRot.RegisterValueChangedCallback(e => ReExpoLabels.LabelRotOffset = e.newValue);
            cardL.Add(labRot);

            var labPos = new UnityEngine.UIElements.Vector3Field("Posición cartel (der/arriba/fondo)") { value = ReExpoLabels.LabelPosOffset };
            StyleFieldLabel(labPos);
            labPos.RegisterValueChangedCallback(e => ReExpoLabels.LabelPosOffset = e.newValue);
            cardL.Add(labPos);

            var labFont = new UnityEngine.UIElements.FloatField("Tamaño de letra (TMP)") { value = ReExpoLabels.LabelFontSize };
            StyleFieldLabel(labFont);
            labFont.RegisterValueChangedCallback(e => ReExpoLabels.LabelFontSize = e.newValue);
            cardL.Add(labFont);

            var labMax = new UnityEngine.UIElements.FloatField("Tamaño máximo cartel (escala)") { value = ReExpoLabels.LabelMaxSize };
            StyleFieldLabel(labMax);
            labMax.RegisterValueChangedCallback(e => ReExpoLabels.LabelMaxSize = e.newValue);
            cardL.Add(labMax);

            // Un solo control de rango con dos pomos: cerca (mín) y lejos (máx).
            var radios = new UnityEngine.UIElements.MinMaxSlider("Radios cerca–lejos (m)", ReExpoLabels.NearRadius, ReExpoLabels.FarRadius, 5f, 4000f);
            StyleFieldLabel(radios);
            var radiosVal = new Label($"cerca {ReExpoLabels.NearRadius:0}  ·  lejos {ReExpoLabels.FarRadius:0} m");
            radiosVal.style.color = Ink; radiosVal.style.fontSize = 10; radiosVal.style.marginTop = 1; radiosVal.style.marginBottom = 4;
            radios.RegisterValueChangedCallback(e =>
            {
                ReExpoLabels.NearRadius = e.newValue.x;
                ReExpoLabels.FarRadius = e.newValue.y;
                radiosVal.text = $"cerca {e.newValue.x:0}  ·  lejos {e.newValue.y:0} m";
            });
            cardL.Add(radios);
            cardL.Add(radiosVal);

            cardL.Add(ReExpoUI.Note("Más cerca del «radio cercano» → tamaño máximo; más lejos del «radio lejano» → desaparece; en medio, escala. El lejano siempre se fuerza mayor que el cercano. Posición del cartel en ejes de cámara (x=derecha, y=arriba, z=al fondo)."));

            var raise = ReExpoUI.Toggle("Elevar POIs sobre edificios que los tapen", ReExpoLabels.RaiseOverBuildings);
            raise.RegisterValueChangedCallback(e => ReExpoLabels.RaiseOverBuildings = e.newValue);
            cardL.Add(raise);
            var clear = new UnityEngine.UIElements.FloatField("Holgura sobre el tejado (m)") { value = ReExpoLabels.PinClearanceMeters };
            StyleFieldLabel(clear);
            clear.RegisterValueChangedCallback(e => ReExpoLabels.PinClearanceMeters = e.newValue);
            cardL.Add(clear);
            cardL.Add(ReExpoUI.Note("La BASE del POI no se mueve. Se detecta el edificio actual sobre cada POI (malla de Google) y se estira el palo para que la bola y el cartel asomen por encima. Estos dos ajustes se aplican al RECONSTRUIR el mundo."));
            c.Add(cardL);

            // ---- Cartel LED de zona (texto que gira por el perímetro) ----
            var cardT = ReExpoUI.Card();
            cardT.Add(ReExpoUI.SectionTitle("Zonas · cartel LED"));
            var tT = ReExpoUI.Toggle("Mostrar el cartel LED girando por cada zona", ReExpoTicker.Enabled);
            tT.RegisterValueChangedCallback(e => ReExpoTicker.Enabled = e.newValue);
            cardT.Add(tT);

            var tSpeed = new UnityEngine.UIElements.FloatField("Velocidad (m/s)") { value = ReExpoTicker.Speed };
            StyleFieldLabel(tSpeed);
            tSpeed.RegisterValueChangedCallback(e => ReExpoTicker.Speed = e.newValue);
            cardT.Add(tSpeed);

            var tLetter = new UnityEngine.UIElements.FloatField("Alto de letra (m)") { value = ReExpoTicker.LetterMeters };
            StyleFieldLabel(tLetter);
            tLetter.RegisterValueChangedCallback(e => ReExpoTicker.LetterMeters = e.newValue);
            cardT.Add(tLetter);

            var tHeight = new UnityEngine.UIElements.FloatField("Altura de la banda (m)") { value = ReExpoTicker.BandMeters };
            StyleFieldLabel(tHeight);
            tHeight.RegisterValueChangedCallback(e => ReExpoTicker.BandMeters = e.newValue);
            cardT.Add(tHeight);

            var tEmis = new UnityEngine.UIElements.Slider("Fuerza del emisivo (EV)", 0f, 8f) { value = ReExpoTicker.Intensity };
            StyleFieldLabel(tEmis);
            var tEmisVal = new Label($"{ReExpoTicker.Intensity:0.0} EV");
            tEmisVal.style.color = Ink; tEmisVal.style.fontSize = 10; tEmisVal.style.marginTop = 1; tEmisVal.style.marginBottom = 4;
            tEmis.RegisterValueChangedCallback(e => { ReExpoTicker.Intensity = e.newValue; tEmisVal.text = $"{e.newValue:0.0} EV"; });
            cardT.Add(tEmis);
            cardT.Add(tEmisVal);

            cardT.Add(ReExpoUI.Note("Todo se aplica AL MOMENTO (no hace falta reconstruir). El COLOR de la letra es el de cada zona; la «fuerza del emisivo» lo realza en HDR (necesita Bloom en el Volume de URP para el halo)."));
            c.Add(cardT);

            var card2 = ReExpoUI.Card();
            card2.Add(ReExpoUI.SectionTitle("Escena"));
            card2.Add(ReExpoUI.Secondary("🎯 Recentrar en el recinto", Recenter, null));
            card2.Add(ReExpoUI.Note("¿POIs/zonas a una altura rara? Reconstruye el mundo desde el Panel de control (se colocan a ras de suelo de la Cartuja)."));
            c.Add(card2);
        }

        // ---------- pestaña Ayuda ----------
        void RenderAyuda(VisualElement c)
        {
            var card = ReExpoUI.Card();
            card.Add(ReExpoUI.SectionTitle("Qué es esta ventana"));
            card.Add(ReExpoUI.Para(
                "Tu mesa de trabajo para reconstruir la Expo 92. Arriba, las capas (POIs, zonas, maqueta de Google) " +
                "y herramientas de vista. En «Re-memorias», el catálogo de piezas a recrear."));
            c.Add(card);

            var card2 = ReExpoUI.Card();
            card2.Add(ReExpoUI.SectionTitle("Construir el recinto (Addressables)"));
            card2.Add(ReExpoUI.Para(
                "El botón ⬇ de cada re-memoria descarga su GLB aprobado a Assets/ (glTFast lo importa solo). " +
                "Para empaquetar una pieza en el recinto, usa el menú «re-Expo92 ▸ Constructor de Addressables»: " +
                "elige la pieza, valida, crea el Addressable y súbelo a revisión (cola UNITY de la web)."));
            card2.Add(ReExpoUI.Warn(
                "Si no ves el «Constructor de Addressables», instala las dependencias desde el menú " +
                "«re-Expo92 ▸ Instalar dependencias de Addressables» (Addressables + glTFast)."));
            c.Add(card2);
        }

        // ---------- acciones de capas / escena ----------
        static GameObject Rig() => GameObject.Find("ReExpo92 Rig");

        bool LayerActive(string child)
        {
            var rig = Rig(); if (rig == null) return false;
            var t = rig.transform.Find(child);
            return t != null && t.gameObject.activeSelf;
        }

        void SetLayer(string child, bool on)
        {
            var rig = Rig();
            if (rig == null) { Status("err", "No hay «ReExpo92 Rig». Constrúyelo desde el Panel de control."); return; }
            var t = rig.transform.Find(child);
            if (t == null) { Status("err", "Capa no encontrada: " + child); return; }
            t.gameObject.SetActive(on);
            Status("ok", child + (on ? " · visible" : " · oculta"));
        }

        void Recenter()
        {
            var rig = Rig();
            if (rig == null) { Status("err", "No hay «ReExpo92 Rig». Constrúyelo primero (Panel de control)."); return; }
            Selection.activeGameObject = rig;
            if (SceneView.lastActiveSceneView != null) SceneView.lastActiveSceneView.FrameSelected();
        }

        // límite de área (excluder), sin referenciar el tipo Cesium
        Behaviour Excluder()
        {
            var rig = Rig(); if (rig == null) return null;
            var tiles = rig.transform.Find("Google Photorealistic 3D Tiles");
            if (tiles == null) return null;
            return tiles.GetComponent("ReExpoBoxExcluder") as Behaviour;
        }
        bool BoundsEnabled() { var e = Excluder(); return e == null || e.enabled; }
        void SetBounds(bool on)
        {
            var e = Excluder();
            if (e == null) { Status("err", "No hay maqueta/límite en la escena. Reconstruye el mundo."); return; }
            e.enabled = on;
            Status("ok", on ? "Carga limitada a la Cartuja." : "Límite desactivado: se cargará todo el mundo.");
        }

        // ---------- datos ----------
        async void LoadItems()
        {
            _loading = true; _loadError = null;
            ApplyFilter();
            var (items, err) = await ReExpoClient.FetchReMemories();
            _loading = false;
            if (err != null) { _loadError = err; _items = new List<ReMemoryItem>(); }
            else { _items = items ?? new List<ReMemoryItem>(); _loadError = null; }
            // Estado de recreación (no crítico: si falla, simplemente no se pintan chips).
            var (states, _) = await ReExpoClient.GetUnityBuildStates();
            if (states != null) _states = states;
            if (_tab == 0) Rebuild();
        }

        List<ReMemoryItem> Filtered()
        {
            if (_items == null) return new List<ReMemoryItem>();
            IEnumerable<ReMemoryItem> q = _items;
            if (_typeFilter != "Todos") q = q.Where(i => i.Category == _typeFilter);
            if (!string.IsNullOrWhiteSpace(_search))
            {
                var s = _search.Trim().ToLowerInvariant();
                q = q.Where(i => (i.Name ?? "").ToLowerInvariant().Contains(s));
            }
            return q.ToList();
        }

        List<string> TypeChoices()
        {
            var cats = (_items ?? new List<ReMemoryItem>())
                .Select(i => i.Category).Where(c => !string.IsNullOrEmpty(c) && c != "—").Distinct().ToList();
            cats.Sort((a, b) =>
            {
                bool pa = a.ToLowerInvariant().Contains("abell"), pb = b.ToLowerInvariant().Contains("abell");
                if (pa != pb) return pa ? -1 : 1;
                return string.Compare(a, b, System.StringComparison.OrdinalIgnoreCase);
            });
            var list = new List<string> { "Todos" };
            list.AddRange(cats);
            return list;
        }

        // ---------- helpers ----------
        static string ShortDesc(string d)
        {
            if (string.IsNullOrWhiteSpace(d)) return "";
            d = d.Replace("\n", " ").Trim();
            return d.Length > 140 ? d.Substring(0, 140) + "…" : d;
        }
        static string StatusLabel(string s)
        {
            switch (s)
            {
                case "validado": return "VALIDADO";
                case "seleccionado": return "OFICIAL";
                case "completo": return "COMPLETO";
                case "en_progreso": return "EN PROGRESO";
                default: return string.IsNullOrEmpty(s) ? "—" : s.ToUpperInvariant();
            }
        }
        static string StatusKind(string s)
        {
            switch (s)
            {
                case "validado": case "seleccionado": return "ok";
                case "completo": case "en_progreso": return "warn";
                default: return "muted";
            }
        }
        static void StyleSliderLabel(Slider s)
        {
            var lbl = s.Q<Label>();
            if (lbl != null) { lbl.style.color = Ink; lbl.style.minWidth = 120; }
        }

        static void StyleFieldLabel(VisualElement field)
        {
            var lbl = field.Q<Label>();
            if (lbl != null) { lbl.style.color = Ink; lbl.style.minWidth = 150; }
        }

        static void StyleDropdown(DropdownField dd)
        {
            var lbl = dd.Q<Label>(); if (lbl != null) { lbl.style.color = Ink; lbl.style.minWidth = 40; }
            foreach (var cn in new[] { "unity-base-popup-field__input", "unity-popup-field__input", "unity-base-dropdown__input" })
            {
                var el = dd.Q(className: cn);
                if (el != null) { el.style.backgroundColor = Color.white; el.style.color = Color.black; }
            }
        }
        void Status(string kind, string text) => ReExpoUI.SetFeedback(_status, kind, text);

        // Construye el mundo con la MISMA función que el Panel de control.
        async void BuildWorldFromTools()
        {
            Status("busy", "Descargando datos y construyendo…");
            var msg = await ReExpoEditorService.BuildWorld(_tiles.value, _pois.value, _zones.value);
            bool ok = msg != null && msg.StartsWith("OK");
            Status(ok ? "ok" : "err", msg ?? "Listo.");
        }
    }
}
