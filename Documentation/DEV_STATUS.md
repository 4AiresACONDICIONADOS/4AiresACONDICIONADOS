# DEV STATUS — Breath of Eclipse

**Versión actual: v0.1.0** · Fecha: 2026-09-28 · Editor objetivo: Unity 6000.3.25f1 (6.3 LTS) · URP 17

## Cómo se verificó esta versión

| Verificación | Resultado |
|---|---|
| Compilación runtime (player build, sin `UNITY_EDITOR`) contra `UnityEngine.*.dll` de Unity 6.3 + Input System + uGUI + URP | ✅ 0 errores, 0 warnings |
| Compilación editor + tests (con `UNITY_EDITOR`, `UnityEditor.*.dll`, NUnit de Unity) | ✅ 0 errores, 0 warnings |
| Tests de la capa Logic (`dotnet test`) | ✅ 39/39 |
| Shaders: 12 shaders × pases × variantes (incluye instancing, Forward+, sombras, SSAO, niebla) compilados con DXC contra la ShaderLibrary de URP 17.3 | ✅ 62 compilaciones, 0 fallos |
| Coherencia de contenido (ids de animación / VFX / sonido referenciados) | ✅ revisión estática; test EditMode incluido |
| **Abrir y jugar en el Editor de Unity** | ⚠️ **Pendiente** (no había Editor disponible) |

Por eso la versión se mantiene en **v0.1.0**; la v0.2.0 se publicará cuando se confirme jugable en el Editor.

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
| — | Prueba de juego, ajuste fino y rendimiento en el Editor | ⏳ Siguiente paso (ver TODO.md) |

## Riesgos principales para la primera apertura

1. **Ajuste fino**: valores de movimiento, cámara y VFX están razonados pero no probados a mano.
2. **Iluminación nocturna**: intensidad de luna/ambiente/bloom puede necesitar retoque según el monitor.
3. **Shaders**: validados en HLSL; ShaderLab (bloques Properties/estados) se valida al importar.

## Dónde mirar si algo falla

- *Breath of Eclipse → Setup → Validate Project* (URP activo, shaders, escenas, input, base de datos).
- Consola: los sistemas registran avisos con prefijo `[Breath of Eclipse]`, `[MaterialFactory]`, `[GameManager]`...
- F1 → *Show Hitboxes* para depurar golpes; F2 para FPS.
