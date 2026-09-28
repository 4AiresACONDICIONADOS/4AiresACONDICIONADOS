# Controles

Todo se define en `Assets/_BreathOfEclipse/Core/Resources/BreathOfEclipse/Input/BreathOfEclipseControls.inputactions`
(Input System, generado por `Tools/Generators/generate_input_actions.py`). Si el asset faltara, `InputReader` usa una
copia embebida idéntica (`DefaultInputActions.cs`), así que el juego nunca se queda sin controles.

## Teclado y ratón

| Acción | Tecla | Notas |
|---|---|---|
| Moverse | **WASD** / flechas | Relativo a la cámara |
| Correr | **Shift** | Gasta stamina |
| Saltar | **Espacio** | |
| Esquivar | **Alt** (izq. o dcha.) | Ver "Modos de esquiva" |
| Ataque ligero | **Clic izquierdo** | |
| Ataque pesado | **Clic derecho** | |
| Bloquear / Parry | **Q** | Mantener = bloqueo; pulsar justo antes del golpe = parry |
| Fijar objetivo | **Rueda (clic)** o **Tab** | Rueda arriba/abajo cambia de objetivo |
| Técnicas 1-4 | **1 2 3 4** | 1-3 técnicas normales, 4 técnica avanzada |
| Ultimate | **R** | Requiere la barra de BREATH llena |
| Cambiar cámara | **C** | Tercera → Primera → Segunda persona |
| Interactuar | **E** | Santuarios, linternas (checkpoint) |
| Respiración siguiente / anterior | **X** / **Z** | Cambia de estilo de respiración |
| Menú de pausa | **Esc** | |
| Menú debug | **F1** | |
| Contador FPS | **F2** | |

## Mando (Xbox / PlayStation)

| Acción | Botón |
|---|---|
| Mover / Cámara | Stick izq. / Stick der. |
| Correr | L3 |
| Saltar | A / Cruz |
| Esquivar | B / Círculo |
| Ligero / Pesado | X / Cuadrado — Y / Triángulo |
| Bloquear / Parry | LB / L1 |
| Fijar objetivo | R3 |
| Técnicas 1-4 | Cruceta arriba / derecha / abajo / izquierda |
| Ultimate | RT / R2 |
| Interactuar | LT / L2 |
| Siguiente respiración | RB / R1 |
| Cámara | Select / Share |
| Pausa | Start / Options |

## Modos de esquiva (Settings → Controls)

| Modo | Comportamiento |
|---|---|
| Botón dedicado | Solo Alt / B esquiva. |
| **Dirección + Espacio con objetivo fijado** (por defecto) | Alt siempre esquiva; con lock-on, dirección + Espacio también esquiva (Espacio solo = salto). |
| Dirección + Espacio siempre | En el suelo, dirección + Espacio esquiva; Espacio sin dirección salta. |

## Reasignar teclas

Settings → **Controls**: pulsa sobre una acción y luego la nueva tecla (Esc cancela). Los cambios se guardan en el
archivo de ajustes local (`SaveSystem`, carpeta `Application.persistentDataPath`). **RESET BINDINGS** restaura las teclas por defecto y **RESET DEFAULTS** el resto de ajustes.
También se guardan sensibilidad de ratón/mando, invertir Y, FOV, intensidad de shake, calidad gráfica, volúmenes,
último estilo de respiración, mostrar números de daño, flash frames y "saltar cinemáticas de ultimate".
