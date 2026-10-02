using System;
using System.Collections.Generic;
using BreathOfEclipse.Audio;
using BreathOfEclipse.Core;
using BreathOfEclipse.Player;
using BreathOfEclipse.Rendering;
using BreathOfEclipse.VFX;
using UnityEngine;
using L = BreathOfEclipse.World.FrontierRegionLayout;

namespace BreathOfEclipse.World
{
    /// <summary>
    /// A shoji door that slides open: villagers open it to go in and out (it closes behind them); the player can
    /// open and close it too. Houses are not enterable — the doorway shows the dark (warm at night) interior.
    /// </summary>
    public sealed class SlidingDoor : MonoBehaviour, IInteractable
    {
        private static readonly Dictionary<string, SlidingDoor> ByPlace = new Dictionary<string, SlidingDoor>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => ByPlace.Clear();

        public static SlidingDoor For(string placeId) => placeId != null && ByPlace.TryGetValue(placeId, out var d) && d != null ? d : null;

        public string PlaceId;
        public Vector3 SlideWorld;
        public bool IsOpen => _target > 0.5f;

        private Vector3 _closed;
        private float _open, _target, _closeAt = -1f;
        private bool _playerOpened;

        public string Prompt => IsOpen ? "Close the door" : "Open the door";
        public Vector3 Position => transform.position - Vector3.up * 0.6f;
        public bool CanInteract => true;

        private void Awake() => _closed = transform.position;

        private void OnEnable()
        {
            if (!string.IsNullOrEmpty(PlaceId)) ByPlace[PlaceId] = this;
            InteractableRegistry.Register(this);
        }

        private void OnDisable()
        {
            if (!string.IsNullOrEmpty(PlaceId) && ByPlace.TryGetValue(PlaceId, out var d) && d == this) ByPlace.Remove(PlaceId);
            InteractableRegistry.Unregister(this);
        }

        public void Interact(PlayerController player)
        {
            _playerOpened = !IsOpen;
            Set(!IsOpen, -1f);
        }

        /// <summary>Someone walks through: open, then close after <paramref name="seconds"/> (unless the player opened it).</summary>
        public void OpenBriefly(float seconds)
        {
            if (_playerOpened && IsOpen) return;
            Set(true, Time.time + seconds);
        }

        private void Set(bool open, float closeAt)
        {
            if ((_target > 0.5f) != open)
            {
                var pc = PlayerController.Instance;
                if (pc != null && (pc.transform.position - transform.position).sqrMagnitude < 30f * 30f) Sfx.Play("door_slide", transform.position, 0.5f);
            }
            _target = open ? 1f : 0f;
            _closeAt = closeAt;
        }

        private void Update()
        {
            if (_closeAt > 0f && Time.time >= _closeAt)
            {
                _closeAt = -1f;
                Set(false, -1f);
            }
            if (Mathf.Approximately(_open, _target)) return;
            _open = Mathf.MoveTowards(_open, _target, Time.deltaTime * 2.2f);
            transform.position = _closed + SlideWorld * Mathf.SmoothStep(0f, 1f, _open);
        }
    }

    /// <summary>Something to look at closely: shows a short text (notice board, offering box, old stones…).</summary>
    public sealed class InspectPoint : MonoBehaviour, IInteractable
    {
        public string Title = "Inspect";
        public string Text;
        public Func<string> DynamicText;
        public string Prompt => "Inspect — " + Title;
        public Vector3 Position => transform.position;
        public bool CanInteract => true;

        public void Interact(PlayerController player)
        {
            var hud = LivingWorld.Instance != null ? LivingWorld.Instance.Hud : null;
            string text = DynamicText != null ? DynamicText() : Text;
            if (hud != null) hud.ShowLine(Title, text, 5f);
            else GameEvents.Notify(text);
        }

        private void OnEnable() => InteractableRegistry.Register(this);
        private void OnDisable() => InteractableRegistry.Unregister(this);

        public static InspectPoint Create(Transform parent, Vector3 pos, string title, string text, Func<string> dynamic = null)
        {
            var go = new GameObject("Inspect_" + title);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var p = go.AddComponent<InspectPoint>();
            p.Title = title;
            p.Text = text;
            p.DynamicText = dynamic;
            return p;
        }
    }

    /// <summary>A place to rest (inn futon, campfire): opens the rest choice.</summary>
    public sealed class RestPoint : MonoBehaviour, IInteractable
    {
        public string Label = "Rest";
        public Func<bool> Available;
        public Func<string> Unavailable;
        public string Prompt => Available == null || Available() ? Label : (Unavailable != null ? Unavailable() : Label);
        public Vector3 Position => transform.position;
        public bool CanInteract => RestSystem.Instance != null && !RestSystem.Instance.Open;

