# re-Expo92 · WorldKit

> Maqueta 3D **georreferenciada** de la Isla de la Cartuja para reconstruir la **Expo 92 de Sevilla** en Unity.

![Unity](https://img.shields.io/badge/Unity-6.3%20LTS%20(6000.3.17f1)-black?logo=unity)
![Pipeline](https://img.shields.io/badge/Render-URP-2f44a0)
![License](https://img.shields.io/badge/License-MIT-FF6B35)

**re-Expo92 WorldKit** monta en tu escena de Unity una maqueta fotorrealista de la Cartuja de hoy (Google Photorealistic 3D Tiles vía **Cesium for Unity**) y, encima, los **POIs y zonas reales** del proyecto descargados en vivo desde Supabase. Así puedes **modelar cada pabellón en su sitio exacto**, a escala real (1 u = 1 m) y en coordenadas del mundo (WGS84) — la misma base que usará el AR sobre el recinto el día de mañana.

Es la **mesa de trabajo** del colaborador, no el resultado: la maqueta de Google es solo **referencia de editor** (no entra en builds ni se redistribuye).

---

## ✨ Qué hace

- 🌍 **Mundo de referencia** — la Cartuja real de hoy, fotorrealista, sobre la que situarte.
- 📌 **POIs** — cada re-memoria con coordenada aparece como chincheta donde estuvo.
- ▰ **Zonas** — los polígonos del recinto, dibujados sobre el terreno.
- 🧭 **Georreferencia exacta** — origen del recinto fijo, escala 1 u = 1 m, WGS84. La misma coordenada cae en el mismo sitio en la web, en Unity y en AR.
- 🪟 **Herramientas de editor** — asistente de configuración y panel de control con estética Windows 95 + Expo 92.
- 🔐 **Sin secretos en el repo** — la sesión y los permisos se resuelven contra Supabase (RLS); el package es público y limpio.

---

## 📦 Requisitos

| | |
|---|---|
| **Editor** | Unity **6.3 LTS** (recomendado `6000.3.17f1`) |
| **Render** | **URP** (Universal Render Pipeline) |
| **Cesium for Unity** | `com.cesium.unity` — **el asistente lo instala por ti** (o manual, ver abajo) |
| **Newtonsoft Json** | `com.unity.nuget.newtonsoft-json` — entra solo como dependencia |
| **Cuenta** | Cuenta de colaborador de re-Expo92 + perfil de **desarrollador Unity** (lo concede el administrador) |

---

## 🚀 Instalación

1. **Añade el package** — `Window ▸ Package Manager ▸ + ▸ Add package from git URL`:
   ```
   https://github.com/FernandoOleaDev/reexpo92-unity-kit.git
   ```
2. Al primer arranque se abre el **Asistente de configuración** (`re-Expo92 ▸ Asistente de configuración`).
3. En el paso **Requisitos**, si falta Cesium, pulsa **«Instalar Cesium for Unity»** → lo añade y Unity lo descarga y recompila solo.

> **Cesium a mano** (opcional): `Edit ▸ Project Settings ▸ Package Manager` → Scoped Registry · Name `Cesium` · URL `https://unity.pkg.cesium.com` · Scope `com.cesium.unity` → instala desde `Window ▸ Package Manager ▸ My Registries`. ([quickstart oficial](https://cesium.com/learn/unity/unity-quickstart/))

---

## 🧭 Uso

Todo cuelga del menú **`re-Expo92`** (arriba, junto a Window/Help):

### Asistente de configuración
Te lleva de la mano en 5 pasos: **Bienvenida → Requisitos → Tu cuenta → Acceso de desarrollador → Construir**.
- Instala Cesium si falta, te loguea, verifica tu acceso de desarrollador (automático) y construye el mundo.
- No deja avanzar de «Tu cuenta» sin iniciar sesión.

### Panel de control
El día a día:
- **Tu cuenta** — login con email o Google.
- **Construir mundo** — activa/desactiva capas (🌍 maqueta · 📌 POIs · ▰ zonas) y pulsa **«Descargar datos y construir mundo»**.
- Aparece un objeto **`ReExpo92 Rig`** en la jerarquía. Selecciónalo y pulsa <kbd>F</kbd> en la Scene para encuadrar la Cartuja.

A partir de ahí, importa tus modelos (GLB/FBX) y colócalos usando los marcadores como referencia. Eso es tu recreación.

---

## 🔧 Cómo funciona

- **Datos** — la RPC pública `export_map_geojson` devuelve POIs + zonas en WGS84 con el origen del recinto y la escala. El package los parsea e instancia anclados con Cesium (`CesiumGlobeAnchor`).
- **Georreferencia** — `Runtime/Geo.cs` replica la fundación geodésica de la web (`src/lib/geo.ts`): origen `37.4055, -6.0035`, ENU en metros, 1 u = 1 m. Es la única fuente de verdad, compartida entre web, Unity y el futuro AR.
- **Maqueta de Google** — se sirve por *streaming* en el editor (`Cesium3DTileset` `FromUrl`). La credencial **no vive en el repo**: se entrega al loguearte, solo si tienes el perfil de desarrollador. Nunca se escribe a disco ni a log.

```
Runtime/                  núcleo (geo, REST de Supabase, modelos, importer, interfaz)
Runtime/Cesium/           constructor con Cesium (compila solo si com.cesium.unity está)
Editor/                   asistente + panel de control (UI Toolkit, estilos inline)
Samples~/DemoCartuja/     ejemplo
```

---

## 🔒 Seguridad y permisos

- La **anon key** de Supabase es pública por diseño; la **RLS** del servidor protege los datos (igual que en la web).
- **De momento, solo usuarios logueados** pueden descargar; subir y colaborar requiere los permisos correspondientes.
- El acceso a la maqueta requiere el perfil de **desarrollador Unity**, que concede el administrador desde la web.
- El streaming de la maqueta de Google **no se cachea ni se redistribuye** (lo prohíben sus términos): es referencia en vivo dentro del editor.

---

## 🛠️ Para mantenedores

El package se desarrolla **embebido en un proyecto Unity** (mutable) para que el editor genere y mantenga los `.meta`. Patrón recomendado: enlaza este repo dentro de `Packages/` del proyecto Unity

```bash
ln -s /ruta/al/repo/reexpo92-unity-kit  /ruta/al/proyecto-unity/Packages/com.reexpo92.worldkit
```

y commitea aquí los `.meta` que Unity genere — son **obligatorios** para que el package funcione instalado por git URL (las carpetas de package son inmutables y Unity no los crea ahí).

---

## 📄 Licencia

[MIT](LICENSE) · © re-Expo92

Parte del proyecto [re-Expo92](https://github.com/FernandoOleaDev/re-Expo92) — recreación 3D colaborativa de la Expo 92 de Sevilla.
