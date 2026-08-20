using System.Collections.Generic;
using Nytherion.Core.Utils;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Tables;

namespace Nytherion.Editor.Localization
{
    public static class LocalizationAudit
    {
        private static readonly string[] RequiredTables =
        {
            LocalizationTables.UI,
            LocalizationTables.Items,
            LocalizationTables.Skills,
            LocalizationTables.Relics,
            LocalizationTables.Progression,
            LocalizationTables.World
        };

        [MenuItem("Nytherion/Localization/Validate Korean And English")]
        public static void ValidateAndLog()
        {
            List<string> issues = CollectIssues();
            if (issues.Count == 0)
            {
                Debug.Log("[Localization] 모든 String Table 항목에 한국어와 영어가 등록돼 있습니다.");
                return;
            }

            Debug.LogError("[Localization] 번역 누락 항목:\n- " + string.Join("\n- ", issues));
        }

        public static List<string> CollectIssues()
        {
            List<string> issues = new List<string>();

            if (LocalizationEditorSettings.ActiveLocalizationSettings == null)
            {
                issues.Add("활성 LocalizationSettings가 없습니다.");
                return issues;
            }

            if (LocalizationEditorSettings.GetLocale(new LocaleIdentifier(LocalizationText.KoreanLocaleCode)) == null)
            {
                issues.Add("한국어(ko) Locale이 없습니다.");
            }

            if (LocalizationEditorSettings.GetLocale(new LocaleIdentifier(LocalizationText.EnglishLocaleCode)) == null)
            {
                issues.Add("영어(en) Locale이 없습니다.");
            }

            foreach (string tableName in RequiredTables)
            {
                ValidateTable(tableName, issues);
            }

            return issues;
        }

        private static void ValidateTable(string tableName, List<string> issues)
        {
            StringTableCollection collection = LocalizationEditorSettings.GetStringTableCollection(tableName);
            if (collection == null)
            {
                issues.Add($"{tableName}: String Table Collection이 없습니다.");
                return;
            }

            StringTable koreanTable = collection.GetTable(
                new LocaleIdentifier(LocalizationText.KoreanLocaleCode)) as StringTable;
            StringTable englishTable = collection.GetTable(
                new LocaleIdentifier(LocalizationText.EnglishLocaleCode)) as StringTable;

            if (koreanTable == null || englishTable == null)
            {
                issues.Add($"{tableName}: 한국어 또는 영어 테이블이 없습니다.");
                return;
            }

            foreach (SharedTableData.SharedTableEntry sharedEntry in collection.SharedData.Entries)
            {
                StringTableEntry koreanEntry = koreanTable.GetEntry(sharedEntry.Id);
                StringTableEntry englishEntry = englishTable.GetEntry(sharedEntry.Id);

                if (string.IsNullOrWhiteSpace(koreanEntry?.Value))
                {
                    issues.Add($"{tableName}/{sharedEntry.Key}: 한국어 번역 누락");
                }

                if (string.IsNullOrWhiteSpace(englishEntry?.Value))
                {
                    issues.Add($"{tableName}/{sharedEntry.Key}: 영어 번역 누락");
                }
            }
        }
    }

    public sealed class LocalizationBuildValidator : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            List<string> issues = LocalizationAudit.CollectIssues();
            if (issues.Count > 0)
            {
                throw new BuildFailedException(
                    "한국어/영어 번역 누락으로 빌드를 중단합니다.\n- " + string.Join("\n- ", issues));
            }
        }
    }
}
