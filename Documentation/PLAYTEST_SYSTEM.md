# Playtest System (v0.1.1)

Herramientas de validación **dentro del juego** para comprobar rápidamente que Breath of Eclipse arranca y es
jugable. Solo existen en el **Editor** y en **Development Builds**: el ensamblado `BreathOfEclipse.Playtest`
tiene la restricción `UNITY_EDITOR || DEVELOPMENT_BUILD`, así que no se compila en builds de release y no
sustituye al gameplay.

> Importante: estas herramientas **no juzgan si algo se ve bonito o se siente bien**. Comprueban que cada sistema
> se ejecuta y produce lo esperado (daño, estados, VFX invocados, cámara, eventos…). La calidad visual y el
> *game feel* requieren inspección humana; para eso están el modo VISUAL, el VFX Lab y las capturas.

## Arranque en un clic (Editor)

Menú **Breath of Eclipse → Playtest**:

| Opción | Qué hace |
|---|---|
| **Full Visual Test** | Abre `03_CombatTest`, entra en Play y ejecuta el FULL TEST a velocidad VISUAL (pausas para observar). |
| **Rising Serpent Visual Test** | Abre `03_CombatTest`, entra en Play, prepara el muñeco, equipa TIDAL BREATH y repite **Rising Serpent** cada 3 s en el VFX Lab. |
| Full Test (Normal / Fast) | FULL TEST con menos esperas. |
| Auto Test (CombatTest suite) | Suite automática en CombatTest (sin Boot/Menú/Bosque, con barrido de las 20 técnicas). |
| AI Test / Boss Test | Suites automáticas de Nightspawn / The Hollow Oni. |
| VFX Lab / Camera Lab / Manual Test | Laboratorios interactivos. |
| Open Reports Folder / Open Screenshots Folder | Abre `PlaytestReports/` o `PlaytestCaptures/`. |

Si ya estás en Play Mode, la opción arranca directamente la sesión en la escena actual.

## Desde el juego (Editor / Development build)

Menú principal → **DEVELOPER PLAYTEST** (el botón solo aparece en Editor / Development). Elige la velocidad
(NORMAL / FAST / VISUAL) y el modo: MANUAL TEST, AUTO TEST, VFX TEST, RISING SERPENT VISUAL, CAMERA TEST, AI TEST,
BOSS TEST o FULL TEST.

## Controles durante una sesión

| Tecla | Acción |
|---|---|
| **F8** | Captura de pantalla (en cualquier momento, Editor / Development) |
| **F9** | Pausar el test (también congela el tiempo de juego) |
| **F10** | Continuar |
| **F11** | Saltar el test actual (queda como NOT TESTED) |
| **F12** | Abortar el test / salir del laboratorio / cerrar el resumen |

F1 (debug) y F2 (FPS) siguen funcionando; las teclas nuevas no se solapan con ningún control existente.

## Modos

### FULL TEST (≈3–5 min según velocidad)

Boot → Main Menu (botones, SETTINGS) → TRAINING → CombatTest → spawn → movimiento → sprint → salto → lock-on →
ataque ligero → ataque pesado → combos LLLL / LLH / LHH / HH → Dash+L → Salto+L → Salto+H → esquiva → bloqueo →
parry simulado + Riposte → esquiva perfecta simulada + contraataque → integridad de golpes por AttackID →
feedback de combate → **Rising Serpent** → **Flash Breaker** → tercera persona → colisión de cámara → primera
persona → segunda persona → pérdida de objetivo → Nightspawn (spawn, detección, persecución, ataca al jugador,
recibe daño, knockback/lanzamiento, muerte, estados de la FSM) → The Hollow Oni (encuentro + barra, fase 1,
HP ≈ 49 % → fase 2, Eclipse Cleave, Ultimate del jugador, derrota) → HUD, pausa, debug, números de daño → audio →
guardado → todas las recetas de VFX → PLAY → Moonlit Forest → resumen de rendimiento.

