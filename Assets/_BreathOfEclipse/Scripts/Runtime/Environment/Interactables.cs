using System;
using BreathOfEclipse.AI;
using BreathOfEclipse.Audio;
using BreathOfEclipse.Combat;
using BreathOfEclipse.Core;
using BreathOfEclipse.Player;
using BreathOfEclipse.Rendering;
using BreathOfEclipse.VFX;
using UnityEngine;

namespace BreathOfEclipse.Environment
{
    /// <summary>Generic interactable stone with a glowing crystal; runs an action when the player presses E.</summary>
    public sealed class ShrineInteractable : MonoBehaviour, IInteractable
    {
        public string PromptText = "Interact";
        public Func<bool> Available = () => true;
        public Action<PlayerController> OnInteract;
        public float Cooldown = 1f;
        private float _ready;

        public string Prompt => PromptText;
        public Vector3 Position => transform.position;
        public bool CanInteract => Time.time >= _ready && (Available == null || Available());

        public void Interact(PlayerController player)
        {
            _ready = Time.time + Cooldown;
            OnInteract?.Invoke(player);
        }

        private void OnEnable() => InteractableRegistry.Register(this);
        private void OnDisable() => InteractableRegistry.Unregister(this);

        public static ShrineInteractable Create(Transform parent, Vector3 position, string prompt, Color crystal, Action<PlayerController> action)
        {
            var root = new GameObject("Shrine_" + prompt);
            root.transform.SetParent(parent, false);
            root.transform.position = position;
            var stone = MaterialFactory.Toon(new Color(0.32f, 0.33f, 0.38f), 1f, false, null, 0.5f, 0.2f, 0f, ProceduralTextures.StoneDetail, 1f);
            var glow = MaterialFactory.Toon(crystal, 0f, false, crystal * 2.5f);
            ProceduralMeshes.CreatePart("Base", PrimitiveType.Cylinder, stone, root.transform, new Vector3(0f, 0.2f, 0f), Vector3.zero, new Vector3(1.2f, 0.2f, 1.2f));
            ProceduralMeshes.CreatePart("Pillar", PrimitiveType.Cube, stone, root.transform, new Vector3(0f, 0.9f, 0f), new Vector3(0f, 45f, 0f), new Vector3(0.5f, 1.2f, 0.5f));
            var crystalGo = ProceduralMeshes.CreatePart("Crystal", ProceduralMeshes.Cone(6), glow, root.transform, new Vector3(0f, 1.6f, 0f), Vector3.zero, new Vector3(0.4f, 0.7f, 0.4f));
            crystalGo.AddComponent<Spinner>().Speed = 45f;
            var l = new GameObject("Light").AddComponent<Light>();
            l.transform.SetParent(root.transform, false);
            l.transform.localPosition = new Vector3(0f, 1.9f, 0f);
            l.color = crystal;
            l.range = 5f;
            l.intensity = 1.5f;
            var col = root.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.8f, 0f);
            col.size = new Vector3(0.9f, 1.6f, 0.9f);
            var shrine = root.AddComponent<ShrineInteractable>();
            shrine.PromptText = prompt;
            shrine.OnInteract = action;
            return shrine;
        }
    }

    /// <summary>Slow rotation for decorative objects.</summary>
    public sealed class Spinner : MonoBehaviour
    {
        public float Speed = 30f;
        private void Update() => transform.Rotate(0f, Speed * Time.deltaTime, 0f, Space.World);
    }

    /// <summary>Resting lantern: heals and becomes the respawn point.</summary>
    public sealed class Checkpoint : MonoBehaviour, IInteractable
    {
        public string Prompt => "Rest at the lantern (heal & set respawn)";
        public Vector3 Position => transform.position;
        public bool CanInteract => true;

        public void Interact(PlayerController player)
        {
            player.SetCheckpoint(transform.position + transform.forward * 1.5f, transform.rotation);
            player.Damageable.Health.Revive(1f);
            player.Stats.Stamina.Refill();
            VFXLibrary.Spawn("perfect_dodge", transform.position, Quaternion.identity, 0.6f);
            Sfx.Play("breath_full", transform.position, 0.7f);
            GameEvents.Notify("Checkpoint set. Health restored.");
        }

        private void OnEnable() => InteractableRegistry.Register(this);
        private void OnDisable() => InteractableRegistry.Unregister(this);
    }

    /// <summary>Starts the boss encounter when the player enters the arena.</summary>
    public sealed class BossArenaTrigger : MonoBehaviour
    {
        public BossController Boss;
        public float Radius = 14f;
        private bool _triggered;

        private void Update()
        {
            if (_triggered || Boss == null) return;
            var pc = PlayerController.Instance;
            if (pc == null) return;
            Vector3 d = pc.transform.position - transform.position;
            d.y = 0f;
            if (d.magnitude > Radius) return;
            _triggered = true;
            Boss.StartEncounter();
        }
    }

    /// <summary>Respawns a group of enemies around a point when they are all dead (training waves).</summary>
    public sealed class WaveSpawner : MonoBehaviour
    {
        public Data.EnemyData Enemy;
        public int Count = 3;
        public float Radius = 4f;
        private readonly System.Collections.Generic.List<EnemyController> _alive = new System.Collections.Generic.List<EnemyController>();

        public int Alive
        {
            get
            {
                _alive.RemoveAll(e => e == null || !e.IsAlive);
                return _alive.Count;
            }
        }

        public void SpawnWave()
        {
            if (Enemy == null) return;
            for (int i = 0; i < Count; i++)
            {
                float a = i / (float)Count * Mathf.PI * 2f;
                Vector3 p = transform.position + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * Radius;
                HitQuery.GroundPoint(p + Vector3.up * 5f, out var ground, out _, 12f);
                var e = EnemyFactory.Spawn(Enemy, ground + Vector3.up * 0.05f, Quaternion.LookRotation(transform.position - p));
                e.Alert();
                _alive.Add(e);
            }
        }
    }
}
