#!/usr/bin/env python3
"""
Generates the Unity files that must exist before the project is opened for the first time:

  * .meta files for every asset/folder under Assets/ that has none, with DETERMINISTIC GUIDs
    (md5 of the project-relative path), so scenes can reference scripts by GUID and every clone agrees.
    Existing .meta files are never modified.
  * The scenes (00_Boot, 01_MainMenu, 02_MoonlitForest, 03_CombatTest, 04_FrontierRegion). Each contains one root object with
    its scene script; the world itself is built procedurally at runtime (see GameplaySceneBuilder).
  * ProjectSettings/EditorBuildSettings.asset (scene list, 00_Boot first).
  * The project's shaders in GraphicsSettings "Always Included Shaders" (they are created from code with
    Shader.Find, so no material asset references them and builds would otherwise strip them).

Run from anywhere:  python3 Tools/Generators/generate_project_files.py
Re-running is safe: scenes/build settings are rewritten, metas only created when missing.
Use --force-scenes=false to keep hand-edited scenes.
"""
import hashlib
import os
import re
import sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
ASSETS = os.path.join(ROOT, "Assets")
SCENES_DIR = "Assets/_BreathOfEclipse/Scenes"
SCRIPTS = "Assets/_BreathOfEclipse/Scripts/Runtime"
SHADERS_DIR = "Assets/_BreathOfEclipse/Shaders"
INPUT_ACTION_IMPORTER_GUID = "8404be70184654265930450def6a9037"


def guid_for(rel_path):
    return hashlib.md5(("BreathOfEclipse:" + rel_path.replace("\\", "/")).encode("utf-8")).hexdigest()


def rel(path):
    return os.path.relpath(path, ROOT).replace("\\", "/")


def meta_text(rel_path, is_dir):
    g = guid_for(rel_path)
    if is_dir:
        return f"fileFormatVersion: 2\nguid: {g}\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {{}}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n"
    ext = os.path.splitext(rel_path)[1].lower()
    if ext == ".cs":
        return (f"fileFormatVersion: 2\nguid: {g}\nMonoImporter:\n  externalObjects: {{}}\n  serializedVersion: 2\n  defaultReferences: []\n"
                "  executionOrder: 0\n  icon: {instanceID: 0}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n")
    if ext == ".shader":
        return (f"fileFormatVersion: 2\nguid: {g}\nShaderImporter:\n  externalObjects: {{}}\n  defaultTextures: []\n  nonModifiableTextures: []\n"
                "  userData: \n  assetBundleName: \n  assetBundleVariant: \n")
    if ext in (".hlsl", ".cginc"):
        return f"fileFormatVersion: 2\nguid: {g}\nShaderIncludeImporter:\n  externalObjects: {{}}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n"
    if ext == ".asmdef":
        return f"fileFormatVersion: 2\nguid: {g}\nAssemblyDefinitionImporter:\n  externalObjects: {{}}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n"
    if ext == ".inputactions":
        return (f"fileFormatVersion: 2\nguid: {g}\nScriptedImporter:\n  internalIDToNameTable: []\n  externalObjects: {{}}\n  serializedVersion: 2\n"
                "  userData: \n  assetBundleName: \n  assetBundleVariant: \n"
                f"  script: {{fileID: 11500000, guid: {INPUT_ACTION_IMPORTER_GUID}, type: 3}}\n"
                "  generateWrapperCode: 0\n  wrapperCodePath: \n  wrapperClassName: \n  wrapperCodeNamespace: \n")
    if ext in (".wav", ".ogg", ".mp3"):
        # Voice / sound clips: preloaded, Vorbis compressed, loaded through Resources by name.
        return (f"fileFormatVersion: 2\nguid: {g}\nAudioImporter:\n  externalObjects: {{}}\n  serializedVersion: 7\n"
                "  defaultSettings:\n    serializedVersion: 2\n    loadType: 0\n    sampleRateSetting: 0\n    sampleRateOverride: 44100\n"
                "    compressionFormat: 1\n    quality: 0.7\n    conversionMode: 0\n    preloadAudioData: 1\n"
                "  platformSettingOverrides: {}\n  forceToMono: 0\n  normalize: 0\n  loadInBackground: 0\n  ambisonic: 0\n  3D: 1\n"
                "  userData: \n  assetBundleName: \n  assetBundleVariant: \n")
    if ext == ".fbx":
        return model_meta(g, rel_path)
    if ext in (".png", ".tga", ".jpg"):
        return texture_meta(g)
    if ext in (".md", ".txt", ".json", ".xml", ".bytes", ".csv"):
        return f"fileFormatVersion: 2\nguid: {g}\nTextScriptImporter:\n  externalObjects: {{}}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n"
    return f"fileFormatVersion: 2\nguid: {g}\nDefaultImporter:\n  externalObjects: {{}}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n"


