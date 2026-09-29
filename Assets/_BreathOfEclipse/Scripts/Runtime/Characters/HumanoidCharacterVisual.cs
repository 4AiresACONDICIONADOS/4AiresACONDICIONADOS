using BreathOfEclipse.Core;
using BreathOfEclipse.Rendering;
using UnityEngine;

namespace BreathOfEclipse.Characters
{
    /// <summary>What the look builder does to an imported model.</summary>
    public enum VisualLook
    {
        /// <summary>Keep the model's own look (VRoid / custom FBX); only convert its materials to the toon shader.</summary>
        Toonify = 0,
        /// <summary>Base body dressed as the game's original swordsman: anime hair, dark top, short haori, hakama, belt, wraps, boots.</summary>
        AnimeSwordsman = 1,
        /// <summary>Base body as the Nightspawn demon: dark skin with glowing markings and eyes, claws, teeth, back spines.</summary>
        Nightspawn = 2,
        /// <summary>Base body as the Hollow Oni boss: huge, horned, bone mask, wild mane, glowing markings (phase 2 brighter).</summary>
        HollowOni = 3
    }

    /// <summary>
    /// An imported character (FBX / VRM with a Humanoid avatar) that replaces the procedural mannequin as the
    /// visual layer only: GameplayRoot → CharacterVisualRoot → Model (Animator, Humanoid skeleton) →
    /// RightHandWeaponSocket (BladeBase / BladeTip). Health, damage, breath, stamina, hit detection, skills, AI and
    /// saves never depend on it. Assign one to <c>GameDatabase.playerVisual</c>, or leave it empty to use the
    /// built-in anime swordsman (Quaternius base body, CC0) — see Settings → player visual.
    /// </summary>
    [CreateAssetMenu(menuName = "Breath of Eclipse/Character Visual", fileName = "Visual_")]
    public sealed class CharacterVisualProfile : ScriptableObject
    {
        [Tooltip("Model prefab with an Animator and a Humanoid avatar (e.g. a VRoid export imported with UniVRM, or an FBX set to Humanoid).")]
        public GameObject modelPrefab;
        [Tooltip("Resources path of the model when Model Prefab is empty (e.g. Quaternius/Characters/Superhero_Male_FullBody).")]
        public string resourcesModel;
        [Tooltip("Hybrid animation (recommended): real humanoid clips (Quaternius Universal Animation Library) plus the game's " +
                 "combat poses retargeted with IK, driven by the procedural animator. Off: the Animator Controller below is used.")]
        public bool hybridAnimation = true;
        public VisualLook look = VisualLook.Toonify;
        public LocomotionSet locomotion = LocomotionSet.Swordsman;
        [Tooltip("Standing height (m) the model is scaled to. 0 keeps the imported scale.")]
        public float targetHeight = 1.78f;
        [Tooltip("Legacy (hybrid off): Animator Controller with states named after motion ids (L1, L2, SkillRisingCut, Dodge…), " +
                 "floats Speed/VelocityX/VelocityZ, bools Grounded/Blocking/Sprinting/KnockedDown/Dead, trigger Hit.")]
        public RuntimeAnimatorController controller;
        public float modelScale = 1f;
        public Vector3 modelOffset;
        [Tooltip("Legacy (hybrid off): katana pose inside the right hand bone. The hybrid path finds the grip from the finger bones.")]
        public Vector3 weaponLocalPosition = new Vector3(0f, 0.02f, 0.04f);
        public Vector3 weaponLocalEuler = new Vector3(0f, 90f, 90f);
        [Tooltip("Legacy (hybrid off): left hand IK on the katana grip while idle / moving. 0 = clips only.")]
        [Range(0f, 1f)] public float leftHandGrip = 0.6f;
        public Vector3 sheathLocalPosition = new Vector3(-0.17f, 0.02f, 0.12f);
        public Vector3 sheathLocalEuler = new Vector3(20f, 180f, 0f);
        [Tooltip("Keep the game's katana scabbard on the imported hips (turn off if the model has its own).")]
        public bool keepScabbard = true;
        [Tooltip("Convert the model's materials to the game's anime toon shader (keeps its textures) so it matches the art style.")]
        public bool toonMaterials = true;
        public float outline = 1.4f;

