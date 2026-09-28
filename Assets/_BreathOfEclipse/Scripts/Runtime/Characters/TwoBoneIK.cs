using UnityEngine;

namespace BreathOfEclipse.Characters
{
    /// <summary>Analytic two-bone IK for bones whose local +Z points toward their child.</summary>
    public static class TwoBoneIK
    {
        /// <summary>
        /// Rotates <paramref name="upper"/> and <paramref name="lower"/> so the end of the chain reaches <paramref name="target"/>.
        /// </summary>
        /// <param name="pole">World point the middle joint (elbow/knee) should bend toward.</param>
        public static void Solve(Transform upper, Transform lower, Transform end, Vector3 target, Vector3 pole)
        {
            Vector3 a = upper.position;
            Vector3 b0 = lower.position;
            Vector3 c0 = end.position;
            float l1 = Vector3.Distance(a, b0);
            float l2 = Vector3.Distance(b0, c0);
            if (l1 < 1e-5f || l2 < 1e-5f) return;

            Vector3 toTarget = target - a;
            float d = toTarget.magnitude;
            if (d < 1e-4f) return;
            Vector3 dir = toTarget / d;
            d = Mathf.Clamp(d, Mathf.Abs(l1 - l2) + 0.001f, l1 + l2 - 0.0005f);

            Vector3 poleDir = Vector3.ProjectOnPlane(pole - a, dir);
            if (poleDir.sqrMagnitude < 1e-6f)
            {
                poleDir = Vector3.ProjectOnPlane(Vector3.down, dir);
                if (poleDir.sqrMagnitude < 1e-6f) poleDir = Vector3.ProjectOnPlane(Vector3.forward, dir);
            }
            poleDir.Normalize();

            float cosA = Mathf.Clamp((l1 * l1 + d * d - l2 * l2) / (2f * l1 * d), -1f, 1f);
            float sinA = Mathf.Sqrt(1f - cosA * cosA);
            Vector3 mid = a + (dir * cosA + poleDir * sinA) * l1;
            Vector3 endPos = a + dir * d;

            Vector3 planeNormal = Vector3.Cross(dir, poleDir);
            if (planeNormal.sqrMagnitude < 1e-6f) planeNormal = Vector3.up;

            Vector3 upperDir = mid - a;
            Vector3 lowerDir = endPos - mid;
            if (upperDir.sqrMagnitude > 1e-8f) upper.rotation = Quaternion.LookRotation(upperDir, planeNormal);
            if (lowerDir.sqrMagnitude > 1e-8f) lower.rotation = Quaternion.LookRotation(lowerDir, planeNormal);
        }
    }
}
