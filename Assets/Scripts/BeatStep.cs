using System;
using UnityEngine;

namespace Gluttony
{
    public static class BeatStep
    {
        public const float HopBeats = 0.15f;

        public static float Evaluate(double beats, int stepBeats, float[] stops, int[] pattern)
        {
            stepBeats = Math.Max(1, stepBeats);
            long step = (long)Math.Floor(beats / stepBeats);
            double intoStep = beats - step * stepBeats;
            int from = pattern[Mod(step, pattern.Length)];
            double hopStart = stepBeats - HopBeats;
            if (intoStep < hopStart)
                return stops[from];
            int to = pattern[Mod(step + 1, pattern.Length)];
            float k = Mathf.Clamp01((float)((intoStep - hopStart) / HopBeats));
            k = 1f - (1f - k) * (1f - k);
            return Mathf.Lerp(stops[from], stops[to], k);
        }

        private static int Mod(long a, int n) => (int)(((a % n) + n) % n);
    }
}
