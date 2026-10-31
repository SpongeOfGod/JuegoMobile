using UnityEngine;

namespace Gluttony
{
    public static class GameSettings
    {
        private const string VibrationKey = "Options.Vibration";

        public static bool Vibration
        {
            get => PlayerPrefs.GetInt(VibrationKey, 1) == 1;
            set => PlayerPrefs.SetInt(VibrationKey, value ? 1 : 0);
        }

        public static void Vibrate()
        {
            if (!Vibration)
                return;
#if UNITY_ANDROID || UNITY_IOS
            Handheld.Vibrate();
#endif
        }

        public static void Save() => PlayerPrefs.Save();
    }
}