        /// <summary>The CC0 Quaternius base body shipped with the project (Resources path).</summary>
        public const string BaseBody = "Quaternius/Characters/Superhero_Male_FullBody";

        /// <summary>A profile built at runtime (not saved) for the built-in characters.</summary>
        public static CharacterVisualProfile Runtime(VisualLook look, LocomotionSet set, float height)
        {
            var p = CreateInstance<CharacterVisualProfile>();
            p.name = "Visual_" + look;
            p.hideFlags = HideFlags.DontSave;
            p.resourcesModel = BaseBody;
            p.hybridAnimation = true;
            p.look = look;
            p.locomotion = set;
            p.targetHeight = height;
            p.keepScabbard = look == VisualLook.AnimeSwordsman;
            p.outline = look == VisualLook.HollowOni ? 1.3f : 1.4f;
            return p;
        }

        public GameObject ResolveModel()
        {
            if (modelPrefab != null) return modelPrefab;
            return string.IsNullOrEmpty(resourcesModel) ? null : Resources.Load<GameObject>(resourcesModel);
        }
    }

    /// <summary>Builds the imported visual on a gameplay root.</summary>
    public static class HumanoidCharacterVisual
    {
        /// <summary>
        /// v0.4 path: the model follows <paramref name="source"/> (real clips + retargeted procedural pose). Returns
        /// null — and leaves the procedural mannequin visible — if the model is missing or fails validation
        /// (invalid avatar, missing bones, broken scale), so the character is never invisible.
        /// </summary>
        public static HumanoidVisualDriver AttachHybrid(Transform gameplayRoot, CharacterRig rig, ProceduralAnimator source, CharacterVisualProfile profile, int layer)
        {
            if (profile == null || rig == null || source == null) return null;
            var prefab = profile.ResolveModel();
            if (prefab == null)
            {
                Debug.LogWarning($"[HumanoidCharacterVisual] Model '{(string.IsNullOrEmpty(profile.resourcesModel) ? profile.name : profile.resourcesModel)}' not found: keeping the procedural mannequin.");
                return null;
            }

            Transform visualRoot = null;
            var saved = SocketState.Capture(rig);
            try
            {
                visualRoot = new GameObject("CharacterVisualRoot").transform;
                visualRoot.SetParent(gameplayRoot, false);
                var model = Object.Instantiate(prefab, visualRoot, false);
                model.name = "Model";
                model.transform.localPosition = Vector3.zero;
                model.transform.localRotation = Quaternion.identity;
                var sk = HumanoidSkeleton.Build(model, profile.targetHeight, out var report);
                if (sk == null)
                {
                    Debug.LogWarning($"[HumanoidCharacterVisual] '{prefab.name}' cannot be used ({report}): keeping the procedural mannequin.");
                    Object.Destroy(visualRoot.gameObject);
                    return null;
                }
                Layers.SetLayerRecursively(model, layer);

                AttachSockets(gameplayRoot, rig, sk, profile);
                AnimeCharacterLook.Apply(sk, rig, profile, layer);
                rig.SetBodyVisible(false, profile.keepScabbard);
                if (profile.look == VisualLook.Nightspawn) rig.SetWeaponMeshVisible(false);

                var driver = gameplayRoot.gameObject.AddComponent<HumanoidVisualDriver>();
                driver.Initialize(source, rig, sk, visualRoot, profile.locomotion);
                if (Debug.isDebugBuild || Application.isEditor)
                    Debug.Log($"[HumanoidCharacterVisual] {gameplayRoot.name}: '{prefab.name}' ({profile.look}) · {report.Replace("\n", " · ")} · clips: {(driver.ClipsActive ? HumanoidClipLibrary.Count.ToString() : "none (procedural retarget)")}");
                return driver;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[HumanoidCharacterVisual] Could not build the 3D model for {gameplayRoot.name}: {e.Message}. Keeping the procedural mannequin.\n{e.StackTrace}");
                // The katana, scabbard and gameplay points go back to the mannequin before the model is removed.
                saved.Restore(rig);
                if (visualRoot != null)
                {
                    visualRoot.gameObject.SetActive(false);
                    Object.Destroy(visualRoot.gameObject);
                }
                rig.SetBodyVisible(true);
                rig.SetWeaponMeshVisible(true);
                return null;
            }
        }

