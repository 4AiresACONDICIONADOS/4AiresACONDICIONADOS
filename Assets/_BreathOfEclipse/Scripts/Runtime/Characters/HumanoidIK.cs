using UnityEngine;

namespace BreathOfEclipse.Characters
{
    /// <summary>
    /// IK for imported skeletons whose bone axes are unknown: every rotation is a minimal world-space delta, so the
    /// twist the clip gave each bone is kept (forearm roll, knee direction) and only the bend changes.
    /// </summary>
    public static class HumanoidIK
    {
        /// <summary>
        /// Two-bone chain (upper arm → forearm → hand, thigh → shin → foot): the end reaches <paramref name="target"/>,
        /// the middle joint bends toward <paramref name="pole"/>.
        /// </summary>
        public static void SolveTwoBone(Transform upper, Transform lower, Transform end, Vector3 target, Vector3 pole, float weight = 1f)
        {
            if (upper == null || lower == null || end == null || weight <= 0f) return;
            Vector3 a = upper.position;
            Vector3 b0 = lower.position;
            Vector3 c0 = end.position;
            float l1 = Vector3.Distance(a, b0);
            float l2 = Vector3.Distance(b0, c0);
            if (l1 < 1e-5f || l2 < 1e-5f) return;
            if (weight < 1f) target = Vector3.Lerp(c0, target, weight);

            Vector3 toTarget = target - a;
            float d = toTarget.magnitude;
            if (d < 1e-4f) return;
            Vector3 dir = toTarget / d;
            d = Mathf.Clamp(d, Mathf.Abs(l1 - l2) + 0.002f, (l1 + l2) * 0.9995f);

            Vector3 poleDir = Vector3.ProjectOnPlane(pole - a, dir);
            if (poleDir.sqrMagnitude < 1e-6f) poleDir = Vector3.ProjectOnPlane(b0 - a, dir);
            if (poleDir.sqrMagnitude < 1e-6f) poleDir = Vector3.ProjectOnPlane(Vector3.down, dir);
            if (poleDir.sqrMagnitude < 1e-6f) poleDir = Vector3.ProjectOnPlane(Vector3.forward, dir);
            poleDir.Normalize();

            float cosA = Mathf.Clamp((l1 * l1 + d * d - l2 * l2) / (2f * l1 * d), -1f, 1f);
            float sinA = Mathf.Sqrt(Mathf.Max(0f, 1f - cosA * cosA));
            Vector3 mid = a + (dir * cosA + poleDir * sinA) * l1;
            Vector3 endPos = a + dir * d;

            Vector3 curUpper = b0 - a;
            Vector3 newUpper = mid - a;
            if (curUpper.sqrMagnitude > 1e-10f && newUpper.sqrMagnitude > 1e-10f)
                upper.rotation = Quaternion.FromToRotation(curUpper, newUpper) * upper.rotation;
            Vector3 b1 = lower.position;
            Vector3 curLower = end.position - b1;
            Vector3 newLower = endPos - b1;
            if (curLower.sqrMagnitude > 1e-10f && newLower.sqrMagnitude > 1e-10f)
                lower.rotation = Quaternion.FromToRotation(curLower, newLower) * lower.rotation;
        }

        /// <summary>Rotates <paramref name="bone"/> about its pivot so the world direction <paramref name="from"/> points along <paramref name="to"/> (weighted).</summary>
        public static void Aim(Transform bone, Vector3 from, Vector3 to, float weight)
        {
            if (bone == null || weight <= 0f || from.sqrMagnitude < 1e-10f || to.sqrMagnitude < 1e-10f) return;
            var delta = Quaternion.FromToRotation(from, to);
            bone.rotation = Quaternion.Slerp(Quaternion.identity, delta, weight) * bone.rotation;
        }

        /// <summary>Applies a world-space rotation delta (weighted) about the bone's pivot.</summary>
        public static void RotateWorld(Transform bone, Quaternion delta, float weight = 1f)
        {
            if (bone == null || weight <= 0f) return;
            bone.rotation = (weight >= 1f ? delta : Quaternion.Slerp(Quaternion.identity, delta, weight)) * bone.rotation;
        }

        /// <summary>Signed angle-limited yaw/pitch toward a point, in the frame of <paramref name="basis"/> (degrees).</summary>
        public static Vector2 LookAngles(Quaternion basis, Vector3 from, Vector3 target, float maxYaw, float maxPitch)
        {
            Vector3 local = Quaternion.Inverse(basis) * (target - from);
            float yaw = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
            float pitch = -Mathf.Atan2(local.y, new Vector2(local.x, local.z).magnitude) * Mathf.Rad2Deg;
            // Behind the character: no neck-breaking turns, the eyes simply stay ahead.
            if (Mathf.Abs(yaw) > 120f) return Vector2.zero;
            return new Vector2(Mathf.Clamp(yaw, -maxYaw, maxYaw), Mathf.Clamp(pitch, -maxPitch, maxPitch));
        }
    }
}
