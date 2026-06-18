# re-Expo92 WorldKit · `com.reexpo92.worldkit`

Mundo 3D **georreferenciado** de la Isla de la Cartuja para construir la
recreación de la **Expo 92** en Unity. Importa los **POIs y zonas** reales del
proyecto desde Supabase y monta la **maqueta fotorrealista** de la Cartuja de hoy
(Google Photorealistic 3D Tiles vía Cesium for Unity) como **referencia de
editor** sobre la que modelar a su sitio exacto. Base, además, del futuro AR
sobre el recinto.

> Paquete para **desarrolladores/colaboradores** de re-Expo92. La maqueta de
> Google es solo andamiaje de autoría: **no entra en builds públicos ni se
> redistribuye**.

---

## Requisitos

- **Unity 6.3 LTS — `6000.3.17f1`** (versión fijada del proyecto).
- **Render pipeline: URP.**
- **Cesium for Unity** (`com.cesium.unity`, v1.23.x) — instálalo por su *scoped
  registry* (ver Instalación). Sin él, el package compila pero no puede montar la
  maqueta 3D.
- **Newtonsoft Json** (`com.unity.nuget.newtonsoft-json`) — se resuelve solo como
  dependencia.
- Una **cuenta de colaborador de re-Expo92** y el **perfil de desarrollador
  Unity** (lo concede el administrador del proyecto).

## Instalación

1. **Cesium for Unity** ([quickstart oficial](https://cesium.com/learn/unity/unity-quickstart/)):
   `Edit ▸ Project Settings ▸ Package Manager` → añade un Scoped Registry — Name
   `Cesium`, URL `https://unity.pkg.cesium.com`, Scope `com.cesium.unity` → Save.
   Luego `Window ▸ Package Manager ▸ My Registries ▸ Cesium for Unity ▸ Install`.
   (No hace falta cuenta de Cesium ion: usamos la clave de Google directa.)
2. Añade este package por **Package Manager ▸ + ▸ Add package from git URL**:
   `https://github.com/FernandoOleaDev/reexpo92-unity-kit.git`
3. Al primer arranque se abre el **Asistente de configuración**
   (`Tools ▸ re-Expo92 ▸ Asistente de configuración`).

## Puesta en marcha

1. **Asistente**: revisa requisitos → inicia sesión (email o Google) → comprueba
   acceso a la clave → construye el mundo.
2. Si «comprobar acceso» dice que no estás autorizado, pide al administrador que
   te dé el **perfil de desarrollador Unity** (web: `/admin/unity`).
3. Para el día a día usa el **Panel de control** (`Tools ▸ re-Expo92 ▸ Panel de
   control`): login, capas (maqueta / POIs / zonas) y «construir mundo».

## Cómo funciona

- **Datos** (POIs/zonas): RPC pública `export_map_geojson` (WGS84 + origen del
  recinto + escala 1u=1m). Se parsean y se instancian anclados con Cesium.
- **Maqueta de Google**: la API key **no está en este repo**. Al loguearte, la RPC
  `get_my_unity_key` te entrega la clave **ofuscada** (solo si tienes
  `unity_dev`); el package la decodifica en memoria y la pasa al `Cesium3DTileset`
  (`FromUrl` → `tile.googleapis.com`). Nunca se escribe a disco ni a log.
- **Geodesia**: `Runtime/Geo.cs` replica `src/lib/geo.ts` (origen
  `37.4055, -6.0035`, ENU en metros) para que una coordenada caiga en el mismo
  sitio que en la web y, el día de mañana, en AR.

## Seguridad y costes

- La **anon key** de Supabase es pública por diseño (la RLS protege los datos).
- La **clave de Google** es central; el seguro frente a abuso es el **tope de
  gasto** configurado en Google Cloud. La ofuscación es solo anti-casual.
- No se cachean ni redistribuyen los tiles de Google (sus términos lo prohíben):
  se hace **streaming en vivo** en el editor.

## Estructura

```
Runtime/                     core (geo, REST, modelos, importer, interfaz)
Runtime/Cesium/              implementación Cesium (compila solo con com.cesium.unity)
Editor/                      asistente + panel de control (UI Toolkit)
Samples~/DemoCartuja/        ejemplo
```

## Estado

`0.1.0` — primera versión. Código escrito siguiendo las APIs de Unity 6.3 /
Cesium 1.23 / Supabase REST; **pendiente de prueba en el editor** (ver el informe
de implementación del proyecto). Issues y PRs, bienvenidos.

## Licencia

MIT.