        /// <summary>Parents and local poses of the gameplay points, to undo a failed attach.</summary>
        private struct SocketState
        {
            private (Transform t, Transform parent, Vector3 pos, Quaternion rot, Vector3 scale)[] _items;

            public static SocketState Capture(CharacterRig rig)
            {
                var list = new System.Collections.Generic.List<(Transform, Transform, Vector3, Quaternion, Vector3)>();
                foreach (var t in new[] { rig.Bone(RigBone.Weapon), rig.SheathSocket, rig.LockOnPoint, rig.EyePoint, rig.MouthSocket })
                    if (t != null) list.Add((t, t.parent, t.localPosition, t.localRotation, t.localScale));
                return new SocketState { _items = list.ToArray() };
            }

            public void Restore(CharacterRig rig)
            {
                if (_items == null) return;
                foreach (var (t, parent, pos, rot, scale) in _items)
                {
                    if (t == null) continue;
                    t.SetParent(parent, false);
                    t.localPosition = pos;
                    t.localRotation = rot;
                    t.localScale = scale;
                }
                rig.ClearImportedBody();
            }
        }

        /// <summary>
        /// Moves the gameplay points onto the model: RightHandWeaponSocket (katana grip found from the finger bones),
        /// SheathSocket (left hip), LockOnPoint (chest), EyePoint / MouthBreathSocket (head).
        /// </summary>
        private static void AttachSockets(Transform gameplayRoot, CharacterRig rig, HumanoidSkeleton sk, CharacterVisualProfile profile)
        {
            var handR = sk[HumanBodyBones.RightHand];
            var weapon = rig.Bone(RigBone.Weapon);
            weapon.SetParent(handR, false);
            weapon.localPosition = sk.PalmLocalR;
            weapon.localRotation = Quaternion.Inverse(sk.GripToHandR);
            weapon.localScale = Vector3.one * (rig.Scale / Mathf.Max(1e-5f, handR.lossyScale.x));

            float k = sk.Height / 1.8f;
            Quaternion rootRot = gameplayRoot.rotation;
            Vector3 origin = gameplayRoot.position;
            var hips = sk[HumanBodyBones.Hips];
            if (rig.SheathSocket != null)
            {
                rig.SheathSocket.SetParent(hips, true);
                rig.SheathSocket.position = origin + rootRot * new Vector3(-0.2f * k, sk.HipHeight + 0.03f * k, 0.12f * k);
                rig.SheathSocket.rotation = rootRot * Quaternion.LookRotation(new Vector3(0.05f, -0.34f, -0.94f), Vector3.up);
                rig.SheathSocket.localScale = Vector3.one * (rig.Scale / Mathf.Max(1e-5f, hips.lossyScale.x));
            }
            var chest = sk[HumanBodyBones.UpperChest] ?? sk[HumanBodyBones.Chest] ?? sk[HumanBodyBones.Spine];
            if (rig.LockOnPoint != null && chest != null) rig.LockOnPoint.SetParent(chest, true);

            var head = sk[HumanBodyBones.Head];
            // Face points measured on the standing model: eyes ~93.5% of the height, mouth ~89%, in front of the head bone.
            Vector3 headPos = head.position;
            Vector3 fwd = rootRot * Vector3.forward;
            Vector3 eye = new Vector3(headPos.x, origin.y + sk.Height * 0.935f, headPos.z) + fwd * (0.085f * k);
            Vector3 mouth = new Vector3(headPos.x, origin.y + sk.Height * 0.89f, headPos.z) + fwd * (0.11f * k);
            rig.SetImportedBody(head, sk.ModelRoot.parent, mouth, eye);
        }

