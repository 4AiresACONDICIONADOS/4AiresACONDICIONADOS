using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BreathOfEclipse.Core
{
    /// <summary>Loads scenes with a fade. Cleans pools and time state between scenes.</summary>
    public sealed class SceneLoader : MonoBehaviour
    {
        public static SceneLoader Instance { get; private set; }

        public bool IsLoading { get; private set; }

        /// <summary>Raised after a new scene finished loading (scene name).</summary>
        public static event Action<string> SceneLoaded;

        private CanvasGroup _fade;
        private bool _holdingForScene;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => SceneLoaded = null;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
            Services.Register(this);
            BuildFadeOverlay();
        }

        private void OnDestroy()
        {
            if (Instance != this) return;
            Instance = null;
            Services.Unregister(this);
        }

        private void BuildFadeOverlay()
        {
            var canvasGo = new GameObject("SceneFadeCanvas", typeof(Canvas), typeof(CanvasGroup));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;
            _fade = canvasGo.GetComponent<CanvasGroup>();
            _fade.alpha = 0f;
            _fade.blocksRaycasts = false;
            _fade.interactable = false;

            var imageGo = new GameObject("Black", typeof(RectTransform), typeof(Image));
            imageGo.transform.SetParent(canvasGo.transform, false);
            var rt = imageGo.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            imageGo.GetComponent<Image>().color = Color.black;
        }

        public void Load(string sceneName, float fadeDuration = 0.35f)
        {
            if (IsLoading) return;
            StartCoroutine(LoadRoutine(sceneName, fadeDuration));
        }

        public void ReloadCurrent() => Load(SceneManager.GetActiveScene().name);

        private IEnumerator LoadRoutine(string sceneName, float fadeDuration)
        {
            IsLoading = true;
            _fade.blocksRaycasts = true;
            yield return Fade(0f, 1f, fadeDuration);

            if (TimeController.Instance != null) TimeController.Instance.ResetAll();
            if (PoolManager.Instance != null) PoolManager.Instance.ReleaseAll();

            var op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            if (op == null)
            {
                Debug.LogError($"[SceneLoader] Scene '{sceneName}' is not in the build settings.");
            }
            else
            {
                while (!op.isDone) yield return null;
            }

            if (PoolManager.Instance != null) PoolManager.Instance.Prune();
            yield return null;
            SceneLoaded?.Invoke(sceneName);
            if (_holdingForScene)
            {
                // The new scene asked to stay black until it is ready; it fades in by itself.
                while (_holdingForScene) yield return null;
            }
            else
            {
                yield return Fade(1f, 0f, fadeDuration);
            }
            _fade.blocksRaycasts = false;
            IsLoading = false;
        }

        /// <summary>
        /// Keeps the screen black from this frame until the scene has rendered a few frames and, in the Editor, every
        /// shader variant it requested has compiled, then fades in. Without it the first second shows the Editor's cyan
        /// placeholder shader on the sky dome and terrain (the "blue screen" on Play).
        /// </summary>
        public void HoldBlackUntilReady(Action onRevealed = null, float fadeIn = 0.45f, float maxWait = 8f)
        {
            _holdingForScene = true;
            _fade.alpha = 1f;
            StartCoroutine(RevealWhenReady(onRevealed, fadeIn, maxWait));
        }

        private IEnumerator RevealWhenReady(Action onRevealed, float fadeIn, float maxWait)
        {
            float start = Time.unscaledTime;
            // Let the scene render under the black overlay so every material requests its shader variants.
            for (int i = 0; i < 3; i++) yield return null;
#if UNITY_EDITOR
            while (UnityEditor.ShaderUtil.anythingCompiling && Time.unscaledTime - start < maxWait) yield return null;
#endif
            yield return null;
            onRevealed?.Invoke();
            yield return Fade(1f, 0f, fadeIn);
            _holdingForScene = false;
        }

        /// <summary>Fades to black and back without loading (respawn transitions).</summary>
        public IEnumerator FadeOutIn(float duration, Action atBlack)
        {
            _fade.blocksRaycasts = true;
            yield return Fade(0f, 1f, duration);
            atBlack?.Invoke();
            yield return null;
            yield return Fade(1f, 0f, duration);
            _fade.blocksRaycasts = false;
        }

        private IEnumerator Fade(float from, float to, float duration)
        {
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                _fade.alpha = Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t / duration));
                yield return null;
            }
            _fade.alpha = to;
        }
    }
}