def model_meta(g, rel_path):
    """
    FBX import settings (Quaternius setup): bake axis conversion, file units, no material import (the game
    converts every renderer to its own toon shader). Characters and the animation libraries are Humanoid with an
    avatar created from the file; hair meshes are plain static meshes. Characters/HumanoidImportPostprocessor.cs
    applies the same settings (plus per-clip loop / bake-into-pose flags) on every import.
    """
    p = rel_path.replace("\\", "/")
    hair = "/Hair/" in p
    animations = "/Animations/" in p
    animation_type = 0 if hair else 3
    return (f"fileFormatVersion: 2\nguid: {g}\nModelImporter:\n  serializedVersion: 22200\n  internalIDToNameTable: []\n"
            "  externalObjects: {}\n  materials:\n    materialImportMode: 0\n    materialName: 0\n    materialSearch: 1\n"
            "    materialLocation: 1\n  animations:\n    legacyGenerateAnimations: 4\n    bakeSimulation: 0\n"
            "    resampleCurves: 1\n    optimizeGameObjects: 0\n    removeConstantScaleCurves: 0\n    motionNodeName: \n"
            "    animationImportErrors: \n    animationImportWarnings: \n    animationRetargetingWarnings: \n"
            "    animationDoRetargetingWarnings: 0\n    importAnimatedCustomProperties: 0\n    importConstraints: 0\n"
            "    animationCompression: 1\n    animationRotationError: 0.5\n    animationPositionError: 0.5\n"
            "    animationScaleError: 0.5\n    animationWrapMode: 0\n    extraExposedTransformPaths: []\n"
            f"    extraUserProperties: []\n    clipAnimations: []\n    isReadable: {0 if animations else 1}\n  meshes:\n"
            "    lODScreenPercentages: []\n    globalScale: 1\n    meshCompression: 0\n    addColliders: 0\n"
            "    useSRGBMaterialColor: 1\n    sortHierarchyByName: 1\n    importPhysicalCameras: 0\n    importVisibility: 1\n"
            "    importBlendShapes: 1\n    importCameras: 0\n    importLights: 0\n    nodeNameCollisionStrategy: 1\n"
            "    fileIdsGeneration: 2\n    swapUVChannels: 0\n    generateSecondaryUV: 0\n    useFileUnits: 1\n"
            "    keepQuads: 0\n    weldVertices: 1\n    bakeAxisConversion: 1\n    preserveHierarchy: 0\n"
            "    skinWeightsMode: 0\n    maxBonesPerVertex: 4\n    minBoneWeight: 0.001\n    optimizeBones: 1\n"
            "    meshOptimizationFlags: -1\n    indexFormat: 0\n    useFileScale: 1\n    strictVertexDataChecks: 0\n"
            "  tangentSpace:\n    normalSmoothAngle: 60\n    normalImportMode: 0\n    tangentImportMode: 3\n"
            "    normalCalculationMode: 4\n    legacyComputeAllNormalsFromSmoothingGroupsWhenMeshHasBlendShapes: 0\n"
            "    blendShapeNormalImportMode: 1\n    normalSmoothingSource: 0\n  referencedClips: []\n"
            f"  importAnimation: {1 if animations else 0}\n  humanDescription:\n    serializedVersion: 3\n    human: []\n"
            "    skeleton: []\n    armTwist: 0.5\n    foreArmTwist: 0.5\n    upperLegTwist: 0.5\n    legTwist: 0.5\n"
            "    armStretch: 0.05\n    legStretch: 0.05\n    feetSpacing: 0\n    globalScale: 1\n    rootMotionBoneName: \n"
            "    hasTranslationDoF: 0\n    hasExtraRoot: 0\n    skeletonHasParents: 1\n"
            "  lastHumanDescriptionAvatarSource: {instanceID: 0}\n  autoGenerateAvatarMappingIfUnspecified: 1\n"
            f"  animationType: {animation_type}\n  humanoidOversampling: 1\n  avatarSetup: {0 if hair else 1}\n"
            "  addHumanoidExtraRootOnlyWhenUsingAvatar: 1\n  importBlendShapeDeformPercent: 1\n"
            "  remapMaterialsIfMaterialImportModeIsNone: 0\n  additionalBone: 0\n  userData: \n  assetBundleName: \n"
            "  assetBundleVariant: \n")


