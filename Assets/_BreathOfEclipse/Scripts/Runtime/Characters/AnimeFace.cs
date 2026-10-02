using BreathOfEclipse.Audio;
using BreathOfEclipse.Rendering;
using UnityEngine;

namespace BreathOfEclipse.Characters
{
    /// <summary>
    /// Face of an imported character: anime eyes (procedural textures for neutral / focused / attack / pain /
    /// closed), blinking, and a mouth that opens with the voice's loudness (RMS), shouts on attacks and parts on
    /// inhales. Models with facial blendshapes (VRoid / VRM names such as Fcl_EYE_Close, Fcl_MTH_A, Blink, A)
    /// get the same expressions through their blendshapes instead.
    /// </summary>
    [DefaultExecutionOrder(75)]
    public sealed class AnimeFace : MonoBehaviour
    {
        public enum Expression
        {
            Neutral = 0,
            Focused = 1,
            Attack = 2,
            Pain = 3,
            Closed = 4
        }

        public Expression Current { get; private set; }
        /// <summary>Debug: forces an expression (null = automatic).</summary>
        public Expression? Forced { get; set; }
        /// <summary>Light characters without a procedural animator (NPCs): their mood and whether they are speaking.</summary>
        public Expression Mood { get; set; }
        public bool Speaking { get; set; }

        private ProceduralAnimator _src;
        private VoiceChannel _voice;
        private Renderer _eyes;
        private Material[] _eyeMaterials;
        private Transform _mouth;
        private Renderer _mouthRenderer;
        private float _mouthOpen;
        private float _restOpen;
        private float _nextBlink;
        private float _blinkUntil;
        private bool _demon;
        private float _nextVoiceLookup;

        private SkinnedMeshRenderer _shapes;
        private int _bsBlink = -1, _bsMouth = -1, _bsAngry = -1, _bsSorrow = -1, _bsFocus = -1;

        public void Initialize(ProceduralAnimator source, Renderer eyes, Material[] eyeMaterials, Transform mouth, bool demon, float restOpen = 0f)
        {
            _src = source;
            _eyes = eyes;
            _eyeMaterials = eyeMaterials;
            _mouth = mouth;
            _mouthRenderer = mouth != null ? mouth.GetComponent<Renderer>() : null;
            _demon = demon;
            _restOpen = restOpen;
            _voice = source != null ? source.GetComponentInChildren<VoiceChannel>() : null;
            _nextBlink = Time.time + Random.Range(1.5f, 4f);
        }

        /// <summary>Uses facial blendshapes of <paramref name="renderer"/> when it has recognisable ones.</summary>
        public void UseBlendShapes(SkinnedMeshRenderer renderer)
        {
            if (renderer == null || renderer.sharedMesh == null) return;
            var mesh = renderer.sharedMesh;
            _bsBlink = Find(mesh, "Fcl_EYE_Close", "Blink", "blink", "eyes_closed", "EyeClose", "eye_close");
            _bsMouth = Find(mesh, "Fcl_MTH_A", "vrc.v_aa", "Mouth_A", "MTH_A", "aa", "A");
            _bsAngry = Find(mesh, "Fcl_ALL_Angry", "Fcl_EYE_Angry", "Angry", "angry");
            _bsSorrow = Find(mesh, "Fcl_ALL_Sorrow", "Fcl_EYE_Sorrow", "Sorrow", "sad", "Pain");
            _bsFocus = Find(mesh, "Fcl_EYE_Spread", "Fcl_BRW_Angry", "Focus", "serious");
            if (_bsBlink >= 0 || _bsMouth >= 0) _shapes = renderer;
        }

        private static int Find(Mesh mesh, params string[] names)
        {
            foreach (var n in names)
            {
                int i = mesh.GetBlendShapeIndex(n);
                if (i >= 0) return i;
            }
            return -1;
        }

        private void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            if (_src == null)
            {
                UpdateLight(dt);
                return;
            }
            // The voice channel is created after the visual (PlayerController / EnemyVoice): look it up lazily.
            if (_voice == null && Time.time >= _nextVoiceLookup)
            {
                _nextVoiceLookup = Time.time + 1f;
                _voice = _src.GetComponentInChildren<VoiceChannel>();
            }

