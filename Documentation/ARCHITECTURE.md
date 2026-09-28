# Arquitectura

## Principios

- **Todo editable y sustituible**: el prototipo genera personajes, mundo, VFX y audio por código para no depender de
  assets de terceros, pero cada pieza está detrás de una interfaz o un ScriptableObject para reemplazarla por
  contenido real sin reescribir sistemas.
- **Datos fuera del código**: técnicas, ataques, combos, enemigos y ajustes son ScriptableObjects.
- **Sin god objects**: `GameManager` solo crea servicios independientes; cada uno tiene una responsabilidad.
- **Eventos desacoplados**: los productores lanzan `GameEvents.*`; HUD, audio, cámara o analítica escuchan.
- **Cualquier escena se juega con PLAY**: los servicios se crean con `RuntimeInitializeOnLoadMethod(BeforeSceneLoad)`.

## Ensamblados

| asmdef | Contenido | Referencias |
|---|---|---|
| `BreathOfEclipse.Logic` | C# puro, **sin UnityEngine** (`noEngineReferences`): HealthModel, StaminaModel, BreathGaugeModel, CooldownTracker, DamageCalculator, ComboGraph, PoiseModel, ComboCounter, BuffCollection, AttackTokenPool, InputBuffer | — |
| `BreathOfEclipse.Runtime` | El juego | Logic, Input System, URP, uGUI |
| `BreathOfEclipse.Editor` | Menús, exportador de contenido, validador | Runtime |
| `BreathOfEclipse.Tests.EditMode` | Tests NUnit | Logic, Runtime |

La capa Logic se testea también fuera de Unity (`Tools/CompileCheck/LogicTests`).

## Namespaces (`Scripts/Runtime`)

| Namespace | Carpeta | Responsabilidad |
|---|---|---|
| `BreathOfEclipse.Core` | Core, Input, Debug | GameManager, TimeController (hit stop / slow-mo), PoolManager, GameEvents, SaveSystem, GameSettings, SceneLoader, InputReader, FpsCounter, DebugMenu, Layers, Services |
| `BreathOfEclipse.Data` | Data | ScriptableObjects + `DefaultContent` (contenido por defecto) + `PhaseBuilder` |
| `BreathOfEclipse.Combat` | Combat | HitData/HitResult, Damageable, HitQuery, HitRegistry, CombatFeedback, HitStopManager, Projectile, HitZone, Destructible, Targeting, HitboxDebug |
| `BreathOfEclipse.Player` | Player | PlayerController (estados), PlayerMotor, PlayerCombat, PlayerDefense, PlayerStats, TargetLockSystem, PlayerFactory |
| `BreathOfEclipse.Breathing` | Breathing | BreathingStyleSystem, SkillExecutor (ejecuta las fases de SkillData), ISkillBehaviour |
| `BreathOfEclipse.AI` | AI | EnemyController + FSM, EnemyMotor, EncounterDirector, BossController, TelegraphMarker, EnemyFactory |
| `BreathOfEclipse.Characters` | Characters | CharacterRig (maniquí procedural), ProceduralAnimator, MotionLibrary, TwoBoneIK, ICharacterAnimator, MecanimCharacterAnimator, AnimationEventRelay |
| `BreathOfEclipse.CameraSystem` | CameraSystem | CameraRig + modos, CameraShaker, PostProcessController |
| `BreathOfEclipse.VFX` | VFX | VFXLibrary + recetas, piezas, SwordTrail, Afterimages, FlashFrameSystem, ScreenFX |
| `BreathOfEclipse.Rendering` | Rendering | ShaderIds, MaterialFactory, ProceduralMeshes, ProceduralTextures |
| `BreathOfEclipse.Audio` | Audio | AudioManager (Master/Music/SFX/Voice/Ambient), ProceduralAudio, MusicSynth |
| `BreathOfEclipse.UI` | UI | HUD, DamageNumbers, PauseMenu, SettingsPanel, UIFactory |
| `BreathOfEclipse.Environment` | Environment | EnvironmentKit, GameplaySceneBuilder, MoonlitForestBuilder, CombatTestBuilder, interactuables |
| `BreathOfEclipse.Scenes` | Scenes | BootLoader, MainMenuController |

## Arranque

