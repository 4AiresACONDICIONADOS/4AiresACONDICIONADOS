using System.Collections.Generic;

namespace BreathOfEclipse.Core
{
    /// <summary>Gameplay actions that are buffered so early presses are not lost during attacks.</summary>
    public enum BufferedAction
    {
        LightAttack = 0,
        HeavyAttack = 1,
        Dodge = 2,
        Jump = 3,
        Skill1 = 4,
        Skill2 = 5,
        Skill3 = 6,
        Skill4 = 7,
        Ultimate = 8,
        Block = 9
    }

    /// <summary>
    /// Stores the most recent press of each action with a timestamp. Consumers ask "was X pressed within the
    /// last N seconds?" and consume it, which gives responsive combo input without accidental double actions.
    /// </summary>
    public sealed class InputBuffer
    {
        private readonly Dictionary<BufferedAction, float> _presses = new Dictionary<BufferedAction, float>();

        public float DefaultWindow { get; set; }

        public InputBuffer(float defaultWindow = 0.3f)
        {
            DefaultWindow = defaultWindow;
        }

        public void Record(BufferedAction action, float time) => _presses[action] = time;

        public bool Has(BufferedAction action, float now, float window = -1f)
        {
            if (!_presses.TryGetValue(action, out var t)) return false;
            float w = window < 0f ? DefaultWindow : window;
            return now - t <= w;
        }

        public bool Consume(BufferedAction action, float now, float window = -1f)
        {
            if (!Has(action, now, window)) return false;
            _presses.Remove(action);
            return true;
        }

        public void Clear(BufferedAction action) => _presses.Remove(action);

        public void ClearAll() => _presses.Clear();

        /// <summary>Returns the most recently pressed action among the candidates, if any is still buffered.</summary>
        public bool TryGetLatest(BufferedAction[] candidates, float now, out BufferedAction latest, float window = -1f)
        {
            latest = default;
            float best = float.NegativeInfinity;
            bool found = false;
            float w = window < 0f ? DefaultWindow : window;
            for (int i = 0; i < candidates.Length; i++)
            {
                if (_presses.TryGetValue(candidates[i], out var t) && now - t <= w && t > best)
                {
                    best = t;
                    latest = candidates[i];
                    found = true;
                }
            }
            return found;
        }
    }
}
