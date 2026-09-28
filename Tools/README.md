# Tools

Herramientas de desarrollo que **no forman parte del juego** (Unity no las importa: están fuera de `Assets/`).

## Generators/

| Script | Qué hace |
|---|---|
| `generate_input_actions.py` | Genera `BreathOfEclipseControls.inputactions` y la copia embebida `DefaultInputActions.cs` (mismos datos). Edita la tabla de teclas del script y vuelve a ejecutarlo, o edita el asset en Unity. |
| `generate_project_files.py` | Crea los `.meta` que falten (GUIDs deterministas), las 4 escenas, `EditorBuildSettings.asset` y añade los shaders del proyecto a *Always Included Shaders*. Nunca modifica metas existentes. `--force-scenes=false` conserva escenas editadas a mano. |

```bash
python3 Tools/Generators/generate_input_actions.py
python3 Tools/Generators/generate_project_files.py
```

## CompileCheck/

Compila el código del juego **sin abrir Unity**, contra los ensamblados reales del editor, para detectar errores
antes de la primera importación.

| Proyecto | Comprueba |
|---|---|
| `ThirdParty/InputSystem`, `ThirdParty/UGUI` | Compilan el código fuente de esos paquetes a DLLs de referencia (`bin/refs`) |
| `UrpStubs` | Firma de la API de URP usada por el juego (Volume overrides, datos de cámara) |
| `Runtime` | `Scripts/Logic` + `Scripts/Runtime` como build de jugador (sin `UNITY_EDITOR`) |
| `RuntimeEditor` | Runtime + `Scripts/Editor` + `Scripts/Tests` con `UNITY_EDITOR` |
| `LogicTests` | Ejecuta los tests NUnit de la capa `Logic` con `dotnet test` |

Rutas configurables en `Directory.Build.props` (`UnityManaged`, `UnityBuiltInPackages`, `InputSystemSource`).

```bash
cd Tools/CompileCheck/ThirdParty/InputSystem && dotnet build -c Release
cd ../UGUI && dotnet build -c Release
cd ../../UrpStubs && dotnet build -c Release
cd ../Runtime && dotnet build -c Release
cd ../RuntimeEditor && dotnet build -c Release
cd ../LogicTests && dotnet test -c Release
```

## ShaderCheck/

`check_shaders.py` extrae cada `HLSLPROGRAM` de los `.shader` del proyecto y compila vértice y fragmento con
**DXC** contra la ShaderLibrary de URP/Core del editor, en varias combinaciones de keywords (sin keywords, primeras y
últimas opciones de cada `multi_compile`, Forward+ con sombras/SSAO/niebla, GPU instancing).

```bash
DXC=/ruta/a/dxc UNITY_PACKAGES=/ruta/Editor/Data/Resources/PackageManager/BuiltInPackages \
    python3 Tools/ShaderCheck/check_shaders.py
```

DXC: <https://github.com/microsoft/DirectXShaderCompiler/releases>. No valida la sintaxis ShaderLab (Unity lo hace al importar).
