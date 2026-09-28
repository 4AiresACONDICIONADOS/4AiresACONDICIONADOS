using BreathOfEclipse.Combat;
using UnityEngine;

namespace BreathOfEclipse.Characters
{
    /// <summary>
    /// Receives Animation Events from imported clips and forwards them to gameplay (e.g. "OpenCancelWindow").
    /// Add Animation Events calling these methods on the clip; string parameter = CancelFlags names ("Dodge,Skill").
    /// </summary>
    public sealed class AnimationEventRelay : MonoBehaviour
    {
        public event System.Action<CancelFlags> CancelWindowOpened;
        public event System.Action<CancelFlags> CancelWindowClosed;
        public event System.Action HitFramesStarted;
        public event System.Action HitFramesEnded;
        public event System.Action<string> CustomEvent;

        public void OpenCancelWindow(string flags) => CancelWindowOpened?.Invoke(Parse(flags));
        public void CloseCancelWindow(string flags) => CancelWindowClosed?.Invoke(Parse(flags));
        public void HitStart() => HitFramesStarted?.Invoke();
        public void HitEnd() => HitFramesEnded?.Invoke();
        public void Custom(string id) => CustomEvent?.Invoke(id);

        private static CancelFlags Parse(string flags)
        {
            if (string.IsNullOrEmpty(flags)) return CancelFlags.All;
            return System.Enum.TryParse(flags, true, out CancelFlags parsed) ? parsed : CancelFlags.None;
        }
    }
}
