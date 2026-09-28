using System.Collections;
using BreathOfEclipse.Combat;
using BreathOfEclipse.Core;
using BreathOfEclipse.UI;
using BreathOfEclipse.VFX;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BreathOfEclipse.Scenes
{
    /// <summary>
    /// 00_Boot: services are already created by <see cref="GameManager"/> (BeforeSceneLoad). This scene shows a
    /// short splash, warms up shaders/VFX pools, then loads the main menu.
    /// </summary>
    public sealed class BootLoader : MonoBehaviour
    {
        [SerializeField] private float minimumSplash = 1.2f;
        [SerializeField] private string nextScene = SceneNames.MainMenu;

        private IEnumerator Start()
        {
            CursorManager.SetGameplay(false);
            var cam = new GameObject("BootCamera").AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.01f, 0.01f, 0.03f);
            var canvas = UIFactory.Canvas("Splash", 10);
            UIFactory.Text("Title", canvas.transform, "BREATH OF ECLIPSE", 72, Color.white, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 20f), new Vector2(1400f, 110f), TextAnchor.MiddleCenter, FontStyle.BoldAndItalic);
            var status = UIFactory.Text("Status", canvas.transform, "Awakening...", 22, UIColors.TextDim, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -60f), new Vector2(900f, 40f), TextAnchor.MiddleCenter, FontStyle.Normal);

            float start = Time.unscaledTime;
            yield return null;
            status.text = "Warming up techniques...";
            yield return null;
            WarmUpVfx();
            while (Time.unscaledTime - start < minimumSplash) yield return null;
            status.text = "";
            if (SceneLoader.Instance != null) SceneLoader.Instance.Load(nextScene, 0.5f);
            else SceneManager.LoadScene(nextScene);
        }

        /// <summary>
        /// Builds every VFX recipe once far below the world so meshes, textures, materials and pools exist before
        /// gameplay (avoids first-use hitches in combat).
        /// </summary>
        private static void WarmUpVfx()
        {
            var ids = new System.Collections.Generic.List<string>(VFXLibrary.Ids);
            foreach (var id in ids)
            {
                try
                {
                    VFXLibrary.Spawn(id, new Vector3(0f, -500f, 0f), Quaternion.identity, 1f, Element.None, null, 0.05f);
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[Boot] VFX warm-up failed for '{id}': {e.Message}");
                }
            }
        }
    }
}