        public void Interact(PlayerController player)
        {
            if (Available != null && !Available())
            {
                if (LivingWorld.Instance != null && LivingWorld.Instance.Hud != null) LivingWorld.Instance.Hud.ShowLine(null, Unavailable != null ? Unavailable() : "You cannot rest now.", 3f);
                return;
            }
            RestSystem.Instance.Show(this);
        }

        private void OnEnable() => InteractableRegistry.Register(this);
        private void OnDisable() => InteractableRegistry.Unregister(this);

        public static RestPoint Create(Transform parent, Vector3 pos, string label)
        {
            var go = new GameObject("Rest_" + label);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var r = go.AddComponent<RestPoint>();
            r.Label = label;
            return r;
        }
    }

    /// <summary>Medicinal herb growing in the wild: picking it restores a little health; it grows back the next day.</summary>
    public sealed class HerbPickup : MonoBehaviour, IInteractable
    {
        public string Id;
        public string Prompt => "Pick up — Medicinal herb";
        public Vector3 Position => transform.position;
        public bool CanInteract => gameObject.activeInHierarchy;

        public void Interact(PlayerController player)
        {
            var w = LivingWorld.Instance;
            if (w != null) w.State.SetFact("herb_" + Id, w.Time.Clock.Day, w.Now);
            if (player != null && player.Damageable != null && player.Damageable.Health != null)
            {
                var h = player.Damageable.Health;
                h.Heal(h.Max * 0.2f);
            }
            Sfx.Play("ui_confirm", transform.position, 0.5f);
            VFXLibrary.Spawn("perfect_dodge", transform.position, Quaternion.identity, 0.3f);
            if (w != null && w.Hud != null) w.Hud.ShowLine(null, "Medicinal herb — you feel a little better.", 2.5f);
            gameObject.SetActive(false);
        }

        private void OnEnable() => InteractableRegistry.Register(this);
        private void OnDisable() => InteractableRegistry.Unregister(this);

        public static HerbPickup Create(Transform parent, Vector3 ground, string id)
        {
            var go = new GameObject("Herb_" + id);
            go.transform.SetParent(parent, false);
            go.transform.position = ground;
            var mat = MaterialFactory.Toon(new Color(0.35f, 0.62f, 0.32f), 0.6f, false, new Color(0.1f, 0.25f, 0.08f));
            var flower = MaterialFactory.Toon(new Color(0.95f, 0.85f, 0.4f), 0.4f, false, new Color(0.5f, 0.4f, 0.1f));
            for (int i = 0; i < 5; i++)
            {
                float a = i * 72f;
                var leaf = ProceduralMeshes.CreatePart("Leaf", ProceduralMeshes.Cone(5, 0.2f), mat, go.transform, Vector3.zero, Quaternion.Euler(28f, a, 0f), new Vector3(0.09f, 0.32f, 0.05f));
                leaf.layer = Layers.IgnoreRaycast;
            }
            var bud = ProceduralMeshes.CreatePart("Bud", PrimitiveType.Sphere, flower, go.transform, Vector3.up * 0.3f, Vector3.zero, Vector3.one * 0.07f);
            bud.layer = Layers.IgnoreRaycast;
            var h = go.AddComponent<HerbPickup>();
            h.Id = id;
            return h;
        }
    }

