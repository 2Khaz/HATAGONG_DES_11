using System;
using UnityEngine;

namespace HATAGONG.GameFlow
{
    public static class IngameOptionPreferences
    {
        private const string BgmKey = "HATAGONG.INGAME.BGM_ENABLED.V1";
        private const string SfxKey = "HATAGONG.INGAME.SFX_ENABLED.V1";
        private const string VibrationKey = "HATAGONG.INGAME.VIBRATION_ENABLED.V1";

        public static bool BgmEnabled => Read(BgmKey);
        public static bool SfxEnabled => Read(SfxKey);
        public static bool VibrationEnabled => Read(VibrationKey);

        public static event Action Changed;

        public static void SetBgmEnabled(bool enabled) => Write(BgmKey, enabled);
        public static void SetSfxEnabled(bool enabled) => Write(SfxKey, enabled);
        public static void SetVibrationEnabled(bool enabled) => Write(VibrationKey, enabled);

        private static bool Read(string key)
        {
            if (!PlayerPrefs.HasKey(key)) return true;
            int value = PlayerPrefs.GetInt(key, 1);
            if (value == 0) return false;
            if (value == 1) return true;
            PlayerPrefs.SetInt(key, 1);
            PlayerPrefs.Save();
            return true;
        }

        private static void Write(string key, bool enabled)
        {
            int value = enabled ? 1 : 0;
            if (PlayerPrefs.HasKey(key) && PlayerPrefs.GetInt(key, 1) == value) return;
            PlayerPrefs.SetInt(key, value);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }
    }
}
