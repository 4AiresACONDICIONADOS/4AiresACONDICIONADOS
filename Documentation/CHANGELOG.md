# Changelog

Formato: Version / Added / Changed / Fixed / Known Issues. Versionado semántico; **v0.2.0 queda reservada para la
primera versión verificada como jugable dentro del Editor de Unity.**

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