            // ---- expression from what the character is doing
            Expression e = Expression.Neutral;
            string action = _src.ActionId;
            if (_src.Dead) e = Expression.Closed;
            else if (_src.HitWeight > 0.25f || _src.Down) e = Expression.Pain;
            else if (_src.ActionIsAttack) e = Expression.Attack;
            else if (action != null && action.StartsWith("Skill")) e = action == "SkillInhale" ? Expression.Focused : Expression.Attack;
            else if (_src.BlockWeight > 0.5f) e = Expression.Focused;
            if (Forced.HasValue) e = Forced.Value;
            Current = e;

            bool blinking = false;
            if (!_demon && e != Expression.Attack && e != Expression.Closed)
            {
                if (Time.time >= _nextBlink)
                {
                    _blinkUntil = Time.time + 0.11f;
                    _nextBlink = Time.time + Random.Range(2f, 5f);
                }
                blinking = Time.time < _blinkUntil;
            }
            Expression shown = blinking ? Expression.Closed : e;
            if (_eyes != null && _eyeMaterials != null)
            {
                var m = _eyeMaterials[Mathf.Clamp((int)shown, 0, _eyeMaterials.Length - 1)];
                if (m != null && _eyes.sharedMaterial != m) _eyes.sharedMaterial = m;
            }

            // ---- mouth: voice loudness, attack shout, pain grit, inhale
            float target = _restOpen;
            if (_voice != null) target = Mathf.Max(target, Mathf.Clamp01(_voice.Level * 7f));
            if (e == Expression.Attack && _src.ActionIsAttack)
            {
                float t = _src.ActionElapsed;
                float strike = _src.ActionWindup;
                if (t > strike - 0.05f && t < strike + _src.ActionActive + 0.1f) target = Mathf.Max(target, 0.55f);
            }
            if (e == Expression.Pain) target = Mathf.Max(target, 0.35f);
            if (action == "SkillInhale") target = Mathf.Max(target, 0.25f);
            _mouthOpen = Mathf.Lerp(_mouthOpen, target, 1f - Mathf.Exp(-(target > _mouthOpen ? 30f : 14f) * dt));
            if (_mouth != null)
            {
                _mouth.localScale = new Vector3(1f, Mathf.Max(0.02f, _mouthOpen), 1f);
                if (_mouthRenderer != null) _mouthRenderer.enabled = _mouthOpen > 0.06f && _eyes != null && _eyes.enabled;
            }

            if (_shapes != null)
            {
                SetShape(_bsBlink, shown == Expression.Closed ? 100f : 0f);
                SetShape(_bsMouth, _mouthOpen * 100f);
                SetShape(_bsAngry, e == Expression.Attack ? 70f : 0f);
                SetShape(_bsSorrow, e == Expression.Pain ? 80f : 0f);
                SetShape(_bsFocus, e == Expression.Focused ? 60f : 0f);
            }
        }

        /// <summary>NPC face: mood, blinking and a talking mouth (no combat state).</summary>
        private void UpdateLight(float dt)
        {
            Expression e = Forced ?? Mood;
            Current = e;
            bool blinking = false;
            if (!_demon && e != Expression.Closed)
            {
                if (Time.time >= _nextBlink)
                {
                    _blinkUntil = Time.time + 0.11f;
                    _nextBlink = Time.time + Random.Range(2f, 5.5f);
                }
                blinking = Time.time < _blinkUntil;
            }
            Expression shown = blinking ? Expression.Closed : e;
            if (_eyes != null && _eyeMaterials != null)
            {
                var m = _eyeMaterials[Mathf.Clamp((int)shown, 0, _eyeMaterials.Length - 1)];
                if (m != null && _eyes.sharedMaterial != m) _eyes.sharedMaterial = m;
            }
            float target = _restOpen;
            if (Speaking) target = Mathf.Max(target, 0.15f + 0.4f * Mathf.Abs(Mathf.Sin(Time.time * 11f)) * (0.6f + 0.4f * Mathf.Sin(Time.time * 3.1f)));
            if (e == Expression.Pain) target = Mathf.Max(target, 0.35f);
            _mouthOpen = Mathf.Lerp(_mouthOpen, target, 1f - Mathf.Exp(-20f * dt));
            if (_mouth != null)
            {
                _mouth.localScale = new Vector3(1f, Mathf.Max(0.02f, _mouthOpen), 1f);
                if (_mouthRenderer != null) _mouthRenderer.enabled = _mouthOpen > 0.06f && _eyes != null && _eyes.enabled;
            }
        }