        /// <summary>
        /// Legacy path (hybrid animation off): the model plays its own Animator Controller through
        /// <see cref="MecanimCharacterAnimator"/>, which then replaces the procedural animator.
        /// </summary>
        public static MecanimCharacterAnimator Attach(Transform gameplayRoot, CharacterRig rig, CharacterVisualProfile profile, int layer)
        {
            if (profile == null) return null;
            var prefab = profile.ResolveModel();
            if (prefab == null) return null;
            var model = Object.Instantiate(prefab, gameplayRoot, false);
            model.name = "VisualModel";
            model.transform.localPosition = profile.modelOffset;
            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = Vector3.one * profile.modelScale;
            Layers.SetLayerRecursively(model, layer);

            var animator = model.GetComponentInChildren<Animator>();
            if (animator == null) animator = model.AddComponent<Animator>();
            animator.applyRootMotion = false;
            if (profile.controller != null) animator.runtimeAnimatorController = profile.controller;

            // The mannequin keeps the gameplay points (weapon, lock-on, eyes) but is no longer drawn.
            rig.SetBodyVisible(false, profile.keepScabbard);
            var hand = animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.RightHand) : FindDeep(model.transform, "RightHand");
            if (hand != null)
            {
                var socket = new GameObject("RightHandWeaponSocket").transform;
                socket.SetParent(hand, false);
                socket.localPosition = profile.weaponLocalPosition;
                socket.localRotation = Quaternion.Euler(profile.weaponLocalEuler);
                var weapon = rig.Bone(RigBone.Weapon);
                weapon.SetParent(socket, false);
                weapon.localPosition = Vector3.zero;
                weapon.localRotation = Quaternion.identity;
            }
            if (animator.isHuman)
            {
                var chest = animator.GetBoneTransform(HumanBodyBones.Chest);
                if (chest == null) chest = animator.GetBoneTransform(HumanBodyBones.Spine);
                var head = animator.GetBoneTransform(HumanBodyBones.Head);
                if (chest != null && rig.LockOnPoint != null) rig.LockOnPoint.SetParent(chest, false);
                if (head != null && rig.EyePoint != null) rig.EyePoint.SetParent(head, false);
                var hipsBone = animator.GetBoneTransform(HumanBodyBones.Hips);
                if (hipsBone != null && rig.SheathSocket != null)
                {
                    rig.SheathSocket.SetParent(hipsBone, false);
                    rig.SheathSocket.localPosition = profile.sheathLocalPosition;
                    rig.SheathSocket.localRotation = Quaternion.Euler(profile.sheathLocalEuler);
                }
            }
            if (profile.toonMaterials) Toonify(model, profile.outline);

            var adapter = animator.gameObject.GetComponent<MecanimCharacterAnimator>();
            if (adapter == null) adapter = animator.gameObject.AddComponent<MecanimCharacterAnimator>();
            adapter.SetWeaponPoints(rig.WeaponBase, rig.WeaponTip);
            adapter.SetLeftHandGrip(profile.leftHandGrip);
            return adapter;
        }

        private static void Toonify(GameObject model, float outline)
        {
            foreach (var r in model.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    var m = mats[i];
                    if (m == null) continue;
                    Texture tex = m.HasProperty("_BaseMap") ? m.GetTexture("_BaseMap") : m.HasProperty("_MainTex") ? m.GetTexture("_MainTex") : null;
                    Color color = m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : m.HasProperty("_Color") ? m.GetColor("_Color") : Color.white;
                    mats[i] = MaterialFactory.Toon(color, outline, true, null, 0.52f, 0.4f, 0f, tex);
                }
                r.sharedMaterials = mats;
            }
        }

        private static Transform FindDeep(Transform root, string contains)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name.Contains(contains)) return t;
            return null;
        }
    }
}
