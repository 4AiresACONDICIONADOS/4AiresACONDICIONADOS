# Sistema de combate

Todo el combate es **data-driven**: los valores viven en ScriptableObjects (`PlayerData`, `WeaponData`,
`AttackData`, `ComboData`, `SkillData`, `BreathingStyleData`, `EnemyData`). El contenido por defecto se define en
`Scripts/Runtime/Data/DefaultContent.cs` y se exporta a assets editables en la primera apertura del proyecto.

## Tubería de daño

```
Ataque (PlayerCombat / SkillExecutor / EnemyController / Projectile / HitZone)
  └─ HitQuery (sweep de la hoja, esfera, cápsula, cono)  →  HitRegistry (AttackID: 1 golpe por objetivo y swing)
       └─ HitData  →  Damageable.ReceiveHit
            ├─ IHitInterceptor (PlayerDefense: bloqueo, parry, esquiva, esquiva perfecta)
            ├─ DamageCalculator (Logic, testeado): base × multiplicador × buffs × crítico × defensa
            ├─ HealthModel / PoiseModel
            └─ IHitReactor (reacción, stagger, knockback, launch)  →  HitResult
                 └─ CombatFeedback (hit stop, shake, FOV punch, zoom, chispas, números) + GameEvents
```

- **Detección**: la katana hace *sweeps* de esferas entre la posición anterior y la actual de la hoja
  (`HitQuery.BladeSweep`), así un corte rápido no atraviesa objetivos entre frames.
- **AttackID**: cada ejecución de un ataque abre un `HitRegistry` nuevo; un mismo objetivo no recibe dos golpes del
  mismo swing (las técnicas multi-hit declaran `hits` + intervalo).
- **Hitboxes visibles**: F1 → *Show Hitboxes* (rojo = consultas, amarillo = barrido de hoja, verde = hurtboxes).

## Combos

| Entrada | Cadena | Final |
|---|---|---|
| L L L L | L1 Crescent Cut → L2 Returning Cut → L3 Rising Cut → **L4 Eclipse Whirl** | Finisher, knockback |
| L L H | L1 → L2 → **L2H Rising Launcher** | Lanza al enemigo al aire (continuar con combo aéreo) |
| L H H | L1 → **L1H Cross Cut** → **L1HH Crescent Slam** | Finisher, impacto en el suelo |
| H H | **H1 Heaven Cleave** → **H2 Heaven Splitter** | Finisher, derribo |
| Dash + L | **DashL Swift Lunge** | Estocada en carrera |
| Salto + L | A1 → A2 → A3 (Sky Cut...) | Combo aéreo, mantiene al enemigo suspendido |
| Salto + H | **AH Falling Moon** | Caída en picado, derribo |
| Esquiva perfecta + L | **PDCounter Phantom Counter** | Contraataque crítico garantizado |
| Parry + H | **Riposte Break** | Contraataque crítico, derribo |

El grafo (`ComboGraph`, capa Logic) resuelve la siguiente acción según el nodo actual, el botón y el contexto
(en el aire, dash, ventana de esquiva perfecta, ventana de parry). Se testea en `CombatLogicTests`.

### Cancel windows

Cada `AttackData` tiene `cancelWindows` (inicio/fin normalizados + flags `Attack | Dodge | Skill | Block | Ultimate |
Jump | Move`). Por defecto: esquiva/bloqueo cancelan pronto, el siguiente ataque del combo se abre desde
`comboStart`, y el movimiento libre al final de la recuperación. Con modelos reales, `AnimationEventRelay` permite
abrir/cerrar ventanas desde **Animation Events** (`OpenCancelWindow`, `CloseCancelWindow`, `HitStart`, `HitEnd`, `Custom`).
Las pulsaciones se guardan en un **input buffer** (`InputBuffer`, 0.3 s) para que los combos no se pierdan.

## Hit stop, slow motion y sensación de impacto

| Tipo | Hit stop |
|---|---|
| Ligero | 0.025 s |
| Pesado | 0.045 s |
| Técnica | 0.06 s |
| Crítico / Finisher | 0.08 s |
| Ultimate | 0.10 s |

`TimeController` aplica hit stop y cámara lenta sobre tiempo *unscaled* (la UI nunca se congela; los menús usan
tiempo real). Cada impacto combina: hit stop, shake por trauma, impulso direccional, FOV punch, zoom, chispas del
elemento, flash del personaje, decal/debris si toca el suelo, y reacción del enemigo (light, heavy, knockback, launch,
knockdown, stun). Los **flash frames** (1-3 frames) se reservan para críticos, finishers, parries, ultimates y técnicas especiales.

## Defensa

| Mecánica | Valores por defecto (`PlayerData`) |
|---|---|
| Esquiva | 0.32 s, invulnerable 0.26 s, 20 stamina |
| **Esquiva perfecta** | golpe en los primeros 0.18 s → cámara lenta **×0.25 durante 0.4 s**, ventana de contraataque 1.1 s, gana BREATH |
| Bloqueo | reduce 80 % del daño, 14 stamina por golpe, movimiento lento |
| **Parry** | golpe en los 0.17 s tras pulsar Q → chispas, shake, stagger del enemigo, micro slow-mo ×0.2 (0.28 s), ventana de contraataque |

## Recursos

