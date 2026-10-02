# Changelog

Formato: Version / Added / Changed / Fixed / Known Issues. Versionado semántico; **v0.2.0 queda reservada para la
primera versión verificada como jugable dentro del Editor de Unity.**

---

## v0.5.0 — Living World (2026-10-02)

Sin verificar todavía en Unity (NOT PLAYTESTED). Escena nueva `04_FrontierRegion`; `03_CombatTest` y
`02_MoonlitForest` se conservan sin cambios de comportamiento.

### Added
- **Asagiri Frontier** (480 × 480 m, 12 sectores): aldea segura (casas, posada, tienda, herrería, puesto de
  cazadores, campanario, pozo, santuario, huertos, arrozales), zona salvaje (bosque, río con puente, vado y
  cascada, campamento, aserradero, estanque), exploración (ruinas, cueva, mirador, santuario ribereño, un lugar
  secreto sin marcador) y la Hondonada de Ceniza (zona de alto riesgo sin muros invisibles: el aire avisa).
- **Streaming por sectores**: terreno de detalle con colisión cocinada fuera del hilo principal, vistas lejanas
  (terreno 10 m + siluetas de árboles), carga de uno en uno con histéresis, mallas liberadas al descargar; raíz
  persistente (`LivingWorld`) que nunca se duplica.
- **Día y noche** (24 h, Dawn…LateNight): sol / luna / rim, cielo con disco solar, estrellas, nubes de día,
  ambiente, niebla, gradación, faroles y ventanas; niebla matinal ligera.
- **Audio por zonas** (aldea, campos, bosque, río, cascada, tierra maldita, cueva) mezclado por posición y hora,
  con sonidos puntuales (herrero, leñadores, aves, cuervos, búhos, ranas, gruñidos lejanos, campanas).
- **Vida NPC**: 24 personas con rutinas data-driven sobre un grafo de navegación validado (nunca agua, edificios
  ni acantilados), cuerpo anime ligero (cuerpos base CC0 masculino y femenino, 8 atuendos, peinados, cara que
  parpadea y habla, herramientas), animaciones reales con LOD por un planificador central, simulación fuera de
  pantalla, puertas que se abren, tienda con horario, forja, fogata y ropa tendida que siguen a sus dueños,
  diálogos y comentarios en español, ayudar heridos, descanso (hasta la mañana / la tarde) con salto de tiempo.
- **Demonios del mundo**: territorio, patrulla nocturna, acecho con señales previas, sin aparecer a la vista ni
  cerca (≥ 38 m), correa / pérdida de interés, la aldea como refugio, huida al sol, huida herida con memoria
  persistente (escapó, cicatriz, encuentros, respiraciones observadas) y regreso más agresivo; pooling.
- **Cazadores NPC** (Kaede, Rokuro, Sora): detectan y combaten demonios con clips de espada reales, esquivan o
  bloquean, defienden aldeanos, se retiran heridos, caen heridos; Sora muestra una técnica lunar.
- **Eventos dinámicos** (8 + presencia excepcional): caravana atacada, familia perseguida, cazador herido,
  cazador contra demonio, ataque nocturno a la aldea (campana, refugio, guardias y cazadores), demonio raro,
  descubrimiento sin marcador, niño perdido; plazos suave / duro (los eventos no esperan), consecuencias
  persistentes (hechos, carro destrozado y cerca rota con etapas de reparación), resolución fuera de pantalla.
- **Memoria del mundo** (`WorldStateDatabase`, guardado versionado aparte), autoguardado al descansar, tras eventos
  del jugador, al volver a zona segura y al salir; ganchos para v0.6 (hechos como `Saved_Caravan_001`).
- **Mapa** (M) con niebla sobre lo no descubierto; **F1 WORLD LAB**; menú principal **PLAY LIVING WORLD**
  (CONTINUE / NEW cuando hay partida); playtest **FULL WORLD TEST**; tests de lógica (100).

### Changed
- `EnemyController`: cerebro de mundo opcional, objetivo alternativo y estado `World` (las arenas no cambian).
- Generador: escribe sólo las escenas que faltan; capa `Npc` (12).

