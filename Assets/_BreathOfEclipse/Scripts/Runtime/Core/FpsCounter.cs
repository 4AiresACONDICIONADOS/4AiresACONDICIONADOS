using UnityEngine;

namespace BreathOfEclipse.Core
{
    /// <summary>Lightweight FPS / frame time display (F2 or Settings / Debug menu).</summary>
    public sealed class FpsCounter : MonoBehaviour
    {
        public static FpsCounter Instance { get; private set; }
        public bool Visible { get; set; }

        private float _accum;
        private int _frames;
        private float _fps;
        private float _worstMs;
        private float _worstAccum;
        private GUIStyle _style;

        private void Awake()
        {
            Instance = this;
            if (InputReader.Instance != null) InputReader.Instance.ToggleFpsPressed += Toggle;
        }

        private void Start()
        {
            if (InputReader.Instance != null)
            {
                InputReader.Instance.ToggleFpsPressed -= Toggle;
                InputReader.Instance.ToggleFpsPressed += Toggle;
            }
        }

        private void OnDestroy()
        {
            if (InputReader.Instance != null) InputReader.Instance.ToggleFpsPressed -= Toggle;
            if (Instance == this) Instance = null;
        }

        public void Toggle()
        {
            Visible = !Visible;
            SaveSystem.Settings.showFps = Visible;
            SaveSystem.SaveSettings();
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            _accum += dt;
            _frames++;
            _worstAccum = Mathf.Max(_worstAccum, dt * 1000f);
            if (_accum >= 0.5f)
            {
                _fps = _frames / _accum;
                _worstMs = _worstAccum;
                _accum = 0f;
                _frames = 0;
                _worstAccum = 0f;
            }
        }

        private void OnGUI()
        {
            if (!Visible) return;
            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold };
            }
            Color c = _fps >= 58f ? new Color(0.5f, 1f, 0.6f) : _fps >= 40f ? new Color(1f, 0.85f, 0.3f) : new Color(1f, 0.35f, 0.3f);
            _style.normal.textColor = c;
            GUI.Label(new Rect(Screen.width - 230, 8, 220, 24), $"{_fps:0} FPS  ({1000f / Mathf.Max(1f, _fps):0.0} ms, worst {_worstMs:0.0})", _style);
        }
    }
}
