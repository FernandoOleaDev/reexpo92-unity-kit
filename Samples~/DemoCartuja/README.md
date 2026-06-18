# Demo Cartuja

Ejemplo mínimo para construir el rig georreferenciado de la Isla de la Cartuja
con los POIs y zonas reales de re-Expo92.

## Cómo usarlo

1. Asegúrate de tener **Cesium for Unity** instalado y de haber completado el
   asistente (`Tools ▸ re-Expo92 ▸ Asistente de configuración`): login + acceso
   a la clave de Google.
2. Crea o abre una escena vacía (URP).
3. Opción A — menú: `Tools ▸ re-Expo92 ▸ Samples ▸ Construir demo Cartuja`.
   Opción B — panel: `Tools ▸ re-Expo92 ▸ Panel de control` → «Descargar datos y
   construir mundo».
4. Aparecerá un objeto **ReExpo92 Rig** con la georreferencia en el origen del
   recinto, la maqueta de Google (referencia de editor) y los marcadores de POIs
   y zonas. Encuádralo en la Scene view (tecla F sobre el rig).

> La maqueta de Google es **solo referencia de editor**: no entra en builds ni se
> redistribuye. Construye tu recreación encima y guárdala como tus propios GLB.
