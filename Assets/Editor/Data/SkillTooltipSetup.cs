using System;
using System.IO;
using Nytherion.UI.Skill;
using Nytherion.Core.Utils;
using Nytherion.Editor.Localization;
using Nytherion.Data.ScriptableObjects.Skill;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine.Localization.Tables;
using UnityEngine;
using UnityEngine.UI;

namespace Nytherion.Editor
{
    /// <summary>스킬 전용 툴팁을 Unity 에디터에서 생성하고 컨트롤러 프리팹에 연결한다.</summary>
    [InitializeOnLoad]
    public static class SkillTooltipSetup
    {
        public const string Output = "output/skill-tooltip";
        public const string PrefabPath = "Assets/Prefabs/UI/SkillTooltip.prefab";
        private const string ControllerPath = "Assets/Prefabs/Infrastructure/Managers/SkillUIController.prefab";

        static SkillTooltipSetup() { EditorApplication.update += Update; }

        private static void Update()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
                EditorApplication.isPlayingOrWillChangePlaymode) return;
            string descriptionRequest = Output + "/descriptions.request";
            if (File.Exists(descriptionRequest))
            {
                File.Delete(descriptionRequest);
                try { SimplifyDescriptions(); }
                catch (Exception error)
                {
                    File.WriteAllText(Output + "/descriptions.txt", "FAIL " + error);
                    Debug.LogException(error);
                }
                return;
            }
            string layoutRequest = Output + "/layout.request";
            if (File.Exists(layoutRequest))
            {
                File.Delete(layoutRequest);
                GameObject tooltip = PrefabUtility.LoadPrefabContents(PrefabPath);
                try
                {
                    RectOffset padding = tooltip.GetComponent<VerticalLayoutGroup>().padding;
                    padding.left = padding.right = 48;
                    padding.top = 44;
                    PrefabUtility.SaveAsPrefabAsset(tooltip, PrefabPath);
                }
                finally { PrefabUtility.UnloadPrefabContents(tooltip); }
                SkillTooltipVerification.RefreshPreview();
                return;
            }
            string request = Output + "/setup.request";
            if (!File.Exists(request)) return;
            File.Delete(request);
            try { Create(); }
            catch (Exception error)
            {
                File.WriteAllText(Output + "/setup.txt", "FAIL " + error);
                Debug.LogException(error);
            }
        }

        [MenuItem("Tools/Nytherion/Skills/Create Skill Tooltip")]
        public static void Create()
        {
            var collection = LocalizationEditorSettings.GetStringTableCollection(LocalizationTables.UI);
            if (collection != null)
            {
                foreach (var table in collection.StringTables)
                {
                    bool korean = table.LocaleIdentifier.Code.StartsWith("ko", StringComparison.OrdinalIgnoreCase);
                    foreach (string key in new[] { "ui.skill_tooltip.progress", "ui.skill_tooltip.stats" })
                    {
                        var entry = LocalizationTranslationCatalog.UIEntries[key];
                        table.AddEntry(key, korean ? entry.Korean : entry.English).IsSmart = true;
                    }
                    EditorUtility.SetDirty(table);
                    AssetDatabase.SaveAssetIfDirty(table);
                }
                EditorUtility.SetDirty(collection.SharedData);
                AssetDatabase.SaveAssetIfDirty(collection.SharedData);
            }
            const string imagePath = "Assets/Nytherion/Art/UI/SkillTooltip.png";
            var importer = (TextureImporter)AssetImporter.GetAtPath(imagePath);
            importer.spriteBorder = new Vector4(4f, 4f, 4f, 4f);
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            Sprite background = AssetDatabase.LoadAssetAtPath<Sprite>(imagePath);
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                AssetDatabase.GUIDToAssetPath("3ea9213abafbd9147b85db3b64237362"));
            if (background == null || font == null) throw new InvalidOperationException("툴팁 이미지 또는 기존 UI 폰트가 없습니다.");

            GameObject root = new GameObject("SkillTooltip", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasGroup), typeof(Image), typeof(VerticalLayoutGroup), typeof(SkillTooltip));
            try
            {
                RectTransform rect = (RectTransform)root.transform;
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0f, 1f);
                rect.sizeDelta = new Vector2(400f, 640f);
                Canvas canvas = root.GetComponent<Canvas>();
                canvas.overrideSorting = true;
                canvas.sortingOrder = 1000;
                CanvasGroup group = root.GetComponent<CanvasGroup>();
                group.alpha = 0f;
                group.interactable = group.blocksRaycasts = false;
                Image image = root.GetComponent<Image>();
                image.sprite = background;
                image.type = Image.Type.Sliced;
                image.pixelsPerUnitMultiplier = 0.5f;
                image.raycastTarget = false;
                VerticalLayoutGroup layout = root.GetComponent<VerticalLayoutGroup>();
                layout.padding = new RectOffset(48, 48, 44, 32);
                layout.spacing = 16f;
                layout.childAlignment = TextAnchor.UpperCenter;
                layout.childControlWidth = layout.childControlHeight = true;
                layout.childForceExpandWidth = true;
                layout.childForceExpandHeight = false;

                var title = CreateText(root.transform, "Name", font, 30f, TextAlignmentOptions.Center);
                var iconObject = new GameObject("Icon", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
                iconObject.transform.SetParent(root.transform, false);
                Image icon = iconObject.GetComponent<Image>();
                icon.preserveAspect = true;
                icon.raycastTarget = false;
                iconObject.GetComponent<LayoutElement>().preferredHeight = 96f;
                var progress = CreateText(root.transform, "Progress", font, 22f, TextAlignmentOptions.Center);
                progress.color = new Color32(180, 197, 240, 255);
                var stats = CreateText(root.transform, "Stats", font, 24f, TextAlignmentOptions.Left);
                var description = CreateText(root.transform, "Description", font, 24f, TextAlignmentOptions.Left);
                SerializedObject serialized = new SerializedObject(root.GetComponent<SkillTooltip>());
                Assign(serialized, "canvasGroup", group);
                Assign(serialized, "nameText", title);
                Assign(serialized, "skillIcon", icon);
                Assign(serialized, "progressText", progress);
                Assign(serialized, "statsText", stats);
                Assign(serialized, "descriptionText", description);
                serialized.ApplyModifiedPropertiesWithoutUndo();
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                GameObject controller = PrefabUtility.LoadPrefabContents(ControllerPath);
                try
                {
                    var controllerSerialized = new SerializedObject(controller.GetComponent<SkillUIController>());
                    Assign(controllerSerialized, "skillTooltipPrefab", prefab.GetComponent<SkillTooltip>());
                    controllerSerialized.ApplyModifiedPropertiesWithoutUndo();
                    PrefabUtility.SaveAsPrefabAsset(controller, ControllerPath);
                }
                finally { PrefabUtility.UnloadPrefabContents(controller); }
                Directory.CreateDirectory(Output);
                File.WriteAllText(Output + "/setup.txt", "PASS: 스킬 전용 프리팹 생성 및 SkillUIController Inspector 참조 연결 완료");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [MenuItem("Tools/Nytherion/Skills/Simplify Skill Descriptions")]
        public static void SimplifyDescriptions()
        {
            var results = new List<string>();
            var collection = LocalizationEditorSettings.GetStringTableCollection(LocalizationTables.Skills);
            foreach (string guid in AssetDatabase.FindAssets("t:SkillData",
                new[] { "Assets/Nytherion/Data/ScriptableObjects/Skill" }))
            {
                SkillData skill = AssetDatabase.LoadAssetAtPath<SkillData>(AssetDatabase.GUIDToAssetPath(guid));
                if (!LocalizationTranslationCatalog.SkillKoreanDescriptions.TryGetValue(skill.skillID, out string description))
                    throw new InvalidOperationException("간략 설명이 없는 스킬: " + skill.skillID);
                skill.description = description;
                EditorUtility.SetDirty(skill);
                AssetDatabase.SaveAssetIfDirty(skill);
                if (collection != null)
                {
                    foreach (StringTable table in collection.StringTables)
                    {
                        bool korean = table.LocaleIdentifier.Code.StartsWith("ko", StringComparison.OrdinalIgnoreCase);
                        table.AddEntry(LocalizationKeys.SkillDescription(skill.skillID), korean
                            ? description : LocalizationTranslationCatalog.SkillEnglishDescriptions[skill.skillID]);
                        EditorUtility.SetDirty(table);
                        AssetDatabase.SaveAssetIfDirty(table);
                    }
                }
                results.Add(skill.skillID + ": " + description);
            }
            if (collection != null)
            {
                EditorUtility.SetDirty(collection.SharedData);
                AssetDatabase.SaveAssetIfDirty(collection.SharedData);
            }
            var uiCollection = LocalizationEditorSettings.GetStringTableCollection(LocalizationTables.UI);
            if (uiCollection != null)
            {
                var entry = LocalizationTranslationCatalog.UIEntries["ui.skill_tooltip.stats"];
                foreach (StringTable table in uiCollection.StringTables)
                {
                    table.AddEntry(entry.Key, table.LocaleIdentifier.Code.StartsWith("ko", StringComparison.OrdinalIgnoreCase)
                        ? entry.Korean : entry.English).IsSmart = true;
                    EditorUtility.SetDirty(table);
                    AssetDatabase.SaveAssetIfDirty(table);
                }
                EditorUtility.SetDirty(uiCollection.SharedData);
                AssetDatabase.SaveAssetIfDirty(uiCollection.SharedData);
            }
            Directory.CreateDirectory(Output);
            File.WriteAllLines(Output + "/descriptions.txt", results);
            GenerateSkillRelics.UpdateAllSkillRelicDescriptions(silent: true);
            SkillTooltipVerification.RefreshPreview();
        }

        private static TextMeshProUGUI CreateText(Transform parent, string name, TMP_FontAsset font,
            float size, TextAlignmentOptions alignment)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<TextMeshProUGUI>();
            text.font = font;
            text.fontSize = size;
            text.color = Color.white;
            text.alignment = alignment;
            text.enableWordWrapping = true;
            text.raycastTarget = false;
            return text;
        }

        private static void Assign(SerializedObject target, string field, UnityEngine.Object value)
        {
            target.FindProperty(field).objectReferenceValue = value;
        }
    }
}
