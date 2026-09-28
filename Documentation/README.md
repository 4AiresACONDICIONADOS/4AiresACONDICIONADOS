# BREATH OF ECLIPSE — Vertical Slice (v0.1.1)

Action RPG anime de espadachín en **Unity 6.3 LTS + URP**. Proyecto 100 % editable: sin instalador, sin
empaquetado final. Se abre con Unity Hub y se juega pulsando **PLAY**.

> Estado honesto de esta versión: el código se ha **compilado contra los ensamblados reales de Unity 6.3**
> (0 errores, 0 warnings), los shaders se han **compilado con DXC contra la ShaderLibrary de URP 17.3** y la lógica
> pura pasa **39 tests**. **El juego todavía no se ha ejecutado en el Editor de Unity** (el entorno donde se generó
> no tiene Editor). v0.1.1 añade las herramientas para hacer esa primera prueba en un clic:
> **Breath of Eclipse → Playtest → Full Visual Test** ([PLAYTEST_SYSTEM.md](PLAYTEST_SYSTEM.md)).
> Ver [DEV_STATUS.md](DEV_STATUS.md) y la sección *Known Issues* del [CHANGELOG](CHANGELOG.md).

## Requisitos

| Elemento | Versión |
|---|---|
| Unity | **6000.3.x (Unity 6.3 LTS)** — el proyecto se creó con 6000.3.25f1. Cualquier Unity 6 LTS con URP 17 debería abrirlo. |
| Render pipeline | URP 17.x (paquete del propio editor) — Forward+, HDR, SSAO |
| Paquetes | Input System 1.14.2, uGUI 2.0, Test Framework 1.6 |
| Hardware objetivo | 1080p / 60 FPS en GPU de gama media |

## Abrir y jugar

1. Unity Hub → **Add → Add project from disk** → selecciona la carpeta raíz del repositorio.
2. Abre con Unity 6.3 LTS. La primera importación compila scripts y shaders (unos minutos).
3. En la primera apertura, el editor exporta automáticamente el contenido por defecto a *ScriptableObjects*
   editables (`Assets/_BreathOfEclipse/Data/` y `Core/Resources/BreathOfEclipse/GameDatabase.asset`).
4. Abre `Assets/_BreathOfEclipse/Scenes/00_Boot.unity` (o usa el menú **Breath of Eclipse → Play From Boot**) y pulsa **PLAY**.
   Cualquier escena funciona también por separado: `GameManager` se crea antes de cargar la escena.
5. Si algo no se ve bien: **Breath of Eclipse → Setup → Validate Project** revisa URP, shaders, escenas e input.
6. **Primera validación recomendada:** **Breath of Eclipse → Playtest → Full Visual Test** recorre todo el juego
   solo y deja un informe en `PlaytestReports/` y capturas en `PlaytestCaptures/`.
   **Breath of Eclipse → Playtest → Rising Serpent Visual Test** repite la técnica para observarla.

## Escenas

| Escena | Contenido |
|---|---|
| `00_Boot` | Splash, precalentado de todos los VFX (pools), carga el menú. |
| `01_MainMenu` | PLAY / TRAINING / SETTINGS / QUIT sobre el espadachín practicando katas bajo la luna. |
| `02_MoonlitForest` | Bosque nocturno original: luna gigante, niebla, árboles, arroyo con puente, claros de combate, linternas-checkpoint, templo abandonado con **The Hollow Oni**. |
| `03_CombatTest` | Arena de entrenamiento: muñecos, destructibles, pilares/rampas y santuarios [E] para invocar Nightspawn, al jefe, o restaurar recursos. |

El mundo de cada escena se **construye proceduralmente al pulsar Play** (geometría, materiales, texturas, VFX y
audio se generan por código). Así el proyecto no depende de assets de terceros y cualquier pieza se puede sustituir
por un modelo/prefab real más adelante (ver [ARCHITECTURE.md](ARCHITECTURE.md#sustituir-placeholders)).

## Documentación

| Documento | Tema |
|---|---|
| [PLAYTEST_SYSTEM.md](PLAYTEST_SYSTEM.md) | Auto test, laboratorios VFX / cámara, reportes, capturas (v0.1.1) |
| [CONTROLS.md](CONTROLS.md) | Teclado/ratón, mando, reasignación, modos de esquiva |
| [COMBAT_SYSTEM.md](COMBAT_SYSTEM.md) | Combos, cancel windows, hit detection, hit stop, parry, esquiva perfecta, respiraciones, enemigos, jefe |
| [VFX_SYSTEM.md](VFX_SYSTEM.md) | Librería de VFX, trails, flash frames, shaders, post-proceso |
| [CAMERA_SYSTEM.md](CAMERA_SYSTEM.md) | Tercera, primera y segunda persona, cámara cinemática, shake/zoom |
| [ARCHITECTURE.md](ARCHITECTURE.md) | Capas, ensamblados, eventos, datos, decisiones técnicas |
| [CHANGELOG.md](CHANGELOG.md) | Versiones |
| [TODO.md](TODO.md) | Próximos pasos |
| [DEV_STATUS.md](DEV_STATUS.md) | Estado por fase |
| [../Tools/README.md](../Tools/README.md) | Herramientas offline (compile check, shader check, generadores) |

## Estructura

```
Assets/_BreathOfEclipse/
  Core/Resources/BreathOfEclipse/   Input actions (+ GameDatabase tras la exportación)
  Data/                             ScriptableObjects editables (se generan en la primera apertura)
  Scenes/                           00_Boot, 01_MainMenu, 02_MoonlitForest, 03_CombatTest
  Scripts/Logic/                    C# puro sin UnityEngine (modelos de vida, stamina, combos...) + tests
  Scripts/Runtime/                  Juego (namespaces BreathOfEclipse.*)
  Scripts/Playtest/                 Herramientas de playtest (solo Editor / Development builds)
  Scripts/Editor/                   Menús, exportador de contenido, validador, playtest en un clic
  Scripts/Tests/EditMode/           Tests NUnit (lógica, guardado, validación de contenido)
  Settings/                         Assets de URP (PC / Mobile), perfiles de volumen
  Shaders/                          Shaders HLSL de URP escritos a mano
Documentation/                      Esta documentación
Tools/                              Compile check offline, shader check (DXC), generadores
```

## Tests

- **Window → General → Test Runner → EditMode → Run All**: lógica de combate/recursos, sistema de guardado,
  planes de playtest y validación de contenido (cada id de animación, VFX y sonido usado por ataques, técnicas y enemigos existe; los
  combos obligatorios resuelven).
- Fuera de Unity: `dotnet test Tools/CompileCheck/LogicTests` ejecuta los tests de la capa `Logic`.
