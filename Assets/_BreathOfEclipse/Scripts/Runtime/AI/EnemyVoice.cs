using BreathOfEclipse.Audio;
using UnityEngine;

namespace BreathOfEclipse.AI
{
    /// <summary>
    /// Demon vocalisations from the enemy's position (3D): a grunt on some attacks and a cry when staggered,
    /// knocked down or launched. One voice per enemy; death silences it (the death sound plays separately).
    /// </summary>
    public sealed class EnemyVoice : MonoBehaviour
    {
        private EnemyController _enemy;
        private VoiceChannel _voice;
        private string _prefix;
        private float _nextGrunt, _nextHurt;

        public static EnemyVoice Attach(EnemyController enemy, string prefix)
        {
            if (string.IsNullOrEmpty(prefix)) return null;
            var v = enemy.gameObject.AddComponent<EnemyVoice>();
            v._enemy = enemy;
            v._prefix = prefix;
            v._voice = VoiceChannel.Create(enemy.transform, true, enemy.IsBoss ? 1f : 0.8f);
            enemy.StateChanged += v.OnStateChanged;
            return v;
        }

        private void OnDestroy()
        {
            if (_enemy != null) _enemy.StateChanged -= OnStateChanged;
        }

        private void OnStateChanged(EnemyStateId from, EnemyStateId to)
        {
            float now = Time.unscaledTime;
            switch (to)
            {
                case EnemyStateId.Attack:
                    if (now >= _nextGrunt && Random.value < (_enemy.IsBoss ? 0.7f : 0.45f))
                    {
                        Say(_prefix + "_grunt");
                        _nextGrunt = now + (_enemy.IsBoss ? 2f : 1.4f);
                    }
                    break;
                case EnemyStateId.Stagger:
                case EnemyStateId.Knockdown:
                case EnemyStateId.Airborne:
                    if (now >= _nextHurt)
                    {
                        Say(_prefix + "_hurt");
                        _nextHurt = now + 0.6f;
                    }
                    break;
                case EnemyStateId.Dead:
                    _voice.Stop(0.05f);
                    break;
            }
        }

        private void Say(string id)
        {
            var clip = AudioManager.Instance != null ? AudioManager.Instance.Clip(id) : null;
            if (clip == null) return;
            _voice.Speak(new[] { new VoiceChannel.Line { Text = id, Clip = clip } });
        }
    }
}
