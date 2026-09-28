using System;
using System.Collections.Generic;
using BreathOfEclipse.Characters;
using BreathOfEclipse.Core;
using BreathOfEclipse.Data;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace BreathOfEclipse.CameraSystem
{
    /// <summary>
    /// Owns the gameplay camera: switches between first / second / third person, plays cinematic shots for
    /// ultimates and layers combat camera effects (shake, impulses, FOV punch, zoom in/out).
    /// </summary>
    [DefaultExecutionOrder(200)]
    public sealed class CameraRig : MonoBehaviour
    {
        private struct ZoomRequest
        {
            public float Multiplier;
            public float Start;
            public float End;
            public float BlendIn;
            public float BlendOut;
        }

        public static CameraRig Instance { get; private set; }

        public Camera Camera { get; private set; }
        public CameraMode Mode { get; private set; } = CameraMode.ThirdPerson;
        public bool CinematicActive { get; private set; }
        public PostProcessController Post { get; private set; }

        /// <summary>Yaw used for camera-relative movement.</summary>
        public float MovementYaw => _active != null ? _active.Yaw : transform.eulerAngles.y;
        public Vector3 PlanarForward => Quaternion.Euler(0f, MovementYaw, 0f) * Vector3.forward;
        public Vector3 PlanarRight => Quaternion.Euler(0f, MovementYaw, 0f) * Vector3.right;

        /// <summary>Returns the current lock-on target (null when not locked).</summary>
        public Func<Transform> LockTargetProvider;
        public Func<bool> LockTargetIsBossProvider;
        public Func<Vector3> PlayerVelocityProvider;
        public Func<bool> PlayerSprintingProvider;
        public Func<bool> PlayerAttackingProvider;

        /// <summary>Raised when second person loses its target and the rig falls back to third person.</summary>
        public event Action SecondPersonTargetLost;

        /// <summary>Diagnostics: current lock-on target seen by the camera (null when none).</summary>
        public Transform LockTarget => CurrentLockTarget();
        /// <summary>Diagnostics: distance from the camera to the followed player.</summary>
        public float DistanceToPlayer => _player != null ? Vector3.Distance(transform.position, _player.position + Vector3.up * 1.5f) : 0f;
        /// <summary>Diagnostics: third-person collision pulled the camera in this frame.</summary>
        public bool CollisionActive => Mode == CameraMode.ThirdPerson && !CinematicActive && Third.Obstructed;

        public readonly ThirdPersonCameraMode Third = new ThirdPersonCameraMode();
        public readonly FirstPersonCameraMode First = new FirstPersonCameraMode();
        public readonly SecondPersonCameraMode Second = new SecondPersonCameraMode();
        public readonly CinematicCombatCamera Cinematic = new CinematicCombatCamera();
        private readonly CameraShaker _shaker = new CameraShaker();
        private readonly List<ZoomRequest> _zooms = new List<ZoomRequest>();

        private ICameraMode _active;
        private Transform _player;
        private CharacterRig _playerRig;
        private float _playerScale = 1f;
        private CameraPose _current;
        private CameraPose _transitionFrom;
        private float _transitionStart = -10f;
        private float _transitionDuration = 0.35f;
        private float _fovPunch;
        private float _zoomSmoothed = 1f;
        private int _normalCullingMask;
        private float _cinematicEnd;

        public static CameraRig Create(string name = "Main Camera")
        {
            var go = new GameObject(name);
            go.tag = "MainCamera";
            var cam = go.AddComponent<Camera>();
            cam.nearClipPlane = 0.08f;
            cam.farClipPlane = 900f;
            cam.fieldOfView = 60f;
            cam.allowHDR = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.02f, 0.025f, 0.06f);
            go.AddComponent<AudioListener>();
            var data = cam.GetUniversalAdditionalCameraData();
            if (data != null)
            {
                data.renderPostProcessing = true;
                data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                data.antialiasingQuality = AntialiasingQuality.High;
                data.renderShadows = true;
                data.stopNaN = true;
                data.dithering = true;
            }
            var rig = go.AddComponent<CameraRig>();
            rig.Camera = cam;
            var post = new GameObject("PostProcess");
            post.transform.SetParent(go.transform, false);
            rig.Post = post.AddComponent<PostProcessController>();
            return rig;
        }

        private void Awake()
        {
            Instance = this;
            if (Camera == null) Camera = GetComponent<Camera>();
            _active = Third;
            _current = new CameraPose(transform.position, transform.rotation, 60f);
        }

        private void Start()
        {
            _normalCullingMask = Camera.cullingMask;
            if (InputReader.Instance != null) InputReader.Instance.CameraModePressed += CycleMode;
            SaveSystem.SettingsChanged += OnSettingsChanged;
            OnSettingsChanged(SaveSystem.Settings);
        }

        private void OnDestroy()
        {
            if (InputReader.Instance != null) InputReader.Instance.CameraModePressed -= CycleMode;
            SaveSystem.SettingsChanged -= OnSettingsChanged;
            if (Instance == this) Instance = null;
        }

        private void OnSettingsChanged(GameSettings s)
        {
            if (Camera == null) return;
            var data = Camera.GetUniversalAdditionalCameraData();
            if (data != null)
            {
                data.antialiasing = s.Quality == GraphicsQuality.Low ? AntialiasingMode.FastApproximateAntialiasing : AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                data.antialiasingQuality = s.Quality >= GraphicsQuality.High ? AntialiasingQuality.High : AntialiasingQuality.Medium;
            }
        }

        /// <summary>Attaches the camera to the player. Restores the last camera mode from settings.</summary>
        public void Bind(Transform player, CharacterRig rig)
        {
            _player = player;
            _playerRig = rig;
            _playerScale = rig != null ? rig.Scale : 1f;
            First.HeadBone = rig != null ? rig.Bone(RigBone.Head) : null;
            Vector3 behind = player.position - player.forward * 5f + Vector3.up * 2.5f;
            _current = new CameraPose(behind, Quaternion.LookRotation(player.position + Vector3.up * 1.4f - behind), SaveSystem.Settings.fieldOfView);
            Third.SetOrbit(player.eulerAngles.y, 14f);
            _active = Third;
            Mode = CameraMode.ThirdPerson;
            var saved = (CameraMode)SaveSystem.Settings.lastCameraMode;
            if (saved == CameraMode.FirstPerson) SetMode(CameraMode.FirstPerson, true);
            ApplyTransform(_current);
        }

        public void CycleMode()
        {
            if (CinematicActive) return;
            var next = Mode == CameraMode.ThirdPerson ? CameraMode.FirstPerson : Mode == CameraMode.FirstPerson ? CameraMode.SecondPerson : CameraMode.ThirdPerson;
            if (next == CameraMode.SecondPerson && CurrentLockTarget() == null)
            {
                GameEvents.Notify("Second person needs a locked-on enemy (Tab / Middle Mouse)");
                next = CameraMode.ThirdPerson;
            }
            SetMode(next);
        }

        public void SetMode(CameraMode mode, bool instant = false)
        {
            if (_player == null) return;
            var ctx = BuildContext(0f);
            Mode = mode;
            _active = mode == CameraMode.FirstPerson ? First : mode == CameraMode.SecondPerson ? (ICameraMode)Second : Third;
            _active.Enter(_current, ctx);
            BeginTransition(instant ? 0f : 0.35f);
            bool fp = mode == CameraMode.FirstPerson;
            if (_playerRig != null) _playerRig.SetFirstPersonHidden(fp, Layers.Player);
            Camera.cullingMask = fp ? _normalCullingMask & ~(1 << Layers.PlayerHidden) : _normalCullingMask;
            Camera.nearClipPlane = fp ? 0.03f : 0.08f;
            SaveSystem.Settings.lastCameraMode = mode == CameraMode.SecondPerson ? (int)CameraMode.ThirdPerson : (int)mode;
            GameEvents.RaiseCameraModeChanged(mode.ToString());
        }

        private void BeginTransition(float duration)
        {
            _transitionFrom = _current;
            _transitionStart = Time.unscaledTime;
            _transitionDuration = duration;
        }

        // ------------------------------------------------------------------ cinematic

        /// <summary>Starts (or continues) a cinematic shot. Returns false when cinematics are disabled in settings.</summary>
        public bool PlayCinematicShot(CinematicShot shot, float distance, float height, Transform focus, float duration)
        {
            if (shot == CinematicShot.None || _player == null) return false;
            if (SaveSystem.Settings.skipUltimateCinematics) return false;
            Cinematic.SetShot(shot, distance, height, _player, focus, _current, CinematicActive ? 0.22f : 0.3f);
            DevTelemetry.ReportCameraCue("cinematic", duration);
            if (!CinematicActive)
            {
                CinematicActive = true;
                if (_playerRig != null) _playerRig.SetFirstPersonHidden(false, Layers.Player);
                Camera.cullingMask = _normalCullingMask;
            }
            _active = Cinematic;
            _cinematicEnd = Time.unscaledTime + Mathf.Max(0.2f, duration);
            return true;
        }

        public void EndCinematic()
        {
            if (!CinematicActive) return;
            CinematicActive = false;
            Cinematic.Clear();
            if (Post != null) Post.SetCinematicDof(0f, 5f);
            // Return smoothly to the gameplay mode.
            var ctx = BuildContext(0f);
            if (Mode == CameraMode.SecondPerson && CurrentLockTarget() == null) Mode = CameraMode.ThirdPerson;
            _active = Mode == CameraMode.FirstPerson ? First : Mode == CameraMode.SecondPerson ? (ICameraMode)Second : Third;
            if (Mode == CameraMode.ThirdPerson && _player != null) Third.SetOrbit(_player.eulerAngles.y, 14f);
            else _active.Enter(_current, ctx);
            BeginTransition(0.55f);
            SetMode(Mode, false);
            BeginTransition(0.55f);
        }

        // ------------------------------------------------------------------ effects API

        public void Shake(float amount)
        {
            float scale = SaveSystem.Settings.cameraShake;
            if (Mode == CameraMode.FirstPerson && !CinematicActive) scale *= 0.35f;
            _shaker.AddTrauma(amount * scale);
            DevTelemetry.ReportCameraCue("shake", amount);
        }

        public void Impulse(Vector3 worldDirection, float strength)
        {
            float scale = SaveSystem.Settings.cameraShake;
            if (Mode == CameraMode.FirstPerson && !CinematicActive) scale *= 0.3f;
            _shaker.AddImpulse(worldDirection.normalized * strength * scale);
            DevTelemetry.ReportCameraCue("impulse", strength);
        }

        public void FovPunch(float degrees)
        {
            _fovPunch = Mathf.Abs(degrees) > Mathf.Abs(_fovPunch) ? degrees : _fovPunch;
            DevTelemetry.ReportCameraCue("fov", degrees);
        }

        /// <summary>Distance multiplier over time: &lt;1 moves closer (parry, finisher), &gt;1 wider (giant attacks).</summary>
        public void Zoom(float multiplier, float duration, float blendIn = 0.08f, float blendOut = 0.35f)
        {
            if (multiplier <= 0f || Mathf.Approximately(multiplier, 1f) || duration <= 0f) return;
            float now = Time.unscaledTime;
            DevTelemetry.ReportCameraCue("zoom", multiplier);
            _zooms.Add(new ZoomRequest { Multiplier = multiplier, Start = now, End = now + duration, BlendIn = blendIn, BlendOut = blendOut });
        }

        public void RecenterBehindPlayer()
        {
            if (Mode == CameraMode.ThirdPerson) Third.RequestRecenter();
        }

        // ------------------------------------------------------------------ update

        private Transform CurrentLockTarget() => LockTargetProvider?.Invoke();

        private CameraContext BuildContext(float dt)
        {
            float zoom = EvaluateZoom();
            return new CameraContext
            {
                Player = _player,
                PlayerScale = _playerScale,
                PlayerVelocity = PlayerVelocityProvider != null ? PlayerVelocityProvider() : Vector3.zero,
                PlayerSprinting = PlayerSprintingProvider != null && PlayerSprintingProvider(),
                PlayerAttacking = PlayerAttackingProvider != null && PlayerAttackingProvider(),
                LockTarget = CurrentLockTarget(),
                LockTargetIsBoss = LockTargetIsBossProvider != null && LockTargetIsBossProvider(),
                LookDelta = InputReader.Instance != null ? InputReader.Instance.LookDelta : Vector2.zero,
                LastLookTime = InputReader.Instance != null ? InputReader.Instance.LastLookTime : 0f,
                BaseFov = SaveSystem.Settings.fieldOfView,
                DistanceMultiplier = zoom,
                AutoRecenter = SaveSystem.Settings.autoRecenterCamera,
                DeltaTime = dt
            };
        }

        private float EvaluateZoom()
        {
            float now = Time.unscaledTime;
            float target = 1f;
            for (int i = _zooms.Count - 1; i >= 0; i--)
            {
                var z = _zooms[i];
                if (now > z.End + z.BlendOut)
                {
                    _zooms.RemoveAt(i);
                    continue;
                }
                float w = 1f;
                if (now < z.Start + z.BlendIn) w = (now - z.Start) / Mathf.Max(0.001f, z.BlendIn);
                else if (now > z.End) w = 1f - (now - z.End) / Mathf.Max(0.001f, z.BlendOut);
                float m = Mathf.Lerp(1f, z.Multiplier, Mathf.SmoothStep(0f, 1f, w));
                // Closest request wins when zooming in, widest when zooming out.
                if (m < 1f) target = Mathf.Min(target, m);
                else if (target >= 1f) target = Mathf.Max(target, m);
            }
            return target;
        }

        private void LateUpdate()
        {
            if (_player == null || Camera == null) return;
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);

            if (CinematicActive && Time.unscaledTime > _cinematicEnd + 1.5f) EndCinematic();

            var ctx = BuildContext(dt);
            _zoomSmoothed = Mathf.Lerp(_zoomSmoothed, ctx.DistanceMultiplier, 1f - Mathf.Exp(-10f * dt));
            ctx.DistanceMultiplier = _zoomSmoothed;

            // Second person without a target falls back to third person automatically.
            if (!CinematicActive && Mode == CameraMode.SecondPerson && ctx.LockTarget == null)
            {
                SetMode(CameraMode.ThirdPerson);
                GameEvents.Notify("Target lost: back to third person");
                SecondPersonTargetLost?.Invoke();
            }

            CameraPose pose = _active.Evaluate(ctx);
            float tt = _transitionDuration <= 0f ? 1f : (Time.unscaledTime - _transitionStart) / _transitionDuration;
            if (tt < 1f) pose = CameraPose.Lerp(_transitionFrom, pose, Mathf.SmoothStep(0f, 1f, tt));

            _fovPunch = Mathf.Lerp(_fovPunch, 0f, 1f - Mathf.Exp(-7f * dt));
            pose.Fov += _fovPunch;
            _current = pose;
            ApplyTransform(pose);
        }

        private void ApplyTransform(CameraPose pose)
        {
            _shaker.Evaluate(Mathf.Min(Time.unscaledDeltaTime, 0.1f), 1f, out var posOffset, out var rotOffset, pose.Rotation);
            transform.SetPositionAndRotation(pose.Position + posOffset, pose.Rotation * rotOffset);
            Camera.fieldOfView = Mathf.Clamp(pose.Fov, 30f, 110f);
        }
    }

    /// <summary>Static shortcuts so gameplay code can request camera effects without holding references.</summary>
    public static class CameraFX
    {
        public static void Shake(float amount)
        {
            if (CameraRig.Instance != null) CameraRig.Instance.Shake(amount);
        }

        public static void Impulse(Vector3 direction, float strength)
        {
            if (CameraRig.Instance != null) CameraRig.Instance.Impulse(direction, strength);
        }

        public static void FovPunch(float degrees)
        {
            if (CameraRig.Instance != null) CameraRig.Instance.FovPunch(degrees);
        }

        public static void Zoom(float multiplier, float duration, float blendIn = 0.08f, float blendOut = 0.35f)
        {
            if (CameraRig.Instance != null) CameraRig.Instance.Zoom(multiplier, duration, blendIn, blendOut);
        }

        public static PostProcessController Post => CameraRig.Instance != null ? CameraRig.Instance.Post : null;
    }
}