```
[BeforeSceneLoad] GameManager.Bootstrap → "[BreathOfEclipse]" (DontDestroyOnLoad)
   ├─ SaveSystem.LoadSettings, Layers.ConfigureCollisionMatrix
   ├─ GameDatabase: Resources/BreathOfEclipse/GameDatabase  (o DefaultContent.Build() si no existe)
   └─ TimeController, PoolManager, InputReader, AudioManager, SceneLoader, FpsCounter, DebugMenu,
      ScreenFX, FlashFrameSystem, EventSystem
Escena → GameplaySceneBuilder.Awake
   ├─ World (construido o reutilizado si está "horneado") + atmósfera
   ├─ CameraRig, Player (PlayerFactory), HUD, DamageNumbers, PauseMenu
   └─ SpawnActors (enemigos, jefe, santuarios, checkpoints)
```

Los estáticos se reinician con `RuntimeInitializeOnLoadMethod(SubsystemRegistration)`, por lo que funciona con
*Enter Play Mode Options* (sin recarga de dominio).

## Eventos (`GameEvents`)

`Damage`, `Critical`, `EnemyKilled`, `PerfectDodge`, `Parry`, `BreathChanged`, `SkillUsed`, `UltimateActivated`,
`PlayerDied`, `PlayerRespawned`, `BossEncounter`, `BossPhaseChanged`, `BossDefeated`, `LockTargetChanged`,
`CameraModeChanged`, `StyleChanged`, `ComboChanged`, `Notification`.

## Guardado (`SaveSystem`)

JSON local (`Application.persistentDataPath/BreathOfEclipse/settings.json`, escritura atómica con archivo temporal).
Guarda ajustes, último estilo, reasignación de controles, sensibilidad, audio y calidad. Lectura tolerante a
archivos corruptos (vuelve a valores por defecto) y valores saneados. `ISaveStorage` permite tests en memoria.

## Rendimiento

Pooling para VFX, proyectiles, números de daño, telegrafías y debris; consultas físicas *NonAlloc*; materiales y
mallas cacheados; música sintetizada en un hilo aparte; densidad de partículas según calidad; FPS en F2.

## Sustituir placeholders

| Placeholder | Cómo sustituirlo |
|---|---|
| Maniquí procedural + animación por código | Importa un modelo humanoide con Animator Controller y usa `MecanimCharacterAnimator` (implementa `ICharacterAnimator`: mismas llamadas `PlayAttack`, `PlayMotion`, `PlayHit`, `SetDodge`...). Los ids de movimiento (`L1`, `SkillRisingCut`...) se mapean a estados del Animator. Hit frames y cancel windows desde Animation Events con `AnimationEventRelay`. |
| VFX procedurales | `VFXData` con el mismo id + prefab → `GameDatabase.vfxOverrides`. |
| Sonidos sintetizados | `AudioLibraryData` (en `GameDatabase.audioLibrary`): asigna AudioClips por id y categoría; los ids sin clip siguen usando el placeholder. |
| Mundo procedural | El builder reutiliza un hijo `World` si existe en la escena: puedes construir el nivel a mano y dejar `buildEnvironment` desactivado. |

## Decisiones técnicas (y por qué)

| Decisión | Motivo |
|---|---|
| Animación procedural (poses + IK de dos huesos) en vez de clips Mecanim | Sin assets de animación con licencia libre; da lectura clara de anticipación/ataque/impacto/recuperación y se reemplaza vía `ICharacterAnimator`. IK de pies, manos, mirada y espada. |
| HLSL a mano en vez de Shader Graph | Outline de casco invertido (pase extra) y flash frames globales. |
| Un solo asmdef de runtime con namespaces | Menos fricción para iterar; la capa pura (Logic) sí está separada y testeada. |
| `CameraSystem` en vez de `Camera` como namespace | Evita choques con `UnityEngine.Camera`. |
| Sin NavMesh | Arenas abiertas: steering + evitación bastan y no hay que hornear navegación. |
| Volumen de audio por categoría con multiplicadores (sin AudioMixer) | Evita un asset binario; mismo resultado para Master/Music/SFX/Voice/Ambient. |
| Volumétricos "fake" | Haces aditivos + bruma: baratos y con estética anime; el volumetric fog real de URP no es estándar en Forward+. |
| Radial blur aproximado | Motion blur + speed lines + aberración cromática, sin render feature propio. |
