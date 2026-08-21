using System;
using Nytherion.Core.Enums;
using UnityEngine.Localization.Settings;

namespace Nytherion.Core.Utils
{
    public static class LocalizationText
    {
        public const string KoreanLocaleCode = "ko";
        public const string EnglishLocaleCode = "en";

        private static SupportedLanguage? temporaryLanguage;

        public static event Action LanguageChanged;

        public static SupportedLanguage CurrentLanguage => IsEnglishSelected
            ? SupportedLanguage.English
            : SupportedLanguage.Korean;

        public static bool IsConfigured
        {
            get
            {
                var settings = LocalizationSettings.GetInstanceDontCreateDefault();
                return settings != null &&
                       !string.Equals(
                           settings.name,
                           "Default Localization Settings",
                           StringComparison.Ordinal);
            }
        }

        public static bool IsEnglishSelected
        {
            get
            {
                if (temporaryLanguage.HasValue)
                {
                    return temporaryLanguage.Value == SupportedLanguage.English;
                }

                if (!IsConfigured)
                {
                    return false;
                }

                string localeCode = LocalizationSettings.SelectedLocale?.Identifier.Code;
                return !string.IsNullOrEmpty(localeCode) &&
                       localeCode.StartsWith(EnglishLocaleCode, StringComparison.OrdinalIgnoreCase);
            }
        }

        public static string Get(
            string tableName,
            string entryKey,
            string koreanFallback = "",
            string englishFallback = "",
            params object[] arguments)
        {
            string fallback = GetFallback(koreanFallback, englishFallback);

            if (!IsConfigured)
            {
                return FormatFallback(fallback, arguments);
            }

            if (!LocalizationSettings.InitializationOperation.IsDone)
            {
                return FormatFallback(fallback, arguments);
            }

            try
            {
                string localized = LocalizationSettings.StringDatabase.GetLocalizedString(
                    tableName,
                    entryKey,
                    arguments);

                return string.IsNullOrWhiteSpace(localized)
                    ? FormatFallback(fallback, arguments)
                    : localized;
            }
            catch (Exception exception)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                UnityEngine.Debug.LogWarning(
                    $"[Localization] '{tableName}/{entryKey}' 조회 실패: {exception.Message}");
#endif
                return FormatFallback(fallback, arguments);
            }
        }

        public static string GetFallback(string koreanFallback, string englishFallback)
        {
            if (IsEnglishSelected)
            {
                return !string.IsNullOrWhiteSpace(englishFallback)
                    ? englishFallback
                    : koreanFallback;
            }

            return !string.IsNullOrWhiteSpace(koreanFallback)
                ? koreanFallback
                : englishFallback;
        }

        public static void SetTemporaryLanguage(SupportedLanguage language)
        {
            if (temporaryLanguage == language)
            {
                return;
            }

            temporaryLanguage = language;
            LanguageChanged?.Invoke();
        }

        public static void NotifyLocaleChanged()
        {
            temporaryLanguage = null;
            LanguageChanged?.Invoke();
        }

        private static string FormatFallback(string fallback, object[] arguments)
        {
            if (string.IsNullOrEmpty(fallback) || arguments == null || arguments.Length == 0)
            {
                return fallback ?? string.Empty;
            }

            try
            {
                return string.Format(fallback, arguments);
            }
            catch (FormatException)
            {
                return fallback;
            }
        }
    }
}
