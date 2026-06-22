# Addressables en el package — estado de integración

> Flujo de Addressables del package re-Expo92 (Fase 5-6). **Cableado entero (best-effort)**;
> falta abrir Unity para compilar y validar. No se ha compilado en el entorno del agente.

## Cómo se usa (cuando esté validado)
1. Menú **re-Expo92 ▸ Instalar dependencias de Addressables** → instala `com.unity.addressables` + `com.unity.cloud.gltfast`. Unity recompila → se activa `REEXPO_ADDR` → aparece el Constructor.
2. Menú **re-Expo92 ▸ Constructor de Addressables**:
   - **1 Pieza**: escribe el `re_memory_id` o pulsa «Elegir…» (lista `FetchReMemories`). Avisa si la pieza ya tiene Addressable / está desactualizado. «Bajar modelo aprobado» descarga el GLB a `Assets/ReExpo92/Downloads/` (glTFast lo importa solo).
   - **2 Objeto**: arrastra el prefab/GLB importado (asset del proyecto).
   - **3 Validar**: pivote, escala, triángulos, LODs, materiales URP — con auto-fix.
   - **4 Script** (opcional): un `TextAsset` (.cs/.dll.bytes) que viaja en el bundle.
   - **5 Crear / Construir y subir**: configura el grupo remoto, hace `BuildPlayerContent`, sube `ServerData/<target>/**` al bucket `model-addressables` (token real del colaborador) y registra el build con `submit_unity_build` (cola UNITY).
3. En **ToolsWindow**, el botón ⬇ de cada re-memoria descarga su GLB aprobado.

## Archivos
**Runtime (sin defines):**
- `ReExpoClient.cs` — añadido: `AccessTokenOrAnon`, `GetUnityBuild`, `GetApprovedGlbUrl`, `SubmitUnityBuild`, modelo `UnityBuildInfo`.
- `SupabaseRest.cs` — añadido: `Download(url)` (bytes), `StoragePut(objectPath, bytes, bearer)`.

**Editor (base):**
- `ReExpoEditorService.cs` — añadido: `InstallAddressables()`, `InstallGltfast()` (`Client.Add`).
- `BuildDepsInstaller.cs` — MenuItem para instalar ambas deps.
- `ToolsWindow.cs` — el stub ⬇ ahora descarga el GLB real; banner de Ayuda actualizado.

**Editor/Addressables (`#if REEXPO_ADDR`, asmdef con defineConstraint):**
- `AddressableBuilderWindow.cs` — ventana guiada completa.
- `AddressableUploadService.cs` — subida async + URL del catálogo.

## ⚠️ Validar al abrir Unity (lo más sensible, por la API de Addressables)
`ConfigureAndMark()` en `AddressableBuilderWindow.cs` usa la API de Addressables-editor, que **varía entre versiones**. Revisar contra la versión instalada:
- `AddressableAssetSettingsDefaultObject.GetSettings(true)`.
- `settings.profileSettings.SetValue(profileId, "RemoteLoadPath"/"RemoteBuildPath", …)`.
- `settings.BuildRemoteCatalog`, `settings.RemoteCatalogBuildPath/LoadPath.SetVariableByName(settings, …)`.
- `settings.CreateGroup(name, false, false, true, null, typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema))`.
- `BundledAssetGroupSchema`: `Compression = BundleCompressionMode.LZ4`, `BundleNaming = BundleNamingStyle.AppendHash`, `BuildPath/LoadPath.SetVariableByName(...)`.
- `settings.CreateOrMoveEntry(guid, group)` + `entry.address`.
- `AddressableAssetSettings.BuildPlayerContent(out var result)` + `result.Error`.

Si algún nombre/firma cambió en tu versión de Addressables, ajústalo ahí (todo está en un `try/catch` con mensaje claro).

## Revisión, estados y rol de revisor (jun-2026)
- **Subida = propuesta**: `submit_unity_build` crea el build en `pendiente`. Se valida por el **motor de consenso unificado** (subject `unity_build`, quórum configurable; ausente = 1 voto). El primer rechazo es terminal y SIEMPRE con motivo; no puedes revisar tu propio build (lo bloquea el servidor).
- **Rol nuevo `revisor_unity`** (gated, se concede por solicitud en /colabora). Mapea en `can_review_subject('unity_build')`. La RLS de `re_memory_unity_builds` deja ver los pendientes a `revisor_unity`/staff/unity_dev; los aprobados, a todos.
- **Estados en ToolsWindow**: cada pieza muestra un chip — `modelo 3D` (hay modelo, falta Addressable), `recreado ✓` (Addressable aprobado), `desactualizado` (modelo nuevo sin reconstruir). Datos de `get_unity_build_states`.
- **Ventana de revisor** (`AddressableReviewerWindow`, menú «Revisión de Addressables»): solo para `revisor_unity`/staff. Lista pendientes (`get_pending_unity_builds`), **previsualiza** el Addressable en escena (carga catálogo remoto + instancia) y vota (`cast_review_vote`). ⚠️ La carga remota en editor puede requerir modo Play en algunas versiones (best-effort, try/catch).
- **Descargar aprobados (unity_dev)**: el Constructor permite «Cargar Addressable aprobado en escena» de cualquier pieza con build aprobado (inspeccionar o partir de él para actualizar).
- **Seguridad**: la visibilidad de las ventanas es solo UX. La autoridad la pone el SERVIDOR: `cast_review_vote`/`can_review_subject` exigen rol (JWT firmado, no falsificable) y bloquean auto-revisión. No es hackeable desde el cliente.

## Notas
- **Auth**: la subida usa `ReExpoClient.AccessTokenOrAnon` (token del colaborador `unity_dev`); la RLS del bucket exige `is_unity_dev()`.
- **Bucket**: `model-addressables/rememoria_<id>/<BuildTarget>/…`. Catálogo público = `…/object/public/model-addressables/rememoria_<id>/<BuildTarget>/catalog*.bin`.
- **Build target**: WebGL (motor único). El token `[BuildTarget]` del RemoteLoadPath se resuelve a `WebGL`.
- **glTFast**: registra un importador de `.glb`, así que **no llamamos a su API** — solo descargamos el fichero. Evita el problema de referencias opcionales de asmdef.
- **HybridCLR**: NO va en el package (solo empaqueta el script como TextAsset). Vive en el proyecto «player» del creador.
- **.meta**: Unity generará los `.meta` de los archivos nuevos al abrir el proyecto.