def texture_meta(g):
    """Color texture, mipmapped, at most 1K (performance budget), normal-quality compression."""
    return (f"fileFormatVersion: 2\nguid: {g}\nTextureImporter:\n  internalIDToNameTable: []\n  externalObjects: {{}}\n"
            "  serializedVersion: 13\n  mipmaps:\n    mipMapMode: 0\n    enableMipMap: 1\n    sRGBTexture: 1\n"
            "    linearTexture: 0\n    fadeOut: 0\n    borderMipMap: 0\n    mipMapsPreserveCoverage: 0\n"
            "    alphaTestReferenceValue: 0.5\n    mipMapFadeDistanceStart: 1\n    mipMapFadeDistanceEnd: 3\n"
            "  bumpmap:\n    convertToNormalMap: 0\n    externalNormalMap: 0\n    heightScale: 0.25\n    normalMapFilter: 0\n"
            "    flipGreenChannel: 0\n  isReadable: 0\n  streamingMipmaps: 0\n  streamingMipmapsPriority: 0\n  vTOnly: 0\n"
            "  ignoreMipmapLimit: 0\n  grayScaleToAlpha: 0\n  generateCubemap: 6\n  cubemapConvolution: 0\n"
            "  seamlessCubemap: 0\n  textureFormat: 1\n  maxTextureSize: 1024\n  textureSettings:\n    serializedVersion: 2\n"
            "    filterMode: 1\n    aniso: 1\n    mipBias: 0\n    wrapU: 1\n    wrapV: 1\n    wrapW: 1\n  nPOTScale: 1\n"
            "  lightmap: 0\n  compressionQuality: 50\n  spriteMode: 0\n  alphaUsage: 1\n  alphaIsTransparency: 0\n"
            "  textureType: 0\n  textureShape: 1\n  singleChannelComponent: 0\n  maxTextureSizeSet: 0\n"
            "  compressionQualitySet: 0\n  textureFormatSet: 0\n  ignorePngGamma: 0\n  applyGammaDecoding: 0\n"
            "  swizzle: 50462976\n  cookieLightType: 0\n  platformSettings:\n  - serializedVersion: 4\n"
            "    buildTarget: DefaultTexturePlatform\n    maxTextureSize: 1024\n    resizeAlgorithm: 0\n    textureFormat: -1\n"
            "    textureCompression: 1\n    compressionQuality: 50\n    crunchedCompression: 0\n    allowsAlphaSplitting: 0\n"
            "    overridden: 0\n    ignorePlatformSupport: 0\n    androidETC2FallbackOverride: 0\n"
            "    forceMaximumCompressionQuality_BC6H_BC7: 0\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n")


def ensure_metas():
    created = 0
    for dirpath, dirnames, filenames in os.walk(ASSETS):
        dirnames[:] = [d for d in dirnames if not d.startswith(".") and not d.endswith("~")]
        entries = [(os.path.join(dirpath, d), True) for d in dirnames] + \
                  [(os.path.join(dirpath, f), False) for f in filenames if not f.endswith(".meta") and not f.startswith(".")]
        for path, is_dir in entries:
            meta = path + ".meta"
            if os.path.exists(meta):
                continue
            with open(meta, "w", encoding="utf-8", newline="\n") as f:
                f.write(meta_text(rel(path), is_dir))
            created += 1
    return created


def read_guid(asset_rel):
    meta = os.path.join(ROOT, asset_rel + ".meta")
    with open(meta, encoding="utf-8") as f:
        m = re.search(r"^guid:\s*([0-9a-f]{32})", f.read(), re.M)
    if not m:
        raise RuntimeError("No guid in " + meta)
    return m.group(1)


