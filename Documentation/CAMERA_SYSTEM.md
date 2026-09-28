# Sistema de cámara

`CameraSystem/CameraRig.cs` es la única cámara del juego. Mantiene un `ICameraMode` activo, mezcla suavemente al
cambiar de modo (0.35 s) y aplica encima los efectos compartidos: shake, impulsos, FOV punch y zoom.
Namespace `BreathOfEclipse.CameraSystem` (no `.Camera`, para no chocar con `UnityEngine.Camera`).

**C** / Select cambia de modo: Tercera → Primera → Segunda persona. El último modo se guarda en los ajustes.

## Tercera persona (principal) — `ThirdPersonCameraMode`

- Órbita con ratón/stick (pitch -35°…70°), distancia 5.2 m, pivote a 1.55 m, ligero offset de hombro.
- **Colisión**: *sphere cast* (radio 0.22) desde el pivote; se acerca al instante si hay obstáculo y se aleja suavemente.
- **Lock-on**: encuadra a los dos luchadores apuntando entre jugador y objetivo; se abre más con jefes.
- **Recentrado** automático suave detrás de la dirección de movimiento (desactivable en Settings) y manual.
- **FOV dinámico**: +7° al correr, FOV punch en dashes y flash steps; FOV base configurable (50-90).
- **Distancia dinámica**: zoom out para ataques gigantes (jefe, ultimates), zoom in para parry, finisher, crítico.

## Primera persona — `FirstPersonCameraMode`

- Ojos del personaje con un ancla estabilizada (no el hueso de la cabeza directamente), así los cortes rápidos no
  marean. Se ocultan cabeza/torso del modelo; **brazos, katana y VFX de técnicas siguen visibles**.
- Roll cinemático mínimo al atacar (máximo 2.5°).

## Segunda persona (experimental) — `SecondPersonCameraMode`

- Punto de vista del **enemigo fijado** mirando al protagonista. El jugador sigue controlando al protagonista.
- Correcciones: paredes detrás del rival, nunca bajo el suelo, obstáculos entre rival y jugador.
- **Fallback**: sin objetivo fijado (o si el objetivo muere) vuelve automáticamente a tercera persona y lo avisa en pantalla.

## Cámara cinemática de combate — `CinematicCombatCamera`

Usada por las técnicas y ultimates **en gameplay real** (no son vídeos). Planos disponibles (`CinematicShot`):
LowAngleHero, OrbitSlow, WideArena, CloseUpFace, OverShoulderTarget, TopDown, FollowBehind, SideProfile, SkyLookUp.
Cada fase de una técnica puede pedir un plano (`PhaseBuilder.Shot`). Si *Skip ultimate cinematics* está activo, el
plano se ignora y la técnica se ejecuta con la cámara de juego. Seguro anti-bloqueo: si una cinemática no se
cierra, el rig vuelve solo al modo normal 1.5 s después de su final previsto.

## Efectos (`CameraFX`)

| Llamada | Uso |
|---|---|
| `CameraFX.Shake(trauma)` | Sacudida por *trauma* (se suma y decae; escala con el ajuste "Camera shake") |
| `CameraFX.Impulse(dir, fuerza)` | Empujón direccional con muelle (dirección del golpe) |
| `CameraFX.FovPunch(grados)` | Golpe de FOV (dashes, flash step) |
| `CameraFX.Zoom(mult, duración, in, out)` | Acercar/alejar temporalmente (parry, finisher, jefe) |
| `CameraFX.Post` | `PostProcessController`: pulsos de bloom, aberración cromática, distorsión de lente, viñeta, exposición, desaturación mantenida, motion blur, DoF cinemático |

Todo funciona con tiempo *unscaled*: durante hit stop y cámara lenta la cámara sigue fluida.

## Post-proceso (`PostProcessController`)

Volumen global creado en runtime: Tonemapping ACES, **Bloom** (HDR), **Color Adjustments**, White Balance,
**Vignette** moderada, **Depth of Field** (solo en cinemáticas), **Motion Blur** controlado (desactivable), Chromatic
Aberration y Lens Distortion por pulsos. SSAO lo hace el renderer de URP (`PC_Renderer`).
Niebla exponencial de la escena + haces de luz y bruma falsos (volumétricos "fake", ver VFX_SYSTEM).
