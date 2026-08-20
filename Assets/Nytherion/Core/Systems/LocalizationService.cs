using System;
using Nytherion.Core.Enums;
using Nytherion.Core.Interfaces;
using Nytherion.Core.Utils;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

namespace Nytherion.Core.Systems
{
    public sealed class LocalizationService : ILocalizationService, IDisposable
    {
        private SupportedLanguage? pendingLanguage;
        private bool isWaitingForInitialization;
        private bool isSubscribed;

        public event Action<SupportedLanguage> LanguageChanged;

        public SupportedLanguage CurrentLanguage => LocalizationText.CurrentLanguage;
        public bool IsReady => LocalizationText.IsConfigured &&
                               LocalizationSettings.InitializationOperation.IsDone;

        public LocalizationService()
        {
            if (LocalizationText.IsConfigured)
            {
                LocalizationSettings.SelectedLocaleChanged += OnSelectedLocaleChanged;
                isSubscribed = true;
            }
        }

        public void SetLanguage(SupportedLanguage language)
        {
            if (!LocalizationText.IsConfigured)
            {
                LocalizationText.SetTemporaryLanguage(language);
                LanguageChanged?.Invoke(language);
                return;
            }

            if (!LocalizationSettings.InitializationOperation.IsDone)
            {
                pendingLanguage = language;

                if (!isWaitingForInitialization)
                {
                    isWaitingForInitialization = true;
                    LocalizationSettings.InitializationOperation.Completed += _ =>
                    {
                        isWaitingForInitialization = false;

                        if (pendingLanguage.HasValue)
                        {
                            SupportedLanguage selectedLanguage = pendingLanguage.Value;
                            pendingLanguage = null;
                            ApplyLanguage(selectedLanguage);
                        }
                    };
                }

                return;
            }

            ApplyLanguage(language);
        }

        public string GetString(
            string tableName,
            string entryKey,
            string koreanFallback = "",
            string englishFallback = "",
            params object[] arguments)
        {
            return LocalizationText.Get(
                tableName,
                entryKey,
                koreanFallback,
                englishFallback,
                arguments);
        }

        public void Dispose()
        {
            if (isSubscribed)
            {
                LocalizationSettings.SelectedLocaleChanged -= OnSelectedLocaleChanged;
                isSubscribed = false;
            }
        }

        private static SupportedLanguage ToSupportedLanguage(Locale locale)
        {
            string localeCode = locale?.Identifier.Code;
            return !string.IsNullOrEmpty(localeCode) &&
                   localeCode.StartsWith(LocalizationText.EnglishLocaleCode, StringComparison.OrdinalIgnoreCase)
                ? SupportedLanguage.English
                : SupportedLanguage.Korean;
        }

        private static string GetLocaleCode(SupportedLanguage language)
        {
            return language == SupportedLanguage.English
                ? LocalizationText.EnglishLocaleCode
                : LocalizationText.KoreanLocaleCode;
        }

        private void ApplyLanguage(SupportedLanguage language)
        {
            if (!LocalizationText.IsConfigured)
            {
                return;
            }

            Locale locale = LocalizationSettings.AvailableLocales.GetLocale(GetLocaleCode(language));
            if (locale == null || LocalizationSettings.SelectedLocale == locale)
            {
                return;
            }

            LocalizationSettings.SelectedLocale = locale;
        }

        private void OnSelectedLocaleChanged(Locale locale)
        {
            LocalizationText.NotifyLocaleChanged();
            LanguageChanged?.Invoke(ToSupportedLanguage(locale));
        }
    }
}
