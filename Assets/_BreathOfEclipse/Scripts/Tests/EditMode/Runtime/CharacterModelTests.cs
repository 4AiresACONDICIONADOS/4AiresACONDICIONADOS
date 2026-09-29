using BreathOfEclipse.Characters;
using NUnit.Framework;
using UnityEngine;

namespace BreathOfEclipse.Tests
{
    /// <summary>
    /// v0.4 real 3D characters: the CC0 Quaternius base body and animation libraries are imported as Humanoid,
    /// the skeleton passes validation (avatar, bones, scale, grip frames) and every clip the game maps exists.
    /// </summary>
    public class CharacterModelTests
    {
        [Test]
        public void BaseBody_IsInResources_AndHumanoid()
        {
            var body = Resources.Load<GameObject>(CharacterVisualProfile.BaseBody);
            Assert.IsNotNull(body, $"Resources/{CharacterVisualProfile.BaseBody} missing");
            var animator = body.GetComponentInChildren<Animator>();
            Assert.IsNotNull(animator, "no Animator on the base body");
            Assert.IsNotNull(animator.avatar, "no avatar: set Rig → Humanoid");
            Assert.IsTrue(animator.avatar.isHuman && animator.avatar.isValid, "avatar is not a valid Humanoid");
        }

        [Test]
        public void BaseBody_SkeletonValidates()
        {
            var prefab = Resources.Load<GameObject>(CharacterVisualProfile.BaseBody);
            Assume.That(prefab != null);
            var go = Object.Instantiate(prefab);
            try
            {
                var sk = HumanoidSkeleton.Build(go, 1.78f, out var report);
                Assert.IsNotNull(sk, report);
                Assert.That(sk.Height, Is.EqualTo(1.78f).Within(0.01f));
                Assert.That(sk.HipHeight, Is.InRange(0.7f, 1.1f), report);
                Assert.That(sk.ArmLengthR, Is.InRange(0.4f, 0.75f), report);
                Assert.IsNotNull(sk[HumanBodyBones.RightMiddleProximal], "finger bones drive the katana grip frame");
                Assert.Greater(sk.FingersR.Length, 3);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void AnimationLibrary_IsHumanoid_AndHasEveryMappedClip()
        {
            Assert.IsTrue(HumanoidClipLibrary.Available, "UAL clips missing or not Humanoid");
            foreach (var clip in HumanoidClipLibrary.SegmentClips())
                Assert.IsNotNull(HumanoidClipLibrary.Get(clip), $"clip {clip} not found");
            foreach (var name in new[] { "Idle_Loop", "Sword_Idle", "Walk_Loop", "Jog_Fwd_Loop", "Sprint_Loop", "NinjaJump_Start", "NinjaJump_Idle_Loop",
                         "NinjaJump_Land", "Hit_Chest", "Hit_Head", "Idle_Shield_Break", "Hit_Knockback", "LayToIdle", "Death01", "Sword_Block",
                         "Zombie_Idle_Loop", "Zombie_Walk_Fwd_Loop", "Jump_Start", "Jump_Loop", "Jump_Land" })
                Assert.IsNotNull(HumanoidClipLibrary.Get(name), $"locomotion / reaction clip {name} not found");
        }

        [Test]
        public void EveryClipSegment_MapsAKnownMotion()
        {
            foreach (var id in HumanoidClipLibrary.SegmentIds)
                Assert.IsTrue(MotionLibrary.Has(id), $"segment for unknown motion id {id}");
        }
    }
}
