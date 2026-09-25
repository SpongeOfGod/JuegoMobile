using UnityEngine;

namespace Gluttony
{
    public abstract class BeatBehaviour : MonoBehaviour
    {
        private int lastBeat = int.MinValue;

        protected virtual void Update()
        {
            var conductor = Conductor.Instance;
            if (conductor == null || !conductor.IsRunning || conductor.IsPaused)
                return;

            int beat = conductor.CurrentBeat;
            if (lastBeat == int.MinValue || beat - lastBeat > 4)
                lastBeat = beat;
            while (lastBeat < beat)
            {
                lastBeat++;
                OnBeat(lastBeat);
            }
        }

        protected abstract void OnBeat(int beat);
    }
}