### Known Issues
- Nada verificado en Unity: rendimiento NOT MEASURED; tiempos de carga, aspecto de los atuendos de los aldeanos,
  sincronía de clips en NPCs y combate de cazadores pendientes de prueba real.

---

## v0.4.0 — Full 3D Anime Evolution (2026-09-29)

Sin verificar todavía en Unity (NOT PLAYTESTED).

### Added
- Personaje 3D real: cuerpo base Quaternius Universal Base Characters (CC0) vestido como espadachín anime original
  (pelo anime procedural con coleta con muelles, parte superior oscura, haori corto abierto con falda y ribete del
  color del estilo, obi del color del estilo, hakama plisado, vendas, botas tabi; cara separada; ojos anime con
  expresiones y parpadeo; boca que se abre con la voz).
- Animaciones reales: Universal Animation Library 1 + 2 (CC0) por Humanoid con Playables: idle / idle de combate,
  caminar-trotar-correr sincronizados a la velocidad, strafe por orientation warping, retroceso, salto / caída /
  aterrizaje, golpes, derribo + levantarse, muerte y un clip de cuerpo por ataque sincronizado con la fase activa.
- Híbrido: el `ProceduralAnimator` sigue siendo la única autoridad; sus poses se retargetean con IK (manos en la
  katana con marco de agarre desde los dedos, torso en ataques, cuerpo completo en técnicas, look-at con límites,
  pies al suelo, puño, hombros al inhalar). Las 35 formas usan el modelo nuevo; la hoja real es la del hitbox.
- Nightspawn y Hollow Oni 3D sobre el mismo cuerpo (piel demoníaca con marcas brillantes, garras, colmillos,
  cuernos, espinas, hakama rasgado; el Oni con volumen, máscara de hueso y melena); fase 2 intensifica marcas y ojos.
- Shader `AnimeCharacterToon` (dos bandas de sombra o rampa, rim, especular estilizado, cara aplanada, marcas,
  hit flash, disolución, contorno para skinned meshes).
- F1: PLAYER / DEMON VISUAL (3D o procedural), pruebas de animación y de demonios, ventana **Animation Lab**.
- Editor: *Characters → Import Player Model* (VRoid/UniVRM o FBX Humanoid en `Characters/Import/Player/`),
  *Validate Character Models*, *Use Built-in Anime Swordsman*. Playtest `ModelSuite` y tests `CharacterModelTests`.

### Changed
- `CharacterRig` integra renderers importados (hit flash, disolución, visibilidad, primera persona, afterimages).
- Efectos de inhalación en `MouthBreathSocket`; primera persona usa la cabeza real y oculta cabeza/pelo/cara.
- Ajustes nuevos `playerVisualMode` / `demonVisualMode` (0 = 3D); partidas de v0.3.0 siguen siendo válidas.

### Known Issues
- No probado en Unity: posibles ajustes de agarre, escala del pelo o poses de técnicas concretas en el modelo real.
- Si el FBX no se importa como Humanoid, se usa el retarget procedural (sin clips); si el modelo falla, el maniquí.

## v0.3.0 — Forms & Character Evolution (2026-09-29)

Sin verificar todavía en Unity (NOT PLAYTESTED).

### Added
- 12 formas nuevas: Trueno IV Colmillo del Relámpago, VI Hilo Fulminante, VII Horizonte Quebrado; Brasas IV Martillo
  de Magma, V Muralla de Brasas, VII Corazón del Volcán; Vendaval IV Cuchillas del Vendaval, VI Picado del Remolino,
  VII Ojo de la Tormenta; Lunar IV Luna Gemela, V Órbita Plateada, VI Filo del Novilunio. Cada estilo tiene 7 formas.
- Slots rápidos por estilo guardados (F + apuntar + 1-4); franja del HUD con todas las formas; título "FORM VII".
- `BreathingInhaleSystem`: pose `SkillInhale`, corrientes por elemento, sonido de inhalación, foco en hoja y cuerpo.
- `VoiceDirection` (emoción, intensidad estilo → postura → nombre, ritmo) por estilo y técnica; clips por estilo.
- Generador de voz v2: Chatterbox Multilingual (MIT) + control con faster-whisper; proveedor ElevenLabs preparado.
- Katana: hamon, hi, habaki, tsuba de hierro con borde dorado, tsuka con ito cruzado, saya con `SheathSocket`.
- Menú *Characters*: importación Humanoid automática, Animator Controller por nombres de clip, perfil asignado.
  IK de mano izquierda en el mango (Mecanim). Estados que faltan usan "Attack" / "Skill" sin errores.
