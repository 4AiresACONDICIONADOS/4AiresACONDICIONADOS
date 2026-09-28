# Changelog

Formato: Version / Added / Changed / Fixed / Known Issues. Versionado semántico; **v0.2.0 queda reservada para la
primera versión verificada como jugable dentro del Editor de Unity.**

---

## v0.1.2 — Runtime stabilization (2026-09-28)

### Fixed
- `CameraRig`: `Random.value` se llamaba desde un inicializador de campo (excepción al construir) y dejaba `_shaker` /
  `_zooms` en null; ahora se crean en `Awake`.
- Pantalla azul al pulsar Play: la escena queda en negro hasta que termina de compilar sus shaders (Editor) y hace
  fundido (`SceneLoader.HoldBlackUntilReady`).
- Jugador tumbado mientras atacaba: todos los cambios de estado pasan por `SetState`; salir de Knockdown limpia la pose.
  Knockdown = suelo (`knockdownGroundTime` 0.55 s) → levantarse (`getUpDuration` 0.45 s, invulnerable, solo esquiva).
  Un golpe ligero ya no saca al jugador del knockdown; un flinch no acorta un stagger pesado.
- Cancelar un ataque o técnica por un golpe detiene el lunge, el air hang y la cámara lenta de la técnica.

### Added
- F1: estado del jugador (Grounded, CanAct, pose de knockdown, cámara, técnica) y PLAYER HIT TEST.

## v0.1.1 — Playtest tooling (2026-09-28)

Sin contenido de juego nuevo: herramientas para validar v0.1.0 en Unity con el mínimo esfuerzo.

### Added

- **RuntimePlaytestSystem** (Editor / Development builds únicamente, ensamblado `BreathOfEclipse.Playtest`):
  modos MANUAL, AUTO, VFX, CAMERA, AI, BOSS, FULL y Rising Serpent Visual; velocidades NORMAL / FAST / VISUAL;
  botón **DEVELOPER PLAYTEST** en el menú principal; HUD de validación arriba a la izquierda
  (TEST n / N, test actual, estado, FPS, errores, últimos resultados).
- **FULL TEST** automático en Play Mode real: Boot → Menú → CombatTest → movimiento, sprint, salto, ataques, combos
  obligatorios, esquiva, bloqueo, parry y esquiva perfecta simulados, lock-on, Rising Serpent, Flash Breaker,
  3 cámaras, Nightspawn, The Hollow Oni (fase 2, Eclipse Cleave, ultimate, derrota), UI, audio, guardado,
  VFX y Moonlit Forest. Resultados PASS / FAIL / WARNING / NOT TESTED con recuperación y continuación ante fallos.
- **AutoPlaytestDriver**: conduce al jugador a través de la nueva capa `SimulatedInput` de `InputReader`
  (mismo buffer y eventos que el teclado; sin duplicar lógica de combate).
- **Test_RisingSerpent**: 12 comprobaciones (inicio, estado, fases, dash, VFX, trails, daño, lanzamiento, cámara,
  hit stop, final, control devuelto).
- **VFX Lab**: 25 técnicas por estilo y ranura, REPEAT RISING SERPENT, auto-repetición 2 / 3 / 5 s, velocidad
  0.25x–1.5x, FREEZE VFX y NEXT FRAME (también en F1 → TIME).
- **Camera Lab**: teclas 1 / 2 / 3 + C, diagnóstico (modo, objetivo, distancia, FOV, colisión, lock-on) y aviso
  SECOND PERSON TARGET LOST con comprobación del regreso a tercera persona.
- **Manual Test**: checklist que se marca sola al jugar.
- **PlaytestLogger** (TXT + JSON en `PlaytestReports/`, captura de Exception / Error / Assert),
  **RuntimePerformanceMonitor** (FPS medio / mínimo / máximo, tiempo de frame, picos, enemigos, VFX, partículas,
  memoria), **capturas** F8 y automáticas (≤15) en `PlaytestCaptures/`, **SceneSanityChecker** por componentes.