        private void SetShape(int index, float weight)
        {
            if (index < 0) return;
            float current = _shapes.GetBlendShapeWeight(index);
            _shapes.SetBlendShapeWeight(index, Mathf.MoveTowards(current, weight, Time.deltaTime * 900f));
        }

        // ------------------------------------------------------------------ eye textures

        /// <summary>
        /// Anime eye texture in the Quaternius eyeball UV layout (iris centred at 0.5, 0.5): large vertical iris
        /// with a dark rim, gradient from the upper lid shadow to a lighter lower half, two highlights.
        /// </summary>
        public static Texture2D EyeTexture(Expression e, Color iris, Color irisLow, Color skin, bool demon)
        {
            string key = $"eye_{e}_{(demon ? "demon" : "hero")}_{iris.r:F2}{iris.g:F2}{iris.b:F2}";
            return ProceduralTextures.Generate(key, 128, 128, (u, v) =>
            {
                float x = u - 0.5f, y = v - 0.5f;
                Color sclera = demon ? new Color(0.14f, 0.02f, 0.05f) : new Color(0.97f, 0.97f, 1f);
                if (!demon) sclera = Color.Lerp(sclera, new Color(0.72f, 0.74f, 0.86f), Mathf.Clamp01((y - 0.06f) / 0.12f));
                float rx = e == Expression.Focused ? 0.11f : 0.125f, ry = e == Expression.Focused ? 0.14f : 0.155f;
                float r = Mathf.Sqrt(x * x / (rx * rx) + y * y / (ry * ry));
                Color c = sclera;
                if (r < 1f)
                {
                    float grad = Mathf.Clamp01(0.5f - y / (ry * 2f));
                    c = Color.Lerp(iris, irisLow, grad);
                    if (r > 0.86f) c = Color.Lerp(c, new Color(0.03f, 0.03f, 0.07f), 0.85f);
                    float pupil = demon ? Mathf.Abs(x) / 0.018f + (y * y) / (0.12f * 0.12f) : r / (e == Expression.Focused ? 0.34f : 0.4f);
                    if (pupil < 1f) c = new Color(0.02f, 0.02f, 0.05f);
                    if (!demon)
                    {
                        // upper lid shadow over the top of the iris
                        if (y > ry * 0.35f) c *= e == Expression.Focused ? 0.55f : 0.7f;
                        float h1 = new Vector2(x + 0.04f, y - 0.05f).magnitude;
                        float h2 = new Vector2(x - 0.045f, y + 0.055f).magnitude;
                        if (h1 < (e == Expression.Focused ? 0.024f : 0.034f)) c = Color.white;
                        else if (h2 < 0.014f) c = new Color(0.92f, 0.95f, 1f);
                    }
                }
                if (demon) return c;
                // lids for the expressions (skin above / below the line, a dark lash line on the edge)
                float lid = e == Expression.Attack ? 0.07f : e == Expression.Pain ? 0.02f : e == Expression.Closed ? -0.03f - 0.05f * (1f - Mathf.Clamp01(x * x / 0.06f)) : 1f;
                if (y > lid)
                {
                    c = skin;
                    if (y < lid + 0.022f) c = new Color(0.08f, 0.06f, 0.08f);
                }
                if (e == Expression.Closed && y < lid - 0.02f) c = skin;
                if (e == Expression.Pain && y < -0.11f) c = skin;
                return c;
            });
        }
    }
}
