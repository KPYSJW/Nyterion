using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Nytherion.Core.Utils;
using Nytherion.Data.ScriptableObjects.Enemy;
using Nytherion.Data.ScriptableObjects.Items;
using Nytherion.Data.ScriptableObjects.Progression;
using Nytherion.Data.ScriptableObjects.Relics;
using Nytherion.Data.ScriptableObjects.Skill;
using Nytherion.Data.ScriptableObjects.Stage;
using Nytherion.Data.ScriptableObjects.Weapons;
using Nytherion.UI.Components;
using Nytherion.UI.Controllers;
using Nytherion.UI.Title;
using TMPro;
using UnityEditor;
using UnityEditor.Localization;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Nytherion.Editor.Localization
{
    public static class LocalizationProjectSetup
    {
        private const string LocalizationRoot = "Assets/Nytherion/Localization";
        private const string LocaleFolder = LocalizationRoot + "/Locales";
        private const string TableFolder = LocalizationRoot + "/StringTables";
        private const string LocalizationSettingsPath = LocalizationRoot + "/LocalizationSettings.asset";
        private const string LanguageDropdownPrefabPath = "Assets/Prefabs/UI/Localization/LanguageDropdown.prefab";
        private const string PlayerPreferenceKey = "Nytherion.SelectedLocale";

        private static readonly string[] RuntimeScenePaths =
        {
            "Assets/Scenes/Title.unity",
            "Assets/Scenes/GameScene.unity",
            "Assets/Scenes/Village.unity"
        };

        [MenuItem("Nytherion/Localization/Setup And Migrate")]
        public static void SetupAndMigrate()
        {
            EnsureFolders();
            EnsureLocalizationSettings();
            EnsureLocales();
            ConfigureStartupLocaleSelectors();
            EnsureAllTables();
            PopulateUIEntries();
            PopulateItemEntries();
            PopulateSkillEntries();
            PopulateRelicEntries();
            PopulateProgressionEntries();
            PopulateWorldEntries();
            CreateLanguageDropdownPrefab();
            SetupLanguageControlsInRuntimeScenes();
            MigrateStaticTexts();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Localization] 한국어/영어 Locale, String Table, UI 참조 및 기존 데이터 이전을 완료했습니다.");
        }

        public static void RunFromCommandLine()
        {
            try
            {
                SetupAndMigrate();
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        private static void EnsureFolders()
        {
            EnsureFolder("Assets/Nytherion", "Localization");
            EnsureFolder(LocalizationRoot, "Locales");
            EnsureFolder(LocalizationRoot, "StringTables");
            EnsureFolder("Assets/Prefabs/UI", "Localization");
        }

        private static void EnsureFolder(string parent, string child)
        {
            string path = $"{parent}/{child}";
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parent, child);
            }
        }

        private static void EnsureLocalizationSettings()
        {
            if (LocalizationEditorSettings.ActiveLocalizationSettings != null)
            {
                return;
            }

            LocalizationSettings settings = ScriptableObject.CreateInstance<LocalizationSettings>();
            AssetDatabase.CreateAsset(settings, LocalizationSettingsPath);
            LocalizationEditorSettings.ActiveLocalizationSettings = settings;
            EditorUtility.SetDirty(settings);
        }

        private static void EnsureLocales()
        {
            EnsureLocale(SystemLanguage.Korean, "Korean.asset");
            EnsureLocale(SystemLanguage.English, "English.asset");
        }

        private static Locale EnsureLocale(SystemLanguage language, string fileName)
        {
            Locale locale = LocalizationEditorSettings.GetLocale(language);
            if (locale != null)
            {
                return locale;
            }

            string path = $"{LocaleFolder}/{fileName}";
            locale = AssetDatabase.LoadAssetAtPath<Locale>(path);
            if (locale == null)
            {
                locale = Locale.CreateLocale(language);
                AssetDatabase.CreateAsset(locale, path);
            }

            LocalizationEditorSettings.AddLocale(locale);
            EditorUtility.SetDirty(locale);
            return locale;
        }

        private static void ConfigureStartupLocaleSelectors()
        {
            LocalizationSettings settings = LocalizationEditorSettings.ActiveLocalizationSettings;
            List<IStartupLocaleSelector> selectors = settings.GetStartupLocaleSelectors();
            selectors.Clear();
            selectors.Add(new PlayerPrefLocaleSelector { PlayerPreferenceKey = PlayerPreferenceKey });
            selectors.Add(new SystemLocaleSelector());
            selectors.Add(new SpecificLocaleSelector
            {
                LocaleId = new LocaleIdentifier(LocalizationText.KoreanLocaleCode)
            });
            EditorUtility.SetDirty(settings);
        }

        private static void EnsureAllTables()
        {
            EnsureTable(LocalizationTables.UI);
            EnsureTable(LocalizationTables.Items);
            EnsureTable(LocalizationTables.Skills);
            EnsureTable(LocalizationTables.Relics);
            EnsureTable(LocalizationTables.Progression);
            EnsureTable(LocalizationTables.World);
        }

        private static StringTableCollection EnsureTable(string tableName)
        {
            StringTableCollection collection = LocalizationEditorSettings.GetStringTableCollection(tableName);
            if (collection == null)
            {
                collection = LocalizationEditorSettings.CreateStringTableCollection(tableName, TableFolder);
            }

            foreach (StringTable table in collection.StringTables)
            {
                LocalizationEditorSettings.SetPreloadTableFlag(table, true);
            }

            return collection;
        }

        private static void PopulateUIEntries()
        {
            foreach (TranslationEntry entry in LocalizationTranslationCatalog.UIEntries.Values)
            {
                SetEntry(LocalizationTables.UI, entry.Key, entry.Korean, entry.English);
            }
        }

        private static void PopulateItemEntries()
        {
            foreach (ItemData item in LoadAssets<ItemData>("Assets/Nytherion/Data/ScriptableObjects"))
            {
                if (item == null || string.IsNullOrWhiteSpace(item.ID))
                {
                    continue;
                }

                string koreanName = item.itemName_KR;
                if (string.IsNullOrWhiteSpace(koreanName) &&
                    LocalizationTranslationCatalog.ItemKoreanNames.TryGetValue(item.itemName_EN, out string translatedName))
                {
                    koreanName = translatedName;
                }

                string englishName = !string.IsNullOrWhiteSpace(item.itemName_EN)
                    ? item.itemName_EN
                    : koreanName;

                string koreanDescription = item.description_KR;
                string englishDescription = item.description_EN;
                if (string.IsNullOrWhiteSpace(koreanDescription) && string.IsNullOrWhiteSpace(englishDescription))
                {
                    CreateDefaultItemDescriptions(item, koreanName, englishName, out koreanDescription, out englishDescription);
                }

                SetEntry(
                    LocalizationTables.Items,
                    LocalizationKeys.ItemName(item.ID),
                    koreanName,
                    englishName);
                SetEntry(
                    LocalizationTables.Items,
                    LocalizationKeys.ItemDescription(item.ID),
                    koreanDescription,
                    englishDescription);
            }
        }

        private static void CreateDefaultItemDescriptions(
            ItemData item,
            string koreanName,
            string englishName,
            out string koreanDescription,
            out string englishDescription)
        {
            if (item is WeaponData weapon)
            {
                string damage = weapon.damage.ToString("0.##", CultureInfo.InvariantCulture);
                string interval = weapon.cooldown.ToString("0.##", CultureInfo.InvariantCulture);
                koreanDescription = $"{koreanName} 무기입니다. 기본 공격력 {damage}, 공격 간격 {interval}초.";
                englishDescription = $"{englishName} weapon. Base damage {damage}, attack interval {interval}s.";
                return;
            }

            if (item is EquipmentData)
            {
                koreanDescription = $"{koreanName} 장비 아이템입니다.";
                englishDescription = $"{englishName} equipment item.";
                return;
            }

            koreanDescription = $"{koreanName} 아이템입니다.";
            englishDescription = $"{englishName} item.";
        }

        private static void PopulateSkillEntries()
        {
            foreach (SkillData skill in LoadAssets<SkillData>("Assets/Nytherion/Data/ScriptableObjects/Skill"))
            {
                if (skill == null || string.IsNullOrWhiteSpace(skill.skillID))
                {
                    continue;
                }

                (string Korean, string English) names =
                    LocalizationTranslationCatalog.Skills.TryGetValue(skill.skillID, out var translatedNames)
                        ? translatedNames
                        : (skill.skillName, skill.skillName);
                string englishDescription =
                    LocalizationTranslationCatalog.SkillEnglishDescriptions.TryGetValue(skill.skillID, out string translatedDescription)
                        ? translatedDescription
                        : skill.description;

                SetEntry(
                    LocalizationTables.Skills,
                    LocalizationKeys.SkillName(skill.skillID),
                    names.Korean,
                    names.English);
                SetEntry(
                    LocalizationTables.Skills,
                    LocalizationKeys.SkillDescription(skill.skillID),
                    skill.description,
                    englishDescription);
            }
        }

        private static void PopulateRelicEntries()
        {
            foreach (RelicData relic in LoadAssets<RelicData>("Assets/Nytherion/Data/ScriptableObjects/Relics"))
            {
                if (relic == null || string.IsNullOrWhiteSpace(relic.relicName))
                {
                    continue;
                }

                string koreanDescription = relic.description_KR;
                string englishDescription = relic.description_EN;
                if (relic.relicName == "Mystical Crystal")
                {
                    koreanDescription = "플레이어의 모든 기본 스탯(공격력, 방어력, 체력 등)을 6% 증가시킵니다.";
                }

                if (string.IsNullOrWhiteSpace(englishDescription) &&
                    LocalizationTranslationCatalog.RelicEnglishDescriptions.TryGetValue(
                        relic.relicName,
                        out string translatedDescription))
                {
                    englishDescription = translatedDescription;
                }

                SetEntry(
                    LocalizationTables.Relics,
                    LocalizationKeys.RelicName(relic.relicName),
                    relic.koreanName,
                    relic.relicName);
                SetEntry(
                    LocalizationTables.Relics,
                    LocalizationKeys.RelicDescription(relic.relicName),
                    koreanDescription,
                    englishDescription);
            }

            foreach (RelicSetBonusData setBonus in LoadAssets<RelicSetBonusData>("Assets/Nytherion/Data/ScriptableObjects/Relics"))
            {
                if (setBonus == null || string.IsNullOrWhiteSpace(setBonus.synergySeriesId))
                {
                    continue;
                }

                SetEntry(
                    LocalizationTables.Relics,
                    LocalizationKeys.RelicSetName(setBonus.synergySeriesId),
                    setBonus.setName_KR,
                    setBonus.setName_EN);
                SetEntry(
                    LocalizationTables.Relics,
                    LocalizationKeys.RelicSetDescription(setBonus.synergySeriesId),
                    setBonus.description_KR,
                    setBonus.description_EN);
            }

            foreach (RelicTranscendenceData transcendence in
                     LoadAssets<RelicTranscendenceData>("Assets/Nytherion/Data/ScriptableObjects/Relics"))
            {
                if (transcendence == null)
                {
                    continue;
                }

                SetEntry(
                    LocalizationTables.Relics,
                    LocalizationKeys.RelicTranscendenceName(transcendence.name),
                    transcendence.effectName_KR,
                    transcendence.effectName_EN);
                SetEntry(
                    LocalizationTables.Relics,
                    LocalizationKeys.RelicTranscendenceDescription(transcendence.name),
                    transcendence.description_KR,
                    transcendence.description_EN);
            }
        }

        private static void PopulateProgressionEntries()
        {
            foreach (MilestoneData milestone in
                     LoadAssets<MilestoneData>("Assets/Nytherion/Data/ScriptableObjects/Progression"))
            {
                if (milestone == null || string.IsNullOrWhiteSpace(milestone.milestoneID))
                {
                    continue;
                }

                TranslationEntry title =
                    LocalizationTranslationCatalog.Milestones.TryGetValue(milestone.milestoneID, out TranslationEntry translatedTitle)
                        ? translatedTitle
                        : new TranslationEntry(milestone.milestoneID, milestone.title, milestone.title);

                string koreanDescription = milestone.description;
                string englishDescription;
                if (LocalizationTranslationCatalog.MilestoneEnglishDescriptions.TryGetValue(
                        milestone.milestoneID,
                        out string translatedDescription))
                {
                    englishDescription = translatedDescription;
                }
                else
                {
                    koreanDescription = string.IsNullOrWhiteSpace(koreanDescription)
                        ? $"{title.Korean} 스킬을 해금합니다."
                        : koreanDescription;
                    englishDescription = $"Unlock the {title.English} skill.";
                }

                SetEntry(
                    LocalizationTables.Progression,
                    LocalizationKeys.MilestoneTitle(milestone.milestoneID),
                    title.Korean,
                    title.English);
                SetEntry(
                    LocalizationTables.Progression,
                    LocalizationKeys.MilestoneDescription(milestone.milestoneID),
                    koreanDescription,
                    englishDescription);
            }
        }

        private static void PopulateWorldEntries()
        {
            foreach (StageData stage in LoadAssets<StageData>("Assets/Nytherion/Data/ScriptableObjects/Stage"))
            {
                if (stage == null)
                {
                    continue;
                }

                SetEntry(
                    LocalizationTables.World,
                    LocalizationKeys.StageName(stage.name),
                    stage.stageName,
                    stage.stageName);
            }

            foreach (EnemyData enemy in LoadAssets<EnemyData>("Assets/Nytherion/Data/ScriptableObjects/Enemy"))
            {
                if (enemy == null)
                {
                    continue;
                }

                string koreanName = LocalizationTranslationCatalog.EnemyKoreanNames.TryGetValue(
                    enemy.enemyName,
                    out string translatedName)
                    ? translatedName
                    : enemy.enemyName;
                SetEntry(
                    LocalizationTables.World,
                    LocalizationKeys.EnemyName(enemy.name),
                    koreanName,
                    enemy.enemyName);
            }
        }

        private static void SetEntry(
            string tableName,
            string entryKey,
            string korean,
            string english)
        {
            if (string.IsNullOrWhiteSpace(entryKey))
            {
                return;
            }

            StringTableCollection collection = EnsureTable(tableName);
            StringTable koreanTable = collection.GetTable(new LocaleIdentifier(LocalizationText.KoreanLocaleCode)) as StringTable;
            StringTable englishTable = collection.GetTable(new LocaleIdentifier(LocalizationText.EnglishLocaleCode)) as StringTable;

            SetTableValue(koreanTable, entryKey, korean);
            SetTableValue(englishTable, entryKey, english);

            EditorUtility.SetDirty(collection.SharedData);
            EditorUtility.SetDirty(koreanTable);
            EditorUtility.SetDirty(englishTable);
        }

        private static void SetTableValue(StringTable table, string key, string value)
        {
            if (table == null)
            {
                throw new InvalidOperationException($"[Localization] '{key}' 항목을 기록할 Locale 테이블이 없습니다.");
            }

            StringTableEntry entry = table.GetEntry(key);
            if (entry == null)
            {
                entry = table.AddEntry(key, value ?? string.Empty);
            }
            else
            {
                entry.Value = value ?? string.Empty;
            }

            entry.IsSmart = !string.IsNullOrEmpty(value) && value.Contains("{0");
        }

        private static void CreateLanguageDropdownPrefab()
        {
            Scene scene = EditorSceneManager.OpenScene("Assets/Scenes/GameScene.unity", OpenSceneMode.Single);
            GameSceneUIRefs uiRefs = Object.FindObjectOfType<GameSceneUIRefs>(true);
            if (uiRefs?.ResolutionDropdown == null)
            {
                throw new InvalidOperationException("[Localization] GameScene의 ResolutionDropdown을 찾을 수 없습니다.");
            }

            GameObject temporary = Object.Instantiate(uiRefs.ResolutionDropdown.gameObject);
            temporary.name = "LanguageDropdown";
            temporary.transform.SetParent(null, false);
            PrefabUtility.SaveAsPrefabAsset(temporary, LanguageDropdownPrefabPath);
            Object.DestroyImmediate(temporary);
            EditorSceneManager.CloseScene(scene, true);
        }

        private static void SetupLanguageControlsInRuntimeScenes()
        {
            SetupGameLanguageControls("Assets/Scenes/GameScene.unity");
            SetupGameLanguageControls("Assets/Scenes/Village.unity");
            SetupTitleLanguageControls();
        }

        private static void SetupGameLanguageControls(string scenePath)
        {
            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            GameSceneUIRefs uiRefs = Object.FindObjectOfType<GameSceneUIRefs>(true);
            if (uiRefs?.ResolutionDropdown == null)
            {
                EditorSceneManager.CloseScene(scene, true);
                return;
            }

            SerializedObject serializedRefs = new SerializedObject(uiRefs);
            SerializedProperty languageProperty = serializedRefs.FindProperty("languageDropdown");
            TMP_Dropdown languageDropdown = languageProperty.objectReferenceValue as TMP_Dropdown;

            if (languageDropdown == null)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(LanguageDropdownPrefabPath);
                GameObject instance = PrefabUtility.InstantiatePrefab(
                    prefab,
                    uiRefs.ResolutionDropdown.transform.parent) as GameObject;
                instance.name = "LanguageDropdown";
                RectTransform dropdownRect = instance.GetComponent<RectTransform>();
                dropdownRect.anchoredPosition = uiRefs.ResolutionDropdown.GetComponent<RectTransform>().anchoredPosition +
                                                new Vector2(0f, -60f);
                languageDropdown = instance.GetComponent<TMP_Dropdown>();
                languageProperty.objectReferenceValue = languageDropdown;
                serializedRefs.ApplyModifiedPropertiesWithoutUndo();

                Transform resolutionLabel = uiRefs.ResolutionDropdown.transform.parent.Find("ResolutionText");
                if (resolutionLabel != null)
                {
                    GameObject label = Object.Instantiate(
                        resolutionLabel.gameObject,
                        resolutionLabel.parent);
                    label.name = "LanguageText";
                    RectTransform labelRect = label.GetComponent<RectTransform>();
                    labelRect.anchoredPosition = new Vector2(
                        labelRect.anchoredPosition.x,
                        dropdownRect.anchoredPosition.y);
                    TMP_Text labelText = label.GetComponent<TMP_Text>();
                    if (labelText != null)
                    {
                        LocalizedTMPText localizedText = label.GetComponent<LocalizedTMPText>();
                        if (localizedText == null)
                        {
                            localizedText = label.AddComponent<LocalizedTMPText>();
                        }
                        localizedText.Configure(
                            LocalizationTables.UI,
                            "ui.settings.language",
                            "언어 :",
                            "Language :");
                    }
                }
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static void SetupTitleLanguageControls()
        {
            Scene scene = EditorSceneManager.OpenScene("Assets/Scenes/Title.unity", OpenSceneMode.Single);
            TitleMenuManager menuManager = Object.FindObjectOfType<TitleMenuManager>(true);
            Canvas canvas = Object.FindObjectOfType<Canvas>(true);
            if (menuManager == null || canvas == null)
            {
                EditorSceneManager.CloseScene(scene, true);
                return;
            }

            SerializedObject serializedMenu = new SerializedObject(menuManager);
            SerializedProperty settingsPanelProperty = serializedMenu.FindProperty("settingsPanel");
            GameObject selector = settingsPanelProperty.objectReferenceValue as GameObject;
            if (selector == null)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(LanguageDropdownPrefabPath);
                selector = PrefabUtility.InstantiatePrefab(prefab, canvas.transform) as GameObject;
                selector.name = "TitleLanguageDropdown";
                RectTransform rectTransform = selector.GetComponent<RectTransform>();
                rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
                rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                rectTransform.anchoredPosition = new Vector2(330f, -240f);
                if (selector.GetComponent<LanguageDropdownController>() == null)
                {
                    selector.AddComponent<LanguageDropdownController>();
                }

                settingsPanelProperty.objectReferenceValue = selector;
                serializedMenu.ApplyModifiedPropertiesWithoutUndo();
                selector.SetActive(false);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static void MigrateStaticTexts()
        {
            foreach (string scenePath in RuntimeScenePaths)
            {
                Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    MigrateTextsInHierarchy(root);
                }

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }

            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs" }))
            {
                string prefabPath = AssetDatabase.GUIDToAssetPath(guid);
                GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
                bool changed = MigrateTextsInHierarchy(root);
                if (changed)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                }
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static bool MigrateTextsInHierarchy(GameObject root)
        {
            bool changed = false;
            foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
            {
                string source = NormalizeStaticText(text.text);
                if (!LocalizationTranslationCatalog.StaticTextBySource.TryGetValue(
                        source,
                        out TranslationEntry translation))
                {
                    continue;
                }

                LocalizedTMPText localizedText = text.GetComponent<LocalizedTMPText>();
                if (localizedText == null)
                {
                    localizedText = text.gameObject.AddComponent<LocalizedTMPText>();
                }

                localizedText.Configure(
                    LocalizationTables.UI,
                    translation.Key,
                    translation.Korean,
                    translation.English);
                EditorUtility.SetDirty(text.gameObject);
                changed = true;
            }

            return changed;
        }

        private static string NormalizeStaticText(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            return value.Trim().Trim('\'', '"').Trim();
        }

        private static IEnumerable<T> LoadAssets<T>(string folder) where T : Object
        {
            return AssetDatabase.FindAssets($"t:{typeof(T).Name}", new[] { folder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(path => AssetDatabase.LoadAssetAtPath<T>(path))
                .Where(asset => asset != null);
        }
    }
}
