# TODO

## Próxima sesión (para llegar a v0.2.0 — "jugable verificado")

- [ ] Abrir en Unity 6.3 LTS, confirmar 0 errores de compilación e importación de shaders.
- [ ] Ejecutar Test Runner (EditMode) y corregir cualquier fallo de `ContentValidationTests` / `SaveSystemTests`.
- [ ] Jugar `03_CombatTest`: ajustar velocidades, distancias de cámara, hit stop, intensidades de VFX y bloom.
- [ ] Jugar `02_MoonlitForest` de principio a fin (incluido The Hollow Oni) y ajustar iluminación/niebla/densidad.
- [ ] Perfilar a 1080p y confirmar 60 FPS en la calidad High; ajustar densidad de partículas y sombras si hace falta.
- [ ] Registrar resultados en `DEV_STATUS.md` y, si todo es jugable, publicar **v0.2.0** en el CHANGELOG.

## Contenido

- [ ] Modelo humanoide real + Animator Controller (usar `MecanimCharacterAnimator`; ids de movimiento como estados).
- [ ] Animaciones con Animation Events para hit frames y cancel windows (`AnimationEventRelay`).
- [ ] Sustituir VFX clave por prefabs (Particle System / VFX Graph) mediante `VFXData` si se quiere más detalle.
- [ ] Música y efectos reales en `AudioLibraryData`; voces (categoría Voice) para callouts de técnicas.
- [ ] Más enemigos (arquetipos a distancia / pesados) y un segundo jefe.

## Sistemas

- [ ] Herramienta de editor para "hornear" el mundo procedural en la escena (edición visual del nivel).
- [ ] NavMesh opcional para enemigos en niveles con obstáculos complejos.
- [ ] Progresión (desbloqueo de técnicas, mejoras de arma) y guardado de partida (el SaveSystem actual solo guarda ajustes).
- [ ] PlayMode tests de combate (combo completo contra el muñeco, parry, esquiva perfecta).
- [ ] Localización (textos de UI en tabla).
- [ ] Mejorar outline en aristas duras (normales suavizadas precalculadas en un canal UV).

## Deuda técnica conocida

- [ ] Varios MonoBehaviours pequeños comparten archivo (p. ej. `VfxParts.cs`, `Interactables.cs`, `UIFactory.cs`):
      separarlos en archivos propios facilita reutilizarlos como componentes en el Inspector.
- [ ] `EnemyMotor`: revisar asignaciones por frame en la evitación de obstáculos.