- **NORMAL**: prueba funcional con pausas cortas.
- **FAST**: esperas mínimas.
- **VISUAL**: juego a 1x con pausas de observación tras cada técnica / escena (recomendado para mirar).

Si un test falla se registra y la secuencia **continúa**. Solo se aborta todo si falta el GameManager, falta el
jugador tras un intento de recuperación, una escena no carga o hay un bucle de excepciones (> 60 errores en 5 s).

### AUTO TEST

Lo mismo pero solo en CombatTest (sin Boot / Menú / Bosque / IA / Jefe) y con **barrido de las 20 técnicas**
normales y avanzadas de los 5 estilos (arranca, termina, invoca VFX y daña al muñeco si la técnica hace daño).

### AI TEST / BOSS TEST

Suites de enemigo y jefe descritas arriba. En BOSS TEST el jugador tiene God Mode (los golpes y eventos de daño
ocurren igual, pero la vida no baja de 1) para que la prueba no termine antes de tiempo; se restaura al final.
La vida del jefe se fija en ≈49 % directamente en su modelo de vida (el mismo evento que usa el juego para
activar la fase 2) y **Eclipse Cleave se fuerza** con un gancho de desarrollo (`EnemyController.ForcedNextAttackId`)
para no depender del azar.

### VFX TEST (VFX Lab)

- Los 5 estilos × Skill 1 / 2 / 3 / Advanced / Ultimate (las 25 técnicas existentes; no hay contenido nuevo).
- **REPEAT RISING SERPENT**, auto-repetición cada **2 / 3 / 5 s**.
- Velocidad de reproducción **0.25x / 0.5x / 1x / 1.5x**.
- **FREEZE VFX** (congela el tiempo del juego: partículas, trails, cintas, mallas, rayos e impactos se detienen;
  la cámara y la UI siguen) y **NEXT FRAME** (avanza un frame de 1/60 s).
- Muestra las fases de la técnica, los VFX invocados y el daño del último lanzamiento.
- Solo en el laboratorio, antes de cada repetición el jugador se recoloca en la marca del escenario para que todas
  las repeticiones se vean igual (desactivable).

FREEZE y NEXT FRAME también están en el menú **F1 → TIME**.

### CAMERA TEST (Camera Lab)

Jugador + muñeco + un Nightspawn. **1** primera persona, **2** segunda persona (fija objetivo si hace falta),
**3** tercera persona, **C** cicla como siempre (las teclas de técnica 1–3 se desactivan en el lab).
Panel con: CURRENT CAMERA, objetivo, distancia, FOV, estado de colisión, lock-on y aviso
**SECOND PERSON TARGET LOST** comprobando el regreso a tercera persona.

### MANUAL TEST

Juegas normalmente en CombatTest; una checklist se marca sola con lo que realmente ocurre (moverse, sprint, salto,
ataques, combo, esquiva, bloqueo, parry, esquiva perfecta, lock-on, técnicas 1–4, ultimate, cambio de estilo,
3 cámaras, pausa, debug, derrotar enemigo, fase 2 del jefe). **F12** termina y escribe el informe
(lo no realizado queda como NOT TESTED).

## Cómo controla el Auto Test al personaje

`AutoPlaytestDriver` no mueve al personaje directamente: escribe en la **capa de entrada simulada de
`InputReader`** (`SimulatedInput`). Las pulsaciones simuladas entran en el **mismo buffer y los mismos eventos** que
el teclado/mando, así que `PlayerController`, el combate, las técnicas y las cámaras ejecutan su código normal.
El movimiento es un vector de stick relativo a la cámara, como lo haría un jugador. No hay teletransportes salvo
para **recuperar** una prueba (jugador muerto / fuera del mundo; se registra) y el reposicionamiento opcional del
VFX Lab. El parry y la esquiva perfecta usan un **golpe enemigo sintético** entregado por el pipeline de daño real
(`Damageable → PlayerDefense`) en el momento exacto.

**Regla:** los tests no cambian valores de gameplay (daño, vida, rangos, velocidades, cooldowns). La preparación
de un test (rellenar vida/stamina/BREATH, reiniciar cooldowns como el santuario de entrenamiento) no altera la
configuración del juego.