- Teclas de sesión: F8 captura, F9 pausa, F10 continuar, F11 saltar test, F12 abortar / salir.
- Menú de Editor **Breath of Eclipse → Playtest** (Full Visual Test, Rising Serpent Visual Test, etc.): abre
  CombatTest, entra en Play y arranca la sesión en un clic.
- Tests EditMode de los planes de playtest; proyecto de compile check del ensamblado de playtest como Development build.
- `Documentation/PLAYTEST_SYSTEM.md`.

### Changed

- Ganchos de observación sin efecto en gameplay: `DevTelemetry` (hit stop, cámara, VFX, telegrafías),
  `InputReader` (entrada simulada, supresión de teclas / mirada para laboratorios), `TimeController`
  (`DevFrozen`, `StepFrame`), `EnemyController` (`StateChanged`, `ForcedNextAttackId`), `CameraRig`
  (`SecondPersonTargetLost`, diagnóstico), lecturas de diagnóstico en `PlayerController`, `AudioManager`,
  `HUDController` y `PoolManager`; `MainMenuController` expone sus botones y un punto de extensión.
- Versión 0.1.1. Los ajustes durante una sesión de playtest se guardan en una copia temporal.

### Fixed

- `00_Boot`: faltaba AudioListener en la cámara del splash (aviso en cada frame).

### Known Issues

- **El juego sigue sin haberse ejecutado en Unity** (no hay Editor en el entorno de desarrollo). El sistema de
  playtest está compilado pero **tampoco se ha ejecutado**; su primera ejecución puede revelar ajustes necesarios
  en los propios tests (tiempos de espera, distancias).
- Los tests automáticos no evalúan calidad visual, sensación ni sonido: requieren revisión humana (modo VISUAL,
  VFX Lab, capturas).
- NEXT FRAME avanza aproximadamente un frame de 1/60 s (depende del orden de actualización de Unity).

---

## v0.1.0 — Vertical slice inicial (2026-09-28)

### Added

- **Proyecto** Unity 6.3 LTS (6000.3.25f1) + URP 17 (Forward+, HDR, SSAO, sombras suaves con 4 cascadas),
  Input System 1.14, uGUI. Assets de URP PC (High/Ultra) y Mobile (Low). Color grading HDR.
- **Escenas**: `00_Boot`, `01_MainMenu`, `02_MoonlitForest`, `03_CombatTest` en Build Settings (Boot primero).
  Cualquier escena se juega directamente con PLAY.
- **Input**: acciones completas teclado/ratón + mando, input buffer, reasignación en Settings, 3 modos de esquiva.
- **Personaje**: maniquí procedural con katana, animación procedural por poses con IK (pies, manos, mirada, espada),
  locomoción (andar, correr, salto, caída), adaptador Mecanim para modelos reales.
- **Combate**: combos LLLL, LLH, LHH, HH, Dash+L, Salto+L (combo aéreo), Salto+H, Esquiva perfecta+L, Parry+H;
  cancel windows por ataque; sweeps de hoja con AttackID; hit stop 0.025/0.045/0.06/0.08/0.1 sin congelar la UI;
  esquiva con i-frames; **esquiva perfecta** (×0.25, 0.4 s); bloqueo; **parry** con stagger y contraataque;
  stamina; barra **BREATH**; críticos; reacciones (light, heavy, knockback, launch, knockdown, stun); estados alterados.
- **BreathingStyleSystem**: 5 estilos originales (TIDAL, THUNDER, EMBER, GALE, MOONLIGHT) × (3 técnicas + 1 avanzada +
  1 ultimate) = 25 técnicas data-driven por fases. Técnicas de referencia **Rising Serpent** y **Flash Breaker** según
  el diseño. Ultimates con cámara cinemática en gameplay (saltables).
- **VFX**: librería de ~60 efectos procedurales con pooling (agua, fuego, trueno, viento, luna, impactos, ondas de
  choque, polvo, grietas, debris, portales, auras), trails de espada + elemento, afterimages, speed lines,
  **flash frames** anime (2 estilos), distorsión de pantalla, telegrafías de ataque.
