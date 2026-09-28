using UnityEngine;

namespace BreathOfEclipse.Playtest
{
    /// <summary>Interactive development lab shown by <see cref="RuntimePlaytestSystem"/> (VFX, camera, manual).</summary>
    public abstract class PlaytestLab : MonoBehaviour
    {
        protected PlaytestContext Ctx { get; private set; }
        public abstract string Title { get; }
        /// <summary>Set by the lab when the tester asks to leave (EXIT button).</summary>
        public bool ExitRequested { get; protected set; }

        public void Begin(PlaytestContext ctx)
        {
            Ctx = ctx;
            OnBegin();
        }

        protected abstract void OnBegin();

        /// <summary>Restores anything the lab changed and reports its results.</summary>
        public abstract void End();

        /// <summary>Draws the lab panel (IMGUI) inside the given area; returns the used height.</summary>
        public abstract float DrawPanel(Rect area);
    }
}
