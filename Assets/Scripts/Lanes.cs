using UnityEngine;

namespace Gluttony
{
    public static class Lanes
    {
        public const int Count = 3;
        public const float Width = 2.625f;
        public const float PlayHalfWidth = 4f;

        public static float X(int lane) => (lane - 1) * Width;

        public static int Nearest(float x) => Mathf.Clamp(Mathf.RoundToInt(x / Width) + 1, 0, Count - 1);
    }
}
