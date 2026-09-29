using BreathOfEclipse.Core;
using BreathOfEclipse.Rendering;
using UnityEngine;

namespace BreathOfEclipse.Characters
{
    /// <summary>
    /// An imported character (FBX / VRM with a Humanoid avatar) that replaces the procedural mannequin as the
    /// visual layer only: Gameplay root → visual model → Animator → RightHandWeaponSocket (BladeBase / BladeTip).
    /// Assign it to <c>GameDatabase.playerVisual</c>; leave it empty to keep the mannequin.
    /// </summary>
    [CreateAssetMenu(menuName = "Breath of Eclipse/Character Visual", fileName = "Visual_")]
    public sealed class CharacterVisualProfile : ScriptableObject
    {
        [Tooltip("Model prefab with an Animator and a Humanoid avatar (e.g. a VRoid export imported with UniVRM, or an FBX set to Humanoid).")]
        public GameObject modelPrefab;
        [Tooltip("Animator Controller: states named after motion ids (L1, L2, L3, L4, H1, H2, L2H, A1, SkillRisingCut, Dodge, Parry…), " +
                 "floats Speed/VelocityX/VelocityZ, bools Grounded/Blocking/Sprinting/KnockedDown/Dead, trigger Hit.")]
        public RuntimeAnimatorController controller;
        public float modelScale = 1f;
        public Vector3 modelOffset;
        [Tooltip("Katana pose inside the right hand bone.")]
        public Vector3 weaponLocalPosition = new Vector3(0f, 0.02f, 0.04f);
        public Vector3 weaponLocalEuler = new Vector3(0f, 90f, 90f);
        [Tooltip("Left hand IK on the katana grip while idle / moving (two-handed stance). 0 = clips only.")]
        [Range(0f, 1f)] public float leftHandGrip = 0.6f;
        [Tooltip("Scabbard mouth on the hips (left side); the SheathSocket moves there.")]
        public Vector3 sheathLocalPosition = new Vector3(-0.17f, 0.02f, 0.12f);
        public Vector3 sheathLocalEuler = new Vector3(20f, 180f, 0f);
        [Tooltip("Keep the game's katana scabbard on the imported hips (turn off if the model has its own).")]
        public bool keepScabbard = true;
        [Tooltip("Convert the model's materials to the game's toon shader (keeps its textures) so it matches the art style.")]
        public bool toonMaterials = true;
        public float outline = 1.4f;
    }

    /// <summary>Builds the imported visual on a gameplay root and returns its Mecanim animator adapter.</summary>
    public static class HumanoidCharacterVisual
    {
        public static MecanimCharacterAnimator Attach(Transform gameplayRoot, CharacterRig rig, CharacterVisualProfile profile, int layer)
        {
            if (profile == null || profile.modelPrefab == null) return null;
            var model = Object.Instantiate(profile.modelPrefab, gameplayRoot, false);
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
                    // The mannequin's scabbard follows the imported hips (hidden with the body; the socket stays useful).
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