    /// <summary>
    /// Places the interactive points of a sector when it loads (they unload with it): rest spots, things to inspect,
    /// herbs. Text is short and in-world; nothing marks them on the map.
    /// </summary>
    public static class WorldInteractables
    {
        public static void Populate(SectorDef s, SectorInstance inst, LivingWorld world)
        {
            if (inst == null || inst.DynamicRoot == null) return;
            var root = new GameObject("Interactables").transform;
            root.SetParent(inst.DynamicRoot, false);

            Vector3 At(string place, float extra)
            {
                var p = L.Place(place);
                L.FrontPoint(p, extra, out float x, out float z);
                return RegionTerrain.OnGround(x, z) + Vector3.up * 1f;
            }

            switch (s.Id)
            {
                case "village":
                    {
                        var inn = RestPoint.Create(root, At("inn", 0.6f), "Rest at Kasumi Inn");
                        inn.transform.position += Vector3.forward * 2.2f;
                        InspectPoint.Create(root, At("hunter_post", 0.3f), "Notice board", null,
                            () => world.State.GetFact("Saved_Caravan_001") > 0
                                ? "«Gracias al viajero que defendió la caravana.» Debajo: «Se buscan cazadores. Desapariciones cerca de la Hondonada de Ceniza.»"
                                : "«Se buscan cazadores. Desapariciones cerca de la Hondonada de Ceniza. No viajar de noche.»");
                        InspectPoint.Create(root, At("village_shrine", 0.2f), "Offering box", "Unas monedas, un poco de arroz y una cuerda nueva. Alguien reza aquí cada mañana.");
                        InspectPoint.Create(root, At("well", 0.3f), "Well", "Agua fresca y fría. La cubeta cuelga de una cuerda gastada.");
                        InspectPoint.Create(root, RegionTerrain.OnGround(-14f, -106f) + Vector3.up, "Bell tower", "La campana de alarma. Si suena de noche, todos corren a la posada.");
                        break;
                    }
                case "forest_road":
                    {
                        var camp = L.Place("camp");
                        var rest = RestPoint.Create(root, RegionTerrain.OnGround(camp.X, camp.Z - 1.4f) + Vector3.up * 0.8f, "Rest by the campfire");
                        rest.Available = () => !WorldClock.IsNight(world.Time.Clock.Hour) || DemonsNear(world, camp.X, camp.Z, 30f) == false;
                        rest.Unavailable = () => "Something is moving in the dark nearby. Not now.";
                        InspectPoint.Create(root, At("crossroads", 0f) + Vector3.right * 2f, "Signpost", "Sur: Aldea Asagiri. Norte: el Gran Alcanforero. Este: las ruinas viejas. Oeste: la cascada.");
                        break;
                    }
                case "deep_forest":
                    InspectPoint.Create(root, RegionTerrain.OnGround(0f, 160f) + Vector3.up, "Elder Camphor", "Una cuerda sagrada rodea el tronco. El árbol es más viejo que la aldea.");
                    InspectPoint.Create(root, RegionTerrain.OnGround(-46f, 196f) + Vector3.up, "Stone guardians", "Dos guardianes de piedra cubiertos de musgo. Alguien les dejó flores frescas... ¿quién viene hasta aquí?");
                    break;
                case "old_ruins":
                    InspectPoint.Create(root, RegionTerrain.OnGround(166f, 46f) + Vector3.up, "Old crest", "Un emblema de luna creciente, casi borrado. Este lugar fue un puesto de cazadores hace mucho.");
                    break;
                case "mountain_path":
                    InspectPoint.Create(root, RegionTerrain.OnGround(-180f, 196f) + Vector3.up, "Cave mouth", "El viento sale de la cueva con un eco largo. Huele a agua y a piedra mojada.");
                    break;
                case "river_east":
                    InspectPoint.Create(root, At("river_shrine", 0.3f), "Riverside shrine", "Un pequeño santuario al dios del río. Las ofrendas son de pescado seco.");
                    break;
                case "danger_zone":
                    InspectPoint.Create(root, RegionTerrain.OnGround(173f, 104f) + Vector3.up, "Broken gate", "Marcas de garras en la madera. Talismanes quemados en el suelo. No es un buen lugar para quedarse.");
                    break;
            }

            // Herbs in the wild (deterministic spots per sector; picked ones grow back the next day).
            if (!s.SafeZone && s.Danger <= 2)
            {
                var rng = new System.Random(L.StableHash(s.Id) ^ 0x4e52);
                int count = 3 + rng.Next(3);
                int made = 0;
                for (int i = 0; i < 40 && made < count; i++)
                {
                    float x = Mathf.Lerp(s.Bounds.MinX + 10f, s.Bounds.MaxX - 10f, (float)rng.NextDouble());
                    float z = Mathf.Lerp(s.Bounds.MinZ + 10f, s.Bounds.MaxZ - 10f, (float)rng.NextDouble());
                    if (L.IsWater(x, z) || NpcPlaces.Blocked(x, z, 2f) || L.Slope(x, z) > 0.5f) continue;
                    string id = $"{s.Id}_{i}";
                    int picked = world.State.GetFact("herb_" + id, -1);
                    var herb = HerbPickup.Create(root, RegionTerrain.OnGround(x, z), id);
                    if (picked >= world.Time.Clock.Day) herb.gameObject.SetActive(false);
                    made++;
                }
            }
        }

        /// <summary>Hook for the demon director (v0.5 E): any active demon within <paramref name="radius"/>.</summary>
        public static Func<float, float, float, bool> DemonsNearQuery;

        private static bool DemonsNear(LivingWorld world, float x, float z, float radius) => DemonsNearQuery != null && DemonsNearQuery(x, z, radius);
    }
}