- **Shaders URP** (HLSL): ToonLit (cel shading 2 pasos, rim, especular estilizado, emisión, outline dependiente de la
  distancia, dissolve, hit flash), VFXAdditive, VFXAlphaBlend, ElementRibbon, SwordTrail, Ghost, SkyDome, ToonWater,
  ScreenDistortion, Telegraph, UI/SpeedLines, UI/RadialBurst.
- **Cámara**: tercera persona (colisión, lock-on, recentrado, FOV dinámico, zoom), primera persona (brazos y espada
  visibles), segunda persona experimental (vista del enemigo con correcciones y fallback), cámara cinemática,
  shake por trauma, impulsos, FOV punch, post-proceso dinámico (bloom, color, viñeta, DoF, motion blur, aberración).
- **Enemigos**: Nightspawn con FSM completa; EncounterDirector (máx. 2 atacantes); **The Hollow Oni** con fase 2 al
  50 % (transición, aura, velocidad, música, ataques nuevos, *Eclipse Cleave* telegrafiado); lock-on.
- **Mundo**: bosque nocturno original (luna, niebla, árboles, arroyo, puente, claros, linternas, puertas lunares,
  templo en ruinas), arena de entrenamiento, destructibles, checkpoints, santuarios interactivos.
- **UI**: HUD (vida con estela, stamina, BREATH, técnicas con cooldowns, combo, vida del jefe, lock-on, callouts de
  técnica, notificaciones), números de daño opcionales, menú principal, pausa, ajustes (audio, controles, cámara,
  gráficos, accesibilidad).
- **Audio**: AudioManager con Master/Music/SFX/Voice/Ambient; ~38 efectos sintetizados y 6 pistas de música
  procedurales (menú, bosque, combate, jefe fase 1/2, victoria) como placeholders sustituibles.
- **Sistemas**: GameManager persistente (no god object), eventos desacoplados, SaveSystem local, pooling, contador
  FPS, **menú debug F1** (God Mode, Breath/Stamina infinitas, spawn de enemigos y jefe, matar enemigos, reset de
  escena, cámara lenta, hitboxes visibles, FPS, modo de cámara, cambio de estilo).
- **Editor**: exportación automática del contenido a ScriptableObjects, menú *Breath of Eclipse* (escenas, Play From
  Boot, registrar escenas, validar proyecto).
- **Tests**: 39 tests de lógica (vida, stamina, BREATH, cooldowns, daño, combos, poise, buffs, tokens, input buffer),
  tests de SaveSystem y validación de contenido (EditMode).
- **Herramientas**: compile check offline contra ensamblados de Unity 6.3, shader check con DXC, generadores de
  input actions, escenas y metas.
- **Documentación** completa en `Documentation/`.

### Changed

- Nada (primera versión).

### Fixed

- Nada publicado previamente. Durante el desarrollo se corrigieron, entre otros: orden de triángulos del cielo y
  anillos, doble aplicación de alpha en mallas de VFX, fuentes de audio compartiendo posición, flash frames que no
  terminaban durante `timeScale = 0`, capturas de variable de bucle en lambdas, asignaciones por frame en el
  controlador del jugador.

### Known Issues

- **No verificado en el Editor de Unity**: el proyecto se generó en un entorno sin Editor. Está compilado contra los
  ensamblados reales de Unity 6.3 (0 errores/0 warnings) y los shaders contra la ShaderLibrary de URP 17.3 con DXC,
  pero la jugabilidad, el *game feel* y el rendimiento **no se han probado todavía**. Es posible que haya que ajustar
  valores (velocidades, distancias de cámara, intensidades de VFX, iluminación) en la primera sesión de juego.
- La validación offline de shaders no cubre la sintaxis ShaderLab (Properties, estados de render): Unity la valida al importar.
- Los personajes son maniquíes geométricos con animación procedural (placeholders deliberados); las aristas duras
  de las primitivas pueden mostrar pequeñas discontinuidades en el outline.
- El mundo se genera al pulsar Play: en modo edición las escenas aparecen vacías (solo el objeto constructor).
- La segunda persona es experimental: con muchos obstáculos puede volver a tercera persona con frecuencia.
- Sin navegación por NavMesh: los enemigos pueden quedarse atascados detrás de obstáculos grandes.
- El audio es sintetizado (placeholder).
