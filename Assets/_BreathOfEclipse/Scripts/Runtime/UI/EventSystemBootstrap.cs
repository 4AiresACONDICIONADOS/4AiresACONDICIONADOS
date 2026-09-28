using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace BreathOfEclipse.UI
{
    /// <summary>Guarantees a single persistent EventSystem using the Input System UI module.</summary>
    public sealed class EventSystemBootstrap : MonoBehaviour
    {
        private void Awake() => Ensure();

        private void OnEnable() => UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
        private void OnDisable() => UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;

        private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode) => Ensure();

        private void Ensure()
        {
            var existing = FindObjectsByType<EventSystem>(FindObjectsSortMode.None);
            EventSystem keep = null;
            foreach (var es in existing)
            {
                if (keep == null && es.transform.IsChildOf(transform)) keep = es;
            }
            foreach (var es in existing)
            {
                if (es != keep && keep != null) Destroy(es.gameObject);
            }
            if (keep != null) return;
            if (existing.Length > 0)
            {
                // Scene already provides one: make sure it uses the Input System module.
                var es = existing[0];
                var legacy = es.GetComponent<StandaloneInputModule>();
                if (legacy != null) Destroy(legacy);
                if (es.GetComponent<InputSystemUIInputModule>() == null) es.gameObject.AddComponent<InputSystemUIInputModule>();
                return;
            }
            var go = new GameObject("EventSystem");
            go.transform.SetParent(transform, false);
            go.AddComponent<EventSystem>();
            go.AddComponent<InputSystemUIInputModule>();
        }
    }
}
