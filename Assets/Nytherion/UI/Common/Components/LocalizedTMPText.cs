using System.Collections;
using Nytherion.Core.Utils;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

namespace Nytherion.UI.Components
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TMP_Text))]
    public class LocalizedTMPText : MonoBehaviour
    {
        [SerializeField] private string tableName = LocalizationTables.UI;
        [SerializeField] private string entryKey;
        [SerializeField, TextArea] private string koreanFallback;
        [SerializeField, TextArea] private string englishFallback;

        private TMP_Text targetText;
        private Coroutine initializationCoroutine;
        private bool isLocalizationSubscribed;

        private void Awake()
        {
            targetText = GetComponent<TMP_Text>();
        }

        private void OnEnable()
        {
            LocalizationText.LanguageChanged += OnTemporaryLanguageChanged;
            Refresh();

            if (!LocalizationText.IsConfigured)
            {
                return;
            }

            LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;
            isLocalizationSubscribed = true;

            if (!LocalizationSettings.InitializationOperation.IsDone)
            {
                initializationCoroutine = StartCoroutine(RefreshAfterInitialization());
            }
        }

        private void OnDisable()
        {
            LocalizationText.LanguageChanged -= OnTemporaryLanguageChanged;

            if (isLocalizationSubscribed)
            {
                LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;
                isLocalizationSubscribed = false;
            }

            if (initializationCoroutine != null)
            {
                StopCoroutine(initializationCoroutine);
                initializationCoroutine = null;
            }
        }

        public void Configure(
            string newTableName,
            string newEntryKey,
            string newKoreanFallback,
            string newEnglishFallback)
        {
            tableName = newTableName;
            entryKey = newEntryKey;
            koreanFallback = newKoreanFallback;
            englishFallback = newEnglishFallback;
            Refresh();
        }

        public void Refresh()
        {
            if (targetText == null)
            {
                targetText = GetComponent<TMP_Text>();
            }

            if (targetText == null || string.IsNullOrWhiteSpace(entryKey))
            {
                return;
            }

            targetText.text = LocalizationText.Get(
                tableName,
                entryKey,
                koreanFallback,
                englishFallback);
        }

        private IEnumerator RefreshAfterInitialization()
        {
            if (!LocalizationText.IsConfigured)
            {
                initializationCoroutine = null;
                yield break;
            }

            yield return LocalizationSettings.InitializationOperation;
            initializationCoroutine = null;
            Refresh();
        }

        private void OnLocaleChanged(Locale _)
        {
            Refresh();
        }

        private void OnTemporaryLanguageChanged()
        {
            Refresh();
        }
    }
}
