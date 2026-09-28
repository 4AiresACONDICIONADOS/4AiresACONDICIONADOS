using UnityEngine;

namespace BreathOfEclipse.CameraSystem
{
    /// <summary>
    /// Trauma-based shake (Perlin noise, squared falloff) plus a spring "impulse" for directional kicks.
    /// Runs on unscaled time so impact frames still shake during hit stop.
    /// </summary>
    public sealed class CameraShaker
    {
        public float MaxOffset = 0.28f;
        public float MaxAngle = 3.2f;
        public float Frequency = 22f;
        public float Decay = 1.7f;
        public float Stiffness = 170f;
        public float Damping = 16f;

        private float _trauma;
        private float _seed = Random.value * 100f;
        private Vector3 _impulseOffset;
        private Vector3 _impulseVelocity;

        public float Trauma => _trauma;

        public void AddTrauma(float amount) => _trauma = Mathf.Clamp01(_trauma + amount);

        public void AddImpulse(Vector3 worldVelocity) => _impulseVelocity += worldVelocity;

        public void Clear()
        {
            _trauma = 0f;
            _impulseOffset = Vector3.zero;
            _impulseVelocity = Vector3.zero;
        }

        /// <summary>Returns (position offset, rotation offset) for this frame.</summary>
        public void Evaluate(float dt, float intensity, out Vector3 positionOffset, out Quaternion rotationOffset, Quaternion cameraRotation)
        {
            _trauma = Mathf.Max(0f, _trauma - Decay * dt);
            float shake = _trauma * _trauma * intensity;
            float t = Time.unscaledTime * Frequency;
            float nx = Mathf.PerlinNoise(_seed, t) * 2f - 1f;
            float ny = Mathf.PerlinNoise(_seed + 11.3f, t) * 2f - 1f;
            float nz = Mathf.PerlinNoise(_seed + 27.1f, t) * 2f - 1f;
            Vector3 local = new Vector3(nx, ny, nz * 0.3f) * (MaxOffset * shake);
            rotationOffset = Quaternion.Euler(ny * MaxAngle * shake, nx * MaxAngle * shake, nz * MaxAngle * 1.4f * shake);

            // Critically-damped-ish spring for impulses.
            Vector3 accel = -Stiffness * _impulseOffset - Damping * _impulseVelocity;
            _impulseVelocity += accel * dt;
            _impulseOffset += _impulseVelocity * dt;
            positionOffset = cameraRotation * local + _impulseOffset * intensity;
        }
    }
}