SCENE_HEADER = """%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!29 &1
OcclusionCullingSettings:
  m_ObjectHideFlags: 0
  serializedVersion: 2
  m_OcclusionBakeSettings:
    smallestOccluder: 5
    smallestHole: 0.25
    backfaceThreshold: 100
  m_SceneGUID: 00000000000000000000000000000000
  m_OcclusionCullingData: {fileID: 0}
--- !u!104 &2
RenderSettings:
  m_ObjectHideFlags: 0
  serializedVersion: 10
  m_Fog: 1
  m_FogColor: {r: 0.07, g: 0.09, b: 0.17, a: 1}
  m_FogMode: 3
  m_FogDensity: 0.016
  m_LinearFogStart: 0
  m_LinearFogEnd: 300
  m_AmbientSkyColor: {r: 0.16, g: 0.2, b: 0.36, a: 1}
  m_AmbientEquatorColor: {r: 0.1, g: 0.12, b: 0.22, a: 1}
  m_AmbientGroundColor: {r: 0.05, g: 0.05, b: 0.08, a: 1}
  m_AmbientIntensity: 1
  m_AmbientMode: 1
  m_SubtractiveShadowColor: {r: 0.42, g: 0.478, b: 0.627, a: 1}
  m_SkyboxMaterial: {fileID: 0}
  m_HaloStrength: 0.5
  m_FlareStrength: 1
  m_FlareFadeSpeed: 3
  m_HaloTexture: {fileID: 0}
  m_SpotCookie: {fileID: 10001, guid: 0000000000000000e000000000000000, type: 0}
  m_DefaultReflectionMode: 0
  m_DefaultReflectionResolution: 128
  m_ReflectionBounces: 1
  m_ReflectionIntensity: 1
  m_CustomReflection: {fileID: 0}
  m_Sun: {fileID: 0}
  m_IndirectSpecularColor: {r: 0, g: 0, b: 0, a: 1}
  m_UseRadianceAmbientProbe: 0
--- !u!157 &3
LightmapSettings:
  m_ObjectHideFlags: 0
  serializedVersion: 12
  m_GISettings:
    serializedVersion: 2
    m_BounceScale: 1
    m_IndirectOutputScale: 1
    m_AlbedoBoost: 1
    m_EnvironmentLightingMode: 0
    m_EnableBakedLightmaps: 0
    m_EnableRealtimeLightmaps: 0
  m_LightmapEditorSettings:
    serializedVersion: 12
    m_Resolution: 2
    m_BakeResolution: 40
    m_AtlasSize: 1024
    m_AO: 0
    m_AOMaxDistance: 1
    m_CompAOExponent: 1
    m_CompAOExponentDirect: 0
    m_ExtractAmbientOcclusion: 0
    m_Padding: 2
    m_LightmapParameters: {fileID: 0}
    m_LightmapsBakeMode: 1
    m_TextureCompression: 1
    m_ReflectionCompression: 2
    m_MixedBakeMode: 2
    m_BakeBackend: 1
    m_PVRSampling: 1
    m_PVRDirectSampleCount: 32
    m_PVRSampleCount: 512
    m_PVRBounces: 2
    m_PVREnvironmentSampleCount: 256
    m_PVREnvironmentReferencePointCount: 2048
    m_PVRFilteringMode: 1
    m_PVRDenoiserTypeDirect: 1
    m_PVRDenoiserTypeIndirect: 1
    m_PVRDenoiserTypeAO: 1
    m_PVRFilterTypeDirect: 0
    m_PVRFilterTypeIndirect: 0
    m_PVRFilterTypeAO: 0
    m_PVREnvironmentMIS: 1
    m_PVRCulling: 1
    m_PVRFilteringGaussRadiusDirect: 1
    m_PVRFilteringGaussRadiusIndirect: 5
    m_PVRFilteringGaussRadiusAO: 2
    m_PVRFilteringAtrousPositionSigmaDirect: 0.5
    m_PVRFilteringAtrousPositionSigmaIndirect: 2
    m_PVRFilteringAtrousPositionSigmaAO: 1
    m_ExportTrainingData: 0
    m_TrainingDataDestination: TrainingData
    m_LightProbeSampleCountMultiplier: 4
  m_LightingDataAsset: {fileID: 0}
  m_LightingSettings: {fileID: 0}
--- !u!196 &4
NavMeshSettings:
  serializedVersion: 2
  m_ObjectHideFlags: 0
  m_BuildSettings:
    serializedVersion: 3
    agentTypeID: 0
    agentRadius: 0.5
    agentHeight: 2
    agentSlope: 45
    agentClimb: 0.4
    ledgeDropHeight: 0
    maxJumpAcrossDistance: 0
    minRegionArea: 2
    manualCellSize: 0
    cellSize: 0.16666667
    manualTileSize: 0
    tileSize: 256
    buildHeightMesh: 0
    maxJobWorkers: 0
    preserveTilesOutsideBounds: 0
    debug:
      m_Flags: 0
  m_NavMeshData: {fileID: 0}
"""

GO_ID, TR_ID, MB_ID = 1180000001, 1180000002, 1180000003


