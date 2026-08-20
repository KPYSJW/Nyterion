using System.Collections;
using System.Collections.Generic;
using Nytherion.Core.Enums;
using Nytherion.Core.Interfaces;
using Nytherion.Core.Utils;
using TMPro;
using UnityEngine;
using UnityEngine.Localization.Settings;
using VContainer;

namespace Nytherion.UI.Controllers
{
    [RequireComponent(typeof(TMP_Dropdown))]
    public class LanguageDropdownController : MonoBehaviour
    {
        private TMP_Dropdown dropdown;
        private ILocalizationService localizationService;
        private Coroutine initializationCoroutine;

        [Inject]
        public void Construct(ILocalizationService localizationService)
        {
            this.localizationService = localizationService;
        }

        private void Awake()
        {
            dropdown = GetComponent<TMP_Dropdown>();
        }

        private void OnEnable()
        {
            if (!LocalizationText.IsConfigured)
            {
                InitializeDropdown();
                dropdown.interactable = false;
                return;
            }

            dropdown.interactable = true;

            if (LocalizationSettings.InitializationOperation.IsDone)
            {
                InitializeDropdown();
            }
            else
            {
                initializationCoroutine = StartCoroutine(InitializeAfterLocalization());
            }
        }

        private void OnDisable()
        {
            if (dropdown != null)
            {
                dropdown.onValueChanged.RemoveListener(OnLanguageSelected);
            }

            if (localizationService != null)
            {
                localizationService.LanguageChanged -= OnLanguageChanged;
            }

            if (initializationCoroutine != null)
            {
                StopCoroutine(initializationCoroutine);
                initializationCoroutine = null;
            }
        }

        private IEnumerator InitializeAfterLocalization()
        {
            if (!LocalizationText.IsConfigured)
            {
                initializationCoroutine = null;
                yield break;
            }

            yield return LocalizationSettings.InitializationOperation;
            initializationCoroutine = null;
            InitializeDropdown();
        }

        private void InitializeDropdown()
        {
            if (dropdown == null)
            {
                return;
            }

            dropdown.onValueChanged.RemoveListener(OnLanguageSelected);
            dropdown.ClearOptions();
            dropdown.AddOptions(new List<string> { "한국어", "English" });
            dropdown.SetValueWithoutNotify((int)GetCurrentLanguage());
            dropdown.RefreshShownValue();
            dropdown.onValueChanged.AddListener(OnLanguageSelected);

            if (localizationService != null)
            {
                localizationService.LanguageChanged -= OnLanguageChanged;
                localizationService.LanguageChanged += OnLanguageChanged;
            }
        }

        private SupportedLanguage GetCurrentLanguage()
        {
            if (localizationService != null)
            {
                return localizationService.CurrentLanguage;
            }

            if (!LocalizationText.IsConfigured)
            {
                return SupportedLanguage.Korean;
            }

            string code = LocalizationSettings.SelectedLocale?.Identifier.Code;
            return code != null && code.StartsWith("en")
                ? SupportedLanguage.English
                : SupportedLanguage.Korean;
        }

        private void OnLanguageSelected(int index)
        {
            SupportedLanguage language = index == (int)SupportedLanguage.English
                ? SupportedLanguage.English
                : SupportedLanguage.Korean;

            if (localizationService != null)
            {
                localizationService.SetLanguage(language);
                return;
            }

            if (!LocalizationText.IsConfigured)
            {
                return;
            }

            string localeCode = language == SupportedLanguage.English ? "en" : "ko";
            var locale = LocalizationSettings.AvailableLocales.GetLocale(localeCode);
            if (locale != null)
            {
                LocalizationSettings.SelectedLocale = locale;
            }
        }

        private void OnLanguageChanged(SupportedLanguage language)
        {
            if (dropdown == null)
            {
                return;
            }

            dropdown.SetValueWithoutNotify((int)language);
            dropdown.RefreshShownValue();
        }
    }
}
