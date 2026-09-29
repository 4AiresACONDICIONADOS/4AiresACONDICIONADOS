using UnityEngine;

namespace BreathOfEclipse.Characters
{
    /// <summary>
    /// Lightweight spring bones for secondary motion (ponytail, hair locks, cloth tails): each joint is a verlet
    /// point pulled back toward its rest pose, with damping, gravity, sphere colliders and fixed segment lengths.
    /// The first joint is anchored to its parent (head, hips…); joints are re-aimed every frame after animation.
    /// </summary>
    [DefaultExecutionOrder(80)]
    public sealed class SpringBoneChain : MonoBehaviour
    {
        public struct Collider
        {
            public Transform Bone;
            public Vector3 Offset;
            public float Radius;
        }

        [Range(0f, 1f)] public float stiffness = 0.12f;
        [Range(0f, 1f)] public float damping = 0.16f;
        public float gravity = 2.5f;

        private Transform[] _joints;
        private Vector3[] _restLocalPos;
        private Quaternion[] _restLocalRot;
        private Vector3[] _pos, _prev;
        private float[] _len;
        private Collider[] _colliders = new Collider[0];
        private float _scale = 1f;

        /// <summary><paramref name="joints"/>: anchor first, tip last (the tip is only aimed at).</summary>
        public void Initialize(Transform[] joints, float scale, params Collider[] colliders)
        {
            _joints = joints;
            _scale = Mathf.Max(0.01f, scale);
            _colliders = colliders ?? new Collider[0];
            int n = joints.Length;
            _restLocalPos = new Vector3[n];
            _restLocalRot = new Quaternion[n];
            _pos = new Vector3[n];
            _prev = new Vector3[n];
            _len = new float[n];
            for (int i = 0; i < n; i++)
            {
                _restLocalPos[i] = joints[i].localPosition;
                _restLocalRot[i] = joints[i].localRotation;
                _pos[i] = _prev[i] = joints[i].position;
                _len[i] = i > 0 ? Vector3.Distance(joints[i].position, joints[i - 1].position) : 0f;
            }
        }

        public void ResetPose()
        {
            if (_joints == null) return;
            for (int i = 0; i < _joints.Length; i++)
            {
                _joints[i].localRotation = _restLocalRot[i];
                _pos[i] = _prev[i] = _joints[i].position;
            }
        }

        private void LateUpdate()
        {
            if (_joints == null || _joints.Length < 2) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return; // hit stop: hold the pose
            dt = Mathf.Min(dt, 0.05f);
            for (int i = 0; i < _joints.Length - 1; i++) _joints[i].localRotation = _restLocalRot[i];

            Vector3 g = Vector3.down * (gravity * _scale * dt * dt);
            for (int i = 1; i < _joints.Length; i++)
            {
                var parent = _joints[i - 1];
                Vector3 rest = parent.TransformPoint(_restLocalPos[i]);
                // Teleports / flash steps: snap instead of whipping across the map.
                if ((_pos[i] - rest).sqrMagnitude > 1.5f * _scale * 1.5f * _scale)
                {
                    _pos[i] = _prev[i] = rest;
                }
                Vector3 velocity = (_pos[i] - _prev[i]) * (1f - damping);
                _prev[i] = _pos[i];
                Vector3 p = _pos[i] + velocity + (rest - _pos[i]) * stiffness + g;
                for (int c = 0; c < _colliders.Length; c++)
                {
                    var col = _colliders[c];
                    if (col.Bone == null) continue;
                    Vector3 centre = col.Bone.TransformPoint(col.Offset);
                    Vector3 d = p - centre;
                    float r = col.Radius * _scale;
                    if (d.sqrMagnitude < r * r && d.sqrMagnitude > 1e-10f) p = centre + d.normalized * r;
                }
                Vector3 from = parent.position;
                Vector3 dir = p - from;
                if (dir.sqrMagnitude < 1e-10f) dir = rest - from;
                p = from + dir.normalized * _len[i];
                _pos[i] = p;
                Vector3 current = _joints[i].position - from;
                if (current.sqrMagnitude > 1e-10f)
                    parent.rotation = Quaternion.FromToRotation(current, p - from) * parent.rotation;
            }
        }
    }
}
