using Nytherion.Core.Enums;
using UnityEngine;

namespace Nytherion.Core.Data
{
    /// <summary>
    /// 세이브 슬롯과 무관하게 유지되어야 하는 사용자 환경설정을 관리합니다.
    /// </summary>
    public static class UserSettings
    {
        private const string KeyPrefix = "Nytherion.Settings.";
        private const string MasterVolumeKey = KeyPrefix + "MasterVolume";
        private const string BgmVolumeKey = KeyPrefix + "BgmVolume";
        private const string SfxVolumeKey = KeyPrefix + "SfxVolume";
        private const string FullscreenKey = KeyPrefix + "Fullscreen";
        private const string ResolutionWidthKey = KeyPrefix + "ResolutionWidth";
        private const string ResolutionHeightKey = KeyPrefix + "ResolutionHeight";
        private const string LanguageKey = KeyPrefix + "Language";

        public static float GetMasterVolume(float defaultValue = 1f)
        {
            return Mathf.Clamp01(PlayerPrefs.GetFloat(MasterVolumeKey, defaultValue));
        }

        public static float GetBgmVolume(float defaultValue = 1f)
        {
            return Mathf.Clamp01(PlayerPrefs.GetFloat(BgmVolumeKey, defaultValue));
        }

        public static float GetSfxVolume(float defaultValue = 1f)
        {
            return Mathf.Clamp01(PlayerPrefs.GetFloat(SfxVolumeKey, defaultValue));
        }

        public static bool GetFullscreen(bool defaultValue)
        {
            return PlayerPrefs.HasKey(FullscreenKey)
                ? PlayerPrefs.GetInt(FullscreenKey) != 0
                : defaultValue;
        }

        public static bool TryGetResolution(out int width, out int height)
        {
            width = PlayerPrefs.GetInt(ResolutionWidthKey, 0);
            height = PlayerPrefs.GetInt(ResolutionHeightKey, 0);
            return PlayerPrefs.HasKey(ResolutionWidthKey) &&
                   PlayerPrefs.HasKey(ResolutionHeightKey) &&
                   width > 0 &&
                   height > 0;
        }

        public static SupportedLanguage GetLanguage(SupportedLanguage defaultValue)
        {
            int savedValue = PlayerPrefs.GetInt(LanguageKey, (int)defaultValue);
            return savedValue == (int)SupportedLanguage.English
                ? SupportedLanguage.English
                : SupportedLanguage.Korean;
        }

        public static void SetMasterVolume(float value)
        {
            PlayerPrefs.SetFloat(MasterVolumeKey, Mathf.Clamp01(value));
        }

        public static void SetBgmVolume(float value)
        {
            PlayerPrefs.SetFloat(BgmVolumeKey, Mathf.Clamp01(value));
        }

        public static void SetSfxVolume(float value)
        {
            PlayerPrefs.SetFloat(SfxVolumeKey, Mathf.Clamp01(value));
        }

        public static void SetFullscreen(bool isFullscreen)
        {
            PlayerPrefs.SetInt(FullscreenKey, isFullscreen ? 1 : 0);
        }

        public static void SetResolution(int width, int height)
        {
            if (width <= 0 || height <= 0)
            {
                return;
            }

            PlayerPrefs.SetInt(ResolutionWidthKey, width);
            PlayerPrefs.SetInt(ResolutionHeightKey, height);
        }

        public static void SetLanguage(SupportedLanguage language)
        {
            PlayerPrefs.SetInt(LanguageKey, (int)language);
        }

        public static void ApplyBeforeSceneLoad()
        {
            AudioListener.volume = GetMasterVolume(AudioListener.volume);

            bool hasResolution = TryGetResolution(out int width, out int height);
            bool hasFullscreenSetting = PlayerPrefs.HasKey(FullscreenKey);
            if (hasResolution)
            {
                Screen.SetResolution(width, height, GetFullscreen(Screen.fullScreen));
            }
            else if (hasFullscreenSetting)
            {
                Screen.fullScreen = GetFullscreen(Screen.fullScreen);
            }
        }

        public static void Save()
        {
            PlayerPrefs.Save();
        }
    }
}
