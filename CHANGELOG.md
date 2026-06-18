# Changelog

Todas las novedades relevantes de **re-Expo92 WorldKit**.
El formato sigue [Keep a Changelog](https://keepachangelog.com/) y
[SemVer](https://semver.org/lang/es/).

## [0.1.0] — 2026-06-15

### Añadido
- Estructura UPM inicial (`com.reexpo92.worldkit`) para Unity 6.3 LTS (6000.3.17f1) + URP.
- Cliente REST de Supabase sobre `UnityWebRequest` (login email/contraseña y Google
  OAuth PKCE por loopback; llamadas a RPC). Sin dependencias pesadas.
- Fundación geodésica (`Geo`): puerto de `geo.ts` (origen del recinto, ENU 1u=1m) +
  deofuscación de la clave de Google.
- Importador de datos: parser del `export_map_geojson` (POIs + zonas).
- Constructor del mundo con **Cesium for Unity** (ensamblado opcional, solo compila
  con `com.cesium.unity`): `CesiumGeoreference` + Google Photorealistic 3D Tiles
  (referencia de editor) + marcadores anclados de POIs y zonas.
- Herramientas de editor (UI Toolkit): **asistente de configuración** y **panel de
  control**; apertura automática del asistente en el primer arranque.
- Sample «Demo Cartuja».

### Notas
- La clave de Google **no viaja en el repo**: se descarga al loguearse (ofuscada),
  solo para quien tenga el perfil `unity_dev` (lo concede el administrador en la web).
- El seguro real frente a abuso es el **tope de gasto** en Google Cloud.