def scene_text(object_name, script_guid, fields):
    field_lines = "".join(f"  {k}: {v}\n" for k, v in fields)
    return SCENE_HEADER + f"""--- !u!1 &{GO_ID}
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: {TR_ID}}}
  - component: {{fileID: {MB_ID}}}
  m_Layer: 0
  m_Name: {object_name}
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!4 &{TR_ID}
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {GO_ID}}}
  serializedVersion: 2
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: 0}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
--- !u!114 &{MB_ID}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {GO_ID}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {script_guid}, type: 3}}
  m_Name:
  m_EditorClassIdentifier:
{field_lines}--- !u!1660057539 &9223372036854775807
SceneRoots:
  m_ObjectHideFlags: 0
  m_Roots:
  - {{fileID: {TR_ID}}}
"""


SCENES = [
    ("00_Boot", "Boot", f"{SCRIPTS}/Scenes/BootLoader.cs", [("minimumSplash", "1.2"), ("nextScene", "01_MainMenu")]),
    ("01_MainMenu", "MainMenu", f"{SCRIPTS}/Scenes/MainMenuController.cs", []),
    ("02_MoonlitForest", "MoonlitForest", f"{SCRIPTS}/Environment/MoonlitForestBuilder.cs",
     [("buildEnvironment", "1"), ("playerSpawn", "{x: 0, y: 0.5, z: -78}"), ("playerSpawnYaw", "0"), ("music", "forest")]),
    ("03_CombatTest", "CombatTest", f"{SCRIPTS}/Environment/CombatTestBuilder.cs",
     [("buildEnvironment", "1"), ("playerSpawn", "{x: 0, y: 0.5, z: -10}"), ("playerSpawnYaw", "0"), ("music", "combat")]),
    ("04_FrontierRegion", "FrontierRegion", f"{SCRIPTS}/World/FrontierRegionBuilder.cs",
     [("buildEnvironment", "1"), ("playerSpawn", "{x: -19, y: 0.5, z: -165}"), ("playerSpawnYaw", "90"), ("music", "")]),
]


def write_scenes(only_missing=False):
    os.makedirs(os.path.join(ROOT, SCENES_DIR), exist_ok=True)
    written = 0
    for scene_name, object_name, script, fields in SCENES:
        script_guid = read_guid(script)
        path = os.path.join(ROOT, SCENES_DIR, scene_name + ".unity")
        if only_missing and os.path.exists(path):
            continue
        written += 1
        with open(path, "w", encoding="utf-8", newline="\n") as f:
            f.write(scene_text(object_name, script_guid, fields))
    return written


def write_build_settings():
    lines = ["%YAML 1.1", "%TAG !u! tag:unity3d.com,2011:", "--- !u!1045 &1", "EditorBuildSettings:", "  m_ObjectHideFlags: 0",
             "  serializedVersion: 2", "  m_Scenes:"]
    for scene_name, _, _, _ in SCENES:
        scene_rel = f"{SCENES_DIR}/{scene_name}.unity"
        lines += ["  - enabled: 1", f"    path: {scene_rel}", f"    guid: {read_guid(scene_rel)}"]
    lines += ["  m_configObjects: {}", "  m_UseUCBPForAssetBundles: 0", ""]
    with open(os.path.join(ROOT, "ProjectSettings/EditorBuildSettings.asset"), "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(lines))


def write_always_included_shaders():
    path = os.path.join(ROOT, "ProjectSettings/GraphicsSettings.asset")
    text = open(path, encoding="utf-8").read()
    shaders = sorted(p for p in os.listdir(os.path.join(ROOT, SHADERS_DIR)) if p.endswith(".shader"))
    entries = [f"  - {{fileID: 4800000, guid: {read_guid(SHADERS_DIR + '/' + s)}, type: 3}}" for s in shaders]
    m = re.search(r"(  m_AlwaysIncludedShaders:\n)((?:  - .*\n)*)", text)
    if not m:
        raise RuntimeError("m_AlwaysIncludedShaders not found")
    existing = [l for l in m.group(2).splitlines() if "type: 0" in l]  # keep Unity built-ins
    block = m.group(1) + "\n".join(existing + entries) + "\n"
    text = text[:m.start()] + block + text[m.end():]
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        f.write(text)
    return len(entries)


def main():
    force_scenes = "--force-scenes=false" not in sys.argv
    created = ensure_metas()
    # Without forcing, hand-edited scenes are kept and only scenes that do not exist yet are written.
    if write_scenes(only_missing=not force_scenes):
        created += ensure_metas()
    write_build_settings()
    n = write_always_included_shaders()
    print(f"metas created: {created}; scenes: {len(SCENES)}; always-included shaders: {n}")


if __name__ == "__main__":
    main()
