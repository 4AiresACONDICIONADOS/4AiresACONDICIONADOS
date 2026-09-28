using BreathOfEclipse.Characters;
using BreathOfEclipse.Combat;
using BreathOfEclipse.Core;
using BreathOfEclipse.Data;
using BreathOfEclipse.VFX;
using UnityEngine;

namespace BreathOfEclipse.AI
{
    /// <summary>Builds enemies (procedural demon rigs + AI) from <see cref="EnemyData"/>.</summary>
    public static class EnemyFactory
    {
        public static EnemyController Spawn(EnemyData data, Vector3 position, Quaternion rotation, bool withPortal = true)
        {
            var go = new GameObject(data.displayName);
            go.layer = Layers.Enemy;
            go.transform.SetPositionAndRotation(position, rotation);

            var profile = data.archetype == EnemyArchetype.HollowOni ? RigProfile.HollowOni() : RigProfile.Nightspawn();
            profile.scale = data.scale;
            profile.skin = data.bodyColor;
            profile.secondary = data.archetype == EnemyArchetype.HollowOni ? profile.secondary : data.accentColor;
            profile.eyes = data.eyeColor;

            var cc = go.AddComponent<CharacterController>();
            cc.radius = 0.38f * data.scale;
            cc.height = 1.8f * data.scale;
            cc.center = new Vector3(0f, 0.9f * data.scale, 0f);
            cc.skinWidth = 0.02f;
            cc.stepOffset = Mathf.Min(0.4f * data.scale, cc.height * 0.3f);
            cc.slopeLimit = 50f;

            var rig = go.AddComponent<CharacterRig>();
            rig.Build(profile, Layers.Enemy);
            var anim = go.AddComponent<ProceduralAnimator>();
            anim.Initialize(rig);
            var damageable = go.AddComponent<Damageable>();
            damageable.Configure(Team.Enemy, data.maxHealth, rig.LockOnPoint, data.defense, data.weakness, data.resistance);
            var motor = go.AddComponent<EnemyMotor>();
            motor.Initialize(cc);
            var enemy = go.AddComponent<EnemyController>();
            enemy.Initialize(data, rig, anim, motor, damageable);
            if (data.isBoss)
            {
                var boss = go.AddComponent<BossController>();
                boss.Initialize(enemy);
            }
            if (withPortal) VFXLibrary.Spawn("spawn_portal", position, Quaternion.identity, data.scale, Element.Dark);
            return enemy;
        }
    }

    /// <summary>
    /// Training dummy: immortal target that wobbles on hits and regenerates (for testing combos, damage numbers
    /// and techniques in 03_CombatTest). Lock-on compatible.
    /// </summary>
    public sealed class TrainingDummy : MonoBehaviour, ITargetable, IHitReactor
    {
        public Transform LockOnPoint { get; private set; }
        public bool IsTargetable => isActiveAndEnabled;
        public bool IsBoss => false;
        public IDamageable Damageable => _damageable;

        private Damageable _damageable;
        private Transform _body;
        private float _wobble;
        private Vector3 _axis = Vector3.right;
        private float _regenTimer;

        public static TrainingDummy Create(Vector3 position, Quaternion rotation)
        {
            var go = new GameObject("TrainingDummy");
            go.layer = Layers.Enemy;
            go.transform.SetPositionAndRotation(position, rotation);
            var col = go.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0f, 1f, 0f);
            col.height = 2f;
            col.radius = 0.4f;
            var dummy = go.AddComponent<TrainingDummy>();
            dummy.Build();
            return dummy;
        }

        private void Build()
        {
            var wood = Rendering.MaterialFactory.Toon(new Color(0.45f, 0.3f, 0.18f), 1.2f, true, null, 0.5f, 0.3f, 0f, Rendering.ProceduralTextures.WoodDetail);
            var straw = Rendering.MaterialFactory.Toon(new Color(0.82f, 0.7f, 0.38f), 1.2f, true);
            var red = Rendering.MaterialFactory.Toon(new Color(0.75f, 0.12f, 0.12f), 0.8f, true);
            var white = Rendering.MaterialFactory.Toon(new Color(0.92f, 0.9f, 0.85f), 0.8f, true);
            Rendering.ProceduralMeshes.CreatePart("Base", PrimitiveType.Cylinder, wood, transform, new Vector3(0f, 0.08f, 0f), Vector3.zero, new Vector3(0.9f, 0.08f, 0.9f));
            _body = new GameObject("Body").transform;
            _body.SetParent(transform, false);
            Rendering.ProceduralMeshes.CreatePart("Post", PrimitiveType.Cylinder, wood, _body, new Vector3(0f, 1f, 0f), Vector3.zero, new Vector3(0.14f, 1f, 0.14f));
            Rendering.ProceduralMeshes.CreatePart("Torso", PrimitiveType.Capsule, straw, _body, new Vector3(0f, 1.25f, 0f), Vector3.zero, new Vector3(0.55f, 0.45f, 0.4f));
            Rendering.ProceduralMeshes.CreatePart("Arms", PrimitiveType.Cylinder, wood, _body, new Vector3(0f, 1.45f, 0f), new Vector3(0f, 0f, 90f), new Vector3(0.1f, 0.6f, 0.1f));
            Rendering.ProceduralMeshes.CreatePart("Head", PrimitiveType.Sphere, straw, _body, new Vector3(0f, 1.85f, 0f), Vector3.zero, Vector3.one * 0.36f);
            Rendering.ProceduralMeshes.CreatePart("TargetOuter", PrimitiveType.Cylinder, white, _body, new Vector3(0f, 1.3f, 0.2f), new Vector3(90f, 0f, 0f), new Vector3(0.36f, 0.01f, 0.36f));
            Rendering.ProceduralMeshes.CreatePart("TargetInner", PrimitiveType.Cylinder, red, _body, new Vector3(0f, 1.3f, 0.215f), new Vector3(90f, 0f, 0f), new Vector3(0.18f, 0.01f, 0.18f));
            LockOnPoint = new GameObject("LockOn").transform;
            LockOnPoint.SetParent(_body, false);
            LockOnPoint.localPosition = new Vector3(0f, 1.35f, 0f);

            _damageable = gameObject.AddComponent<Damageable>();
            _damageable.Configure(Team.Enemy, 5000f, LockOnPoint);
            _damageable.Immortal = true;
            _damageable.AddReactor(this);
        }

        private void OnEnable() => TargetRegistry.Register(this);
        private void OnDisable() => TargetRegistry.Unregister(this);

        public void OnHitResolved(HitData hit, HitResult result)
        {
            if (!result.Landed) return;
            _wobble = Mathf.Min(1f, _wobble + (hit.Reaction >= HitReaction.Heavy ? 1f : 0.5f));
            _axis = Vector3.Cross(Vector3.up, hit.Direction.sqrMagnitude > 0.01f ? hit.Direction : Vector3.forward);
            _regenTimer = 3f;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            _wobble = Mathf.MoveTowards(_wobble, 0f, dt * 1.5f);
            float angle = Mathf.Sin(Time.time * 18f) * 14f * _wobble;
            _body.localRotation = Quaternion.AngleAxis(angle, transform.InverseTransformDirection(_axis.sqrMagnitude > 0.001f ? _axis : Vector3.right));
            _regenTimer -= dt;
            if (_regenTimer <= 0f && _damageable.Health.Current < _damageable.Health.Max) _damageable.Health.Heal(_damageable.Health.Max);
        }
    }
}
