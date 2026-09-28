# Sistema de VFX

Todos los efectos son **reales y procedurales** (no cubos de marcador): sistemas de partículas, mallas animadas,
tubos de elemento, rayos, luces, decals y shaders propios, generados por código y reciclados con pooling.

## VFXLibrary

```csharp
VFXLibrary.Spawn("water_serpent", position, rotation, scale, Element.Water, follow: null, lifetime: 0f);
VFXLibrary.SpawnBetween("thunder_path", from, to, Element.Thunder);   // efectos estirados entre dos puntos
```

- Cada id es una **receta** (`CommonVFX`, `WaterVFX`, `FireVFX`, `ThunderVFX`, `WindVFX`, `MoonVFX`) que construye un
  prefab en memoria con `VfxBuild` la primera vez; después se reutiliza desde `PoolManager` (clave `vfx:id:elemento`).
- `00_Boot` construye todas las recetas una vez (precalentado) para evitar tirones en el primer uso.
- **Sustituir por un prefab real**: crea un `VFXData` (Create → Breath of Eclipse → VFX Override) con el mismo `vfxId`
  y un prefab (Particle System / VFX Graph), y añádelo a `GameDatabase.vfxOverrides`. El código no cambia.
- **Calidad**: `VFXQuality.Density` escala el número de partículas (Low 0.45, Medium 0.75, High 1, Ultra 1.25).

### Piezas (`VfxParts.cs` y otros)

| Pieza | Uso |
|---|---|
| `ParticleBuilder` | API fluida sobre ParticleSystem: ráfagas, vida, velocidad, tamaño, color y tamaño en el tiempo, gravedad, ruido, arrastre, estiramiento, billboards horizontales |
| `RibbonTube` | Tubo que recorre un camino paramétrico con cabeza/cola: serpiente y dragón de agua, arcos de fuego, espirales de viento |
| `ExpandingMesh` | Mallas que escalan/giran/se desvanecen: ondas de choque, medias lunas, domos, halos |
| `LightningBolt` | Rayos quebrados con ramas, re-aleatorizados varias veces por segundo |
| `FlashLight` | Luz puntual con curva de intensidad (impactos iluminan el entorno) |
| `GroundDecal` | Grietas, quemaduras y salpicaduras en el suelo que se desvanecen |
| `AfterimageSystem` | Imágenes residuales del personaje (dash, flash step, esquiva perfecta) con `Graphics.RenderMesh` |
| `SwordTrail` | Trail de la katana (Catmull-Rom) + trail de elemento combinable |

Colores por elemento en `ElementPalette` (Core, Edge, Bright, Dark, Accent) para agua, fuego, trueno, viento, luna y oscuridad.

## Trails de espada (`SwordTrail` + shader `SwordTrail`)

Muestras base/punta de la hoja interpoladas con Catmull-Rom; malla con UV.x = edad y UV.y = base→punta.
Shader con **gradiente** por edad, **ruido** desplazándose (texture movement), **dissolve** al envejecer, **emisión**
HDR (brilla con bloom) y **curva de ancho** (`WidthOverAge`). Cada estilo aporta su gradiente de elemento
(`BreathingStyleData.elementTrailGradient`); el trail de elemento se combina con el de la espada en técnicas y finishers.

## Flash frames anime (`FlashFrameSystem`)

1-3 frames (por conteo de frames, funciona durante hit stop), con enfriamiento para que nunca se encadenen.
Solo en **críticos, finishers, parries, ultimates y técnicas especiales**; desactivables en Settings.

- **DarkBackground**: el mundo se oscurece al color del elemento, los personajes quedan iluminados con borde brillante.
- **Silhouette**: siluetas negras sobre fondo del color del elemento.

Lo aplican los propios shaders (ToonLit, SkyDome, ToonWater) leyendo variables globales, más una capa UI con
líneas de enfoque manga y estrella de impacto (`UI/RadialBurst`).

## Efectos de pantalla (`ScreenFX`)

Speed lines anime (`UI/SpeedLines`, parpadeo "en dos" a 12 fps), lavados de color (fase 2 del jefe, ultimates).
El *radial blur* de los dashes se aproxima con motion blur + speed lines + aberración cromática.

## Shaders (`Assets/_BreathOfEclipse/Shaders`, HLSL para URP)

| Shader | Uso |
|---|---|
| `BreathOfEclipse/ToonLit` | Cel shading: luz en 2 pasos con color de sombra frío, sombras escalonadas, rim, especular estilizado, emisión, **outline por casco invertido con ancho dependiente de la distancia**, hit flash, dissolve, flash frames, SSAO, Forward+ |
| `BreathOfEclipse/VFXAdditive` / `VFXAlphaBlend` | Partículas y sprites: tinte HDR, soft particles, niebla correcta para aditivos, decals |
| `BreathOfEclipse/ElementRibbon` | Tubos de elemento: ruido que fluye, fresnel, cola deshilachada |
| `BreathOfEclipse/SwordTrail` | Trails de espada (ver arriba) |
| `BreathOfEclipse/Ghost` | Afterimages aditivos con fresnel |
| `BreathOfEclipse/SkyDome` | Cielo nocturno: degradado, estrellas titilantes, luna con cráteres y halo, nubes iluminadas, horizonte fundido con la niebla |
| `BreathOfEclipse/ToonWater` | Arroyo: color por profundidad, cáusticas escalonadas, espuma anime en orillas, reflejo de luna, fresnel |
| `BreathOfEclipse/ScreenDistortion` | Refracción de ondas de choque / calor (usa la Opaque Texture) |
| `BreathOfEclipse/Telegraph` | Aviso de ataque en el suelo: anillo + disco que se llena hasta el impacto |
| `BreathOfEclipse/UI/SpeedLines`, `UI/RadialBurst` | Overlays UI |

Se eligió **HLSL escrito a mano en lugar de Shader Graph** porque el outline necesita un pase extra (casco invertido)
y los flash frames necesitan variables globales en todos los shaders. Todos son compatibles con SRP Batcher y se
incluyen en *Always Included Shaders* (se crean desde código con `Shader.Find`).
Validación offline: `Tools/ShaderCheck/check_shaders.py` los compila con DXC contra la ShaderLibrary de URP 17.3.

## Entorno

"Volumétricos" falsos: conos de luz aditivos orientados a la luna (`LightShafts`) y bruma baja con partículas;
luciérnagas, pétalos y hojas (`AmbientParticles`). Impactos de entorno: polvo, rocas, grietas (decals temporales),
ondas de choque y debris de destructibles (cajas, jarrones, santuarios).