- Nightspawn: *Rending Cross* (pesado) y animación de tambaleo; Hollow Oni: transformación de fase 2.
- `HitShape.AlongLastPath` / `VFXAnchor.LastPath` (cortes retardados a lo largo del recorrido).

### Changed
- Toda forma gasta BREATH (I 8 … VII 15); la respiración en reposo recupera hasta 40 %; la ultimate se gana en combate.
  Coste y cooldown se pagan al terminar la inhalación.
- Slots por defecto: Agua I / IV / VI / VII. La voz empieza tras la inhalación.

## v0.2.1 — Anime Visual Overhaul (2026-09-29)

Sin verificar todavía en Unity (NOT PLAYTESTED).

### Added
- Shader `TidalWaterAnime` (bandas, espuma de borde y cresta, disolución, fade por profundidad y cerca de la cámara).
- `WaterRibbonRenderer`, `WaterSerpentRenderer` (cuerpo, cresta, cabeza con mandíbula, cuernos y ojos), `AnimeFoam`.
- VFX propio por forma del Agua (I–VII); nuevos `water_tide_cut`, `water_cascade`, `water_ring_current`.
- Flash Breaker (Trueno I) hero: postura, carga con estática por el suelo y arcos por el cuerpo, trayectoria cegadora
  con descargas paralelas y estelas, pausa, línea iai que se parte en relámpagos, clic de la vaina.
- `CharacterVisualProfile` + `HumanoidCharacterVisual`: modelo humanoide opcional con sockets
  `RightHandWeaponSocket` / `BladeBase` / `BladeTip`.
- Bosque: árboles de copa ancha, 650 matas de sotobosque, niebla baja, static batching; split toning lunar.

### Changed
- Rising Serpent y la ultimate del Agua (Summon 1.2 / Leviathan 1.8 / Requiem 1.0 / Stillness 0.6 s) con serpiente real.
- Voz anclada a fases (`VoiceCue`); si no cabe la frase completa, se prefiere «Respiración…» + nombre al cambiar de estilo.
- Combat feel: hit stop 0.045 / 0.07 / 0.1 s, técnicas cancelables antes, cadera-torso-hombros en cada golpe.
- Cámara: sonda de hombro contra paredes, elevación al acercarse por colisión, encuadre hacia el golpe, FOV suave;
  primera persona sin balanceo oscilante; segunda persona estable con luchadores superpuestos.
- Protagonista (ojos, pelo, vaina, mangas, hakama), Nightspawn (costillas, espinas, garras) y Oni (cuernos, grietas).

## v0.2.0 — Tidal Breath Overhaul (2026-09-29)

Sin verificar todavía en Unity.

### Added
- Formas variables por estilo (`BreathingStyleData.forms`, hasta XI), 4 formas rápidas (1-4) y rueda de formas (mantener F).
- Respiración del Agua: 7 formas — I Tide Cutter, II Crescent Tide, III Wandering Current, IV Parting Cascade,
  V Ring of Tides, VI Abyss Fang, VII Rising Serpent (IDs anteriores conservados).
- `BreathingVoiceSystem`: estilo → postura → técnica en español, subtítulos, título pequeño, ducking; opciones en
  Settings. 45 clips generados con Piper (`es_MX-ald-medium`), ver `EXTERNAL_ASSETS.md`.
- Voces 3D de Nightspawn y Hollow Oni (`EnemyVoice`).

### Changed
- Rising Serpent: anticipación 0.34 / carga 0.18 / dash 0.17 / corte 0.16; la serpiente se ve 0.55 s a tamaño completo.
- Fase 2 del Oni: ojos, marcas, aura y noche carmesí desde el rugido.
- VFX, cintas de agua y trails se desvanecen cerca de la cámara.
- Contenido v2: los assets de datos exportados con v1 se re-exportan en el Editor.

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
