# DEV STATUS — Breath of Eclipse

**Versión actual: v0.2.1 — Anime Visual Overhaul** · Editor objetivo: Unity 6000.3.25f1 (6.3 LTS) · URP 17

- **Working (probado por el usuario en Unity 6.3, v0.1.1):** arranque de `03_CombatTest`, personaje, escenario, ataques y técnicas.
- **Working (código compilado, 0 errores / 0 warnings; SIN verificar en Unity):**
  - v0.2.0: formas variables + rueda (F), Agua con 7 formas, voz en español (45 clips), fase 2 del Oni.
  - v0.2.1: shader `TidalWaterAnime`, `WaterRibbonRenderer`, `WaterSerpentRenderer` con cabeza, espuma anime,
    VFX propio para cada forma del Agua, Rising Serpent y ultimate rehechos, voz sincronizada por fase.
  - Combat feel (hit stop, cancel windows, cuerpo entero en cada golpe), protagonista y demonios con más detalle,
    adaptador para modelo humanoide (`CharacterVisualProfile`), bosque más denso con niebla y static batching,
    pulido de cámara (3.ª/1.ª/2.ª persona) y Flash Breaker del Trueno como técnica hero propia.
- **Incomplete:** modelos 3D reales y animaciones Mecanim (adaptador listo, ningún modelo incorporado);
  resto de formas del Trueno sin rediseño hero.
- **Next:** probar v0.2.1 en Unity (Rising Serpent, forma VII y ultimate del Agua, Flash Breaker, bosque, FPS).

## ✅ PLAYTEST INFRASTRUCTURE READY

v0.1.1 añade el sistema de validación en tiempo de ejecución (ver [PLAYTEST_SYSTEM.md](PLAYTEST_SYSTEM.md)).
Primera prueba recomendada al abrir el proyecto:

1. **Breath of Eclipse → Playtest → Full Visual Test** (≈5 min, sin tocar el teclado).
2. Revisar `PlaytestReports/…txt` y las capturas de `PlaytestCaptures/`.
3. **Breath of Eclipse → Playtest → Rising Serpent Visual Test** para observar la técnica una y otra vez.
4. Compartir el informe: con él se corrigen los fallos y se prepara v0.2.0.

## Cómo se verificó esta versión (sin Unity)

| Verificación | Resultado |
|---|---|
| Compilación runtime (player build, sin `UNITY_EDITOR`) contra `UnityEngine.*.dll` de Unity 6.3 + Input System + uGUI + URP | ✅ 0 errores, 0 warnings |
| Compilación del ensamblado de playtest como **Development build** separado (valida límites entre ensamblados) | ✅ 0 errores, 0 warnings |
| Compilación editor + playtest + tests (con `UNITY_EDITOR`, `UnityEditor.*.dll`, NUnit de Unity) | ✅ 0 errores, 0 warnings |
| Tests de la capa Logic (`dotnet test`) | ✅ 39/39 |
| Tests EditMode (lógica, guardado, contenido, planes de playtest) | ⏳ escritos; se ejecutan en el Test Runner de Unity |
| Shaders (13) compilados con DXC contra la ShaderLibrary de URP 17.3 | ✅ 66 compilaciones, 0 fallos |
| **Abrir y jugar en el Editor de Unity** | ❌ **No realizado** |

La v0.2.0 empezará después de la primera prueba visual real.

## Estado por fase

| Fase | Contenido | Estado |
|---|---|---|
| 1 | Proyecto URP, input, movimiento, cámara en tercera persona, combate base, muñeco de entrenamiento | ✅ Implementado |
| 2 | Combos, cancel windows, esquiva + esquiva perfecta, bloqueo, parry, hit stop, efectos de cámara | ✅ Implementado |
| 3 | BreathingStyleSystem, TIDAL y THUNDER con Rising Serpent y Flash Breaker, VFX de elemento, trails | ✅ Implementado |
| 4 | Primera y segunda persona, cámara cinemática, post-proceso dinámico, flash frames | ✅ Implementado |
| 5 | Nightspawn (FSM), EncounterDirector, lock-on, The Hollow Oni con fase 2 | ✅ Implementado |
| 6 | EMBER, GALE y MOONLIGHT; ultimates de los 5 estilos | ✅ Implementado |
| 7 | Moonlit Forest, menú, HUD, ajustes, audio, guardado, debug, documentación | ✅ Implementado |
| v0.1.1 | Playtest tooling: auto test, laboratorios VFX / cámara, IA, jefe, capturas, reportes, rendimiento | ✅ Implementado (compilado, no ejecutado) |
| — | Prueba de juego real, ajuste fino y rendimiento en el Editor | ⏳ Siguiente paso: Full Visual Test (ver arriba) |

## Sesión de prueba solicitada para v0.1.0 (2026-09-28) — NO REALIZADA

Se pidió abrir el proyecto en Unity 6.3 y jugarlo. **No fue posible en el entorno de desarrollo**: no tiene el
Editor de Unity instalado, ni licencia de Unity para activarlo, ni GPU. Por tanto **ninguna casilla de juego está
verificada** (inicio, menú, CombatTest, bosque, combate, Rising Serpent, cámaras, IA, jefe, VFX, audio, UI, FPS).

En su lugar se hizo una auditoría estática de errores que solo aparecen en ejecución:

| Comprobación | Resultado |
|---|---|
| Uso de la API `UnityEngine.Input` antigua (el proyecto usa solo Input System: lanzaría excepciones cada frame) | ✅ ninguno |
| EventSystem con `InputSystemUIInputModule` | ✅ |
| Tags y capas usados en código vs `TagManager` | ✅ coinciden |
| Nombres de acciones/mapas que busca `InputReader` vs el `.inputactions` | ✅ coinciden |
| Partículas: `duration` modificado con el sistema parado | ✅ |
| Orden de `AddComponent` vs `RequireComponent` (componentes duplicados) | ✅ |
| Reasignación de teclas desactiva la acción antes de reasignar | ✅ |
| `fixedDeltaTime` nunca llega a 0 al pausar | ✅ |
| Guardas de `CharacterController.Move` en controladores desactivados | ✅ |
| Campos de datos serializables (el contenido exportado a assets se comporta igual que el generado por código) | ✅ |
| AudioListener en `00_Boot` | ❌ → **corregido** (Unity avisaba en cada frame del splash) |

Para poder jugarlo desde el entorno de desarrollo haría falta: instalar el Editor Linux 6000.3.25f1 en el script de
configuración del entorno (~4.5 GB de descarga), una licencia de Unity como variable de entorno del entorno (nunca
en el chat) y renderizado por software (Xvfb + Mesa). Incluso así, las cifras de FPS no representarían hardware real.

## Riesgos principales para la primera apertura

1. **Ajuste fino**: valores de movimiento, cámara y VFX están razonados pero no probados a mano.
2. **Iluminación nocturna**: intensidad de luna/ambiente/bloom puede necesitar retoque según el monitor.
3. **Shaders**: validados en HLSL; ShaderLab (bloques Properties/estados) se valida al importar.

## Dónde mirar si algo falla

- *Breath of Eclipse → Setup → Validate Project* (URP activo, shaders, escenas, input, base de datos).
- Consola: los sistemas registran avisos con prefijo `[Breath of Eclipse]`, `[MaterialFactory]`, `[GameManager]`...
- F1 → *Show Hitboxes* para depurar golpes; F2 para FPS.