- **Stamina**: correr, esquivar, bloquear, técnicas. Se regenera tras un retardo; al agotarse hay que recuperar un 30 % (`StaminaModel`).
- **BREATH** (barra de respiración): sube golpeando, con hitos de combo, esquivas perfectas, parries, recibiendo daño
  y matando. Se gasta en técnicas avanzadas (35) y ultimates (100). `BreathGaugeModel`.

## Estilos de respiración (`BreathingStyleSystem`)

5 estilos originales, cada uno con 3 técnicas normales (teclas 1-3), 1 avanzada (4) y 1 ultimate (R). X / Z cambian de estilo.

| Estilo | Elemento | 1ª | 2ª | 3ª | Avanzada | Ultimate |
|---|---|---|---|---|---|---|
| **TIDAL BREATH** | Agua | Rising Serpent | Crescent Tide | Flowing Current | Whirlpool Fang | Leviathan's Requiem |
| **THUNDER BREATH** | Trueno | Flash Breaker | Chain Spark | Rolling Thunder | Heavenly Drum | Thousand Flashes |
| **EMBER BREATH** | Fuego | Blazing Arc | Cinder Wheel | Kindling Burst | Phoenix Ascent | Solar Cataclysm |
| **GALE BREATH** | Viento | Sky Rend | Cyclone Dance | Tailwind Step | Tempest Pillar | Heaven's Gale |
| **MOONLIGHT BREATH** | Luna | Crescent Veil | Waning Echo | Lunar Halo | Eclipse Tide | Eternal Eclipse |

### Técnicas de referencia

**TIDAL BREATH — RISING SERPENT**: *Low Stance* (0.3 s, agua en la hoja, ondas en el suelo, zoom y ligera cámara
lenta) → *Serpent Dash* (dash al objetivo con afterimages, speed lines, FOV punch) → *Rising Cut* (corte diagonal
ascendente, serpiente de agua, **hit stop 0.08 s**, zoom, launch) → *Ascend* (el jugador sube con el enemigo, gotas
suspendidas) → *Poise* (cancelable: continúa con el combo aéreo).

**THUNDER BREATH — FLASH BREAKER**: *Sheathe* (**0.35 s** de carga, desaturación -55, zoom) → *Flash* (teletransporte
detrás del enemigo con línea de luz y afterimages) → *Dramatic Pause* (**0.12 s** a ×0.35) → *The Cut* (flash,
explosión eléctrica, crítico garantizado, stun, shake) → *Resheathe*.

### Ultimates

Duran ~5-8 s, usan `CinematicCombatCamera` en gameplay real (planos: low angle heroico, órbita, detrás del hombro,
vista cenital, perfil...), el jugador es invulnerable y se bloquea la entrada. Se pueden **saltar** desde Settings
(*Skip ultimate cinematics*): entonces se ejecutan con la cámara normal.

### Crear una técnica nueva (sin tocar código)

1. Duplica un `Skill_*.asset` en `Assets/_BreathOfEclipse/Data/BreathingStyles/<estilo>/`.
2. Edita `skillId`, nombre, costes, cooldown y la lista de **phases**. Cada fase define: duración, `motionId`
   (animación), movimiento (dash, teleport, rise, hover, plunge...), invulnerabilidad, trails, afterimages,
   **VFX cues** (id + anclaje + retardo), **SFX cues**, **hits** (forma, radio, daño, reacción, hit stop, shake,
   crítico, estados), proyectiles, buffs y **camera cues** (shake, zoom, FOV, slow-mo, saturación, bloom, plano cinemático).
3. Asigna el asset a `techniques`, `advanced` o `ultimate` del `Style_*.asset`.
4. Si necesita lógica especial, implementa `ISkillBehaviour` y regístrala por nombre (`customBehaviour`), como `blink_chain`.

## Enemigos

**Nightspawn** — FSM (`EnemyStates.cs`): Idle, Patrol, Alert, Chase, Reposition, Attack, Block, Dodge, Stagger,
Knockdown, Airborne, Dead (+ Special). Ataques: Rending Claws, Shadow Lunge, Night Pounce. Los ataques grandes se
telegrafían con un destello y/o un círculo en el suelo que se llena hasta el impacto.

**EncounterDirector**: como mucho **2 enemigos atacan a la vez** (tokens de ataque); el resto rodea y amenaza,
para que las peleas contra grupos sean legibles.

**THE HOLLOW ONI** (mini jefe) — Kanabo Smash, Crushing Sweep, Quake Stomp, Demon Charge.
Al **50 % de vida** entra en **fase 2**: rugido con transición (invulnerable, shockwave que empuja al jugador,
desaturación, aberración cromática), aura oscura, más velocidad y agresividad, música `boss_phase2`, ataques nuevos
(Twin Sweep, versiones rápidas) y el ataque espectacular telegrafiado **Eclipse Cleave** (salto con gran círculo de
aviso) además de **Hellfire Wheels** (proyectiles). Barra de vida de jefe en el HUD.

## Lock-on

Rueda/Tab fija el objetivo más cercano a la dirección de la cámara; rueda arriba/abajo cambia. El personaje encara al
objetivo, la cámara lo encuadra (zoom out automático con jefes), la cabeza del jugador lo mira (IK de mirada) y el
HUD muestra el marcador. Se suelta al morir o alejarse.
