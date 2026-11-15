using UnityEngine;

namespace Gluttony
{
    public static class GameSettings
    {
        private const string VibrationKey = "Options.Vibration";
        private const string LegacyChargeBarKey = "Options.LegacyChargeBar";

        public static bool Vibration
        {
            get => PlayerPrefs.GetInt(VibrationKey, 1) == 1;
            set => PlayerPrefs.SetInt(VibrationKey, value ? 1 : 0);
        }

        public static bool LegacyChargeBar
        {
            get => PlayerPrefs.GetInt(LegacyChargeBarKey, 0) == 1;
            set => PlayerPrefs.SetInt(LegacyChargeBarKey, value ? 1 : 0);
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
