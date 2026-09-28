using System;
using UnityEngine;

namespace BreathOfEclipse.Characters
{
    public enum Ease
    {
        Linear = 0,
        In = 1,
        Out = 2,
        InOut = 3,
        /// <summary>Very front-loaded: the blade covers most of the arc in the first frames (anime "smear" timing).</summary>
        Snap = 4,
        /// <summary>Slight pull back before moving.</summary>
        Anticipate = 5
    }

    /// <summary>
    /// A full-body pose in rig space (x right, y up, z forward, origin at the feet, 1.8 m reference character).
    /// The right hand + blade direction drive the sword; everything else is solved with IK.
    /// </summary>
    [Serializable]
    public struct MotionPose
    {
        public Vector3 handR;
        public Vector3 bladeDir;
        public Vector3 bladeEdge;
        public Vector3 handL;
        [Range(0f, 1f)] public float twoHand;
        public Vector3 elbowHintR;
        public Vector3 elbowHintL;
        public Vector3 hips;
        public Vector3 hipsEuler;
        public Vector3 spineEuler;
        public Vector3 chestEuler;
        public Vector3 headEuler;
        public Vector3 footL;
        public Vector3 footR;
        public Vector3 kneeHint;
        public Vector3 visualEuler;
        public Vector3 visualOffset;

        /// <summary>Pivot used for arc interpolation of the hands (upper chest).</summary>
        public static readonly Vector3 ArcPivot = new Vector3(0.05f, 1.3f, 0.05f);

        public static MotionPose Lerp(in MotionPose a, in MotionPose b, float t)
        {
            var p = new MotionPose
            {
                handR = ArcLerp(a.handR, b.handR, t),
                bladeDir = SafeSlerp(a.bladeDir, b.bladeDir, t, Vector3.forward),
                bladeEdge = SafeSlerp(a.bladeEdge, b.bladeEdge, t, Vector3.up),
                handL = ArcLerp(a.handL, b.handL, t),
                twoHand = Mathf.Lerp(a.twoHand, b.twoHand, t),
                elbowHintR = Vector3.Lerp(a.elbowHintR, b.elbowHintR, t),
                elbowHintL = Vector3.Lerp(a.elbowHintL, b.elbowHintL, t),
                hips = Vector3.Lerp(a.hips, b.hips, t),
                hipsEuler = Vector3.Lerp(a.hipsEuler, b.hipsEuler, t),
                spineEuler = Vector3.Lerp(a.spineEuler, b.spineEuler, t),
                chestEuler = Vector3.Lerp(a.chestEuler, b.chestEuler, t),
                headEuler = Vector3.Lerp(a.headEuler, b.headEuler, t),
                footL = Vector3.Lerp(a.footL, b.footL, t),
                footR = Vector3.Lerp(a.footR, b.footR, t),
                kneeHint = Vector3.Lerp(a.kneeHint, b.kneeHint, t),
                visualEuler = Vector3.Lerp(a.visualEuler, b.visualEuler, t),
                visualOffset = Vector3.Lerp(a.visualOffset, b.visualOffset, t)
            };
            return p;
        }

        /// <summary>Interpolates positions along an arc around the chest so swings curve naturally.</summary>
        public static Vector3 ArcLerp(Vector3 a, Vector3 b, float t)
        {
            Vector3 va = a - ArcPivot, vb = b - ArcPivot;
            float la = va.magnitude, lb = vb.magnitude;
            if (la < 1e-4f || lb < 1e-4f) return Vector3.Lerp(a, b, t);
            Vector3 dir = SafeSlerp(va / la, vb / lb, t, Vector3.forward);
            return ArcPivot + dir * Mathf.Lerp(la, lb, t);
        }

        public static Vector3 SafeSlerp(Vector3 a, Vector3 b, float t, Vector3 fallback)
        {
            if (a.sqrMagnitude < 1e-6f) a = fallback;
            if (b.sqrMagnitude < 1e-6f) b = fallback;
            a.Normalize();
            b.Normalize();
            // Opposite vectors: slerp is undefined, go through the fallback.
            if (Vector3.Dot(a, b) < -0.995f)
            {
                return t < 0.5f ? Vector3.Slerp(a, fallback, t * 2f) : Vector3.Slerp(fallback, b, (t - 0.5f) * 2f);
            }
            return Vector3.Slerp(a, b, t);
        }

        public static float Evaluate(Ease ease, float t)
        {
            t = Mathf.Clamp01(t);
            switch (ease)
            {
                case Ease.In: return t * t * t;
                case Ease.Out: return 1f - (1f - t) * (1f - t) * (1f - t);
                case Ease.InOut: return t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) * 0.5f;
                case Ease.Snap: return 1f - Mathf.Pow(1f - t, 5f);
                case Ease.Anticipate:
                {
                    const float s = 1.5f;
                    return t * t * ((s + 1f) * t - s);
                }
                default: return t;
            }
        }

        /// <summary>Returns a copy with an additive offset of body angles (used by hit reactions and look-at).</summary>
        public MotionPose WithBodyOffset(Vector3 spine, Vector3 chest, Vector3 head)
        {
            var p = this;
            p.spineEuler += spine;
            p.chestEuler += chest;
            p.headEuler += head;
            return p;
        }
    }

    [Serializable]
    public struct MotionKey
    {
        /// <summary>Normalized time (0..1) inside the clip.</summary>
        public float time;
        public MotionPose pose;
        public Ease ease;
        /// <summary>Orients the cutting edge along the swing direction for the segment ending at this key.</summary>
        public bool autoEdge;

        public MotionKey(float time, MotionPose pose, Ease ease = Ease.InOut, bool autoEdge = false)
        {
            this.time = time;
            this.pose = pose;
            this.ease = ease;
            this.autoEdge = autoEdge;
        }
    }

    /// <summary>A sequence of poses. Attacks interpret the first key as the windup pose and the rest as the strike.</summary>
    public sealed class MotionClip
    {
        public string Id;
        public MotionKey[] Keys;
        /// <summary>For looping clips (e.g. idle breathing variations). Unused by attacks.</summary>
        public bool Loop;

        public MotionClip(string id, params MotionKey[] keys)
        {
            Id = id;
            Keys = keys;
        }
    }
}