## Resultados y reportes

Cada línea es **PASS / FAIL / WARNING / NOT TESTED**. Al terminar (o abortar) se escribe:

- `PlaytestReports/BoE_Playtest_<fecha>_<modo>.txt` — informe legible: versión, Unity, fecha, escena, FPS
  (medio / mínimo en ventanas de 0.5 s / máximo), tiempo de frame, picos, enemigos, VFX y partículas activas,
  memoria, excepciones, resumen y detalle por secciones (BOOT, MAIN MENU, SCENES, INPUT, MOVEMENT, COMBAT,
  TECHNIQUES, CAMERAS, VFX, ENEMIES, BOSS, AUDIO, UI, SAVE, WORLD, PERFORMANCE) y los errores de consola
  (Exception / Error / Assert agrupados, con pila corta).
- `PlaytestReports/BoE_Playtest_<fecha>_<modo>.json` — los mismos datos en JSON.
- `PlaytestCaptures/YYYYMMDD_HHMMSS_<Cámara>_<Momento>.png` — F8 y capturas automáticas (máximo 15 por sesión):
  Player Spawn, Light Attack, Rising Serpent anticipation / impact, Thunder attack, primera / segunda / tercera
  persona, Nightspawn combat, Hollow Oni fase 1 / fase 2, Ultimate, Moonlit Forest.

Ambas carpetas están en la raíz del proyecto (junto a `Assets/`) y **excluidas de git** (`.gitignore`).
En una Development build se crean junto al ejecutable.

Durante una sesión los ajustes se guardan en una **copia temporal**: tu archivo de ajustes real no se modifica
(los cambios hechos en Settings durante la sesión se descartan al terminar).

## Validación de escena (SCENES)

Al cargar cada escena se comprueba por **componentes** (no por nombres): exactamente una cámara de juego, un
AudioListener, GameManager, InputReader con su asset, un EventSystem con el módulo del Input System, un Canvas de
UI, URP como pipeline activo y, en escenas de juego, jugador, HUD, constructor de escena, luz direccional y niebla.

## Piezas (código)

| Clase | Papel |
|---|---|
| `RuntimePlaytestSystem` | Centro de control (DontDestroyOnLoad): sesiones, teclas F8–F12, HUD, menú DEVELOPER PLAYTEST, reporte |
| `PlaytestRunner` | Ejecuta los pasos con captura de excepciones, timeout, pausa, salto y aborto |
| `PlaytestContext` | Esperas (tiempo de juego / real), ritmo por velocidad, informes, preparación y recuperación |
| `AutoPlaytestDriver` | Conduce al jugador con la entrada simulada |
| `TelemetryRecorder` | Registra daño, hit stops, cámara, VFX, telegrafías, técnicas, parries, combo, jefe |
| `PlaytestLogger` | Resultados + errores de consola (`Application.logMessageReceivedThreaded`) + TXT/JSON |
| `RuntimePerformanceMonitor` | FPS, tiempo de frame, picos, enemigos, VFX, partículas, memoria |
| `PlaytestScreenshots` | F8 y capturas automáticas |
| `SceneSanityChecker` | Validación de escena por componentes |
| `VfxLab`, `CameraLab`, `ManualChecklistLab` | Laboratorios interactivos |
| Suites | `SceneFlowSuite`, `MovementSuite`, `CombatSuite`, `TechniqueSuite` (`Test_RisingSerpent`), `CameraSuite`, `AiSuite`, `BossSuite`, `SystemsSuite` |

Ganchos añadidos al runtime (sin efecto en gameplay): `DevTelemetry` (eventos de hit stop, cámara, VFX y
telegrafías), capa `SimulatedInput` de `InputReader`, `TimeController.DevFrozen/StepFrame`,
`EnemyController.StateChanged/ForcedNextAttackId`, `CameraRig.SecondPersonTargetLost` y lecturas de diagnóstico
(trails, audio, barra de jefe, pools).
