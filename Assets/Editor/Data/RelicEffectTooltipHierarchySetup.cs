using System;
using System.IO;
using System.Linq;
using Nytherion.Data.ScriptableObjects.Relics;
using Nytherion.UI.RelicBoard;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Nytherion.Editor
{
    /// <summary>효과 현황 툴팁을 편집 가능한 씬 UI로 생성하고 기존 툴팁 진입점에 연결한다.</summary>
    [InitializeOnLoad]
    public static class RelicEffectTooltipHierarchySetup
    {
        public const string Output = "output/relic-effect-tooltip";
        private const string ScenePath = "Assets/Scenes/GameScene.unity";

        static RelicEffectTooltipHierarchySetup() { EditorApplication.update += Update; }

        private static void Update()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
                EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (File.Exists(Output + "/requirement-icons.request"))
            {
                File.Delete(Output + "/requirement-icons.request");
                try { ApplyRequirementIcons(); }
                catch (Exception error)
                {
                    File.WriteAllText(Output + "/requirement-icons.txt", "FAIL: " + error);
                    Debug.LogException(error);
                }
                return;
            }
            if (File.Exists(Output + "/set-title-icon.request"))
            {
                File.Delete(Output + "/set-title-icon.request");
                try { ApplySetTitleIcon(); }
                catch (Exception error)
                {
                    File.WriteAllText(Output + "/set-title-icon.txt", "FAIL: " + error);
                    Debug.LogException(error);
                }
                return;
            }
            if (File.Exists(Output + "/transcendence-style.request"))
            {
                File.Delete(Output + "/transcendence-style.request");
                try { ApplyTranscendenceStyle(); }
                catch (Exception error)
                {
                    File.WriteAllText(Output + "/transcendence-style.txt", "FAIL: " + error);
                    Debug.LogException(error);
                }
                return;
            }
            if (!File.Exists(Output + "/hierarchy.request")) return;
            File.Delete(Output + "/hierarchy.request");
            try { Create(); }
            catch (Exception error)
            {
                File.WriteAllText(Output + "/hierarchy-setup.txt", "FAIL: " + error);
                Debug.LogException(error);
            }
        }

        [MenuItem("Tools/Nytherion/유물/초월 조건 세트 아이콘 적용")]
        public static void ApplyRequirementIcons()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("플레이를 종료한 뒤 실행해 주세요.");
            Directory.CreateDirectory(Output);
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            if (!scene.IsValid() || !scene.isLoaded)
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            RelicTooltip owner = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<RelicTooltip>(true)).Single();
            var ownerSerialized = new SerializedObject(owner);
            var tooltip = (RelicEffectTooltip)ownerSerialized.FindProperty("transcendenceEffectTooltip").objectReferenceValue;
            if (tooltip == null) throw new InvalidOperationException("초월 효과 툴팁 참조가 없습니다.");
            string backup = Output + "/GameScene-before-requirement-icons.unity";
            if (!File.Exists(backup)) EditorSceneManager.SaveScene(scene, backup, true);
            Undo.RecordObject(tooltip, "초월 조건 아이콘 64×64 적용");
            var tooltipSerialized = new SerializedObject(tooltip);
            tooltipSerialized.FindProperty("requirementIconSize").floatValue = 64f;
            tooltipSerialized.ApplyModifiedProperties();
            ConfigureRequirementIconTemplate(tooltip);
            var iconTemplate = (Image)tooltipSerialized.FindProperty("requirementIconTemplate").objectReferenceValue;
            if (iconTemplate != null)
            {
                Undo.RecordObject(iconTemplate.rectTransform, "초월 조건 아이콘 템플릿 크기 적용");
                iconTemplate.rectTransform.sizeDelta = new Vector2(64f, 64f);
            }
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("GameScene 저장에 실패했습니다.");
            File.WriteAllText(Output + "/requirement-icons.txt", "PASS: 초월 조건 아이콘 64×64·텍스트 들여쓰기·본문 배치 유지");
        }

        [MenuItem("Tools/Nytherion/유물/세트 툴팁 제목 아이콘 적용")]
        public static void ApplySetTitleIcon()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("플레이를 종료한 뒤 실행해 주세요.");
            Directory.CreateDirectory(Output);
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            if (!scene.IsValid() || !scene.isLoaded)
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            RelicTooltip owner = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<RelicTooltip>(true)).Single();
            var ownerSerialized = new SerializedObject(owner);
            var tooltip = (RelicEffectTooltip)ownerSerialized.FindProperty("setEffectTooltip").objectReferenceValue;
            if (tooltip == null) throw new InvalidOperationException("세트 효과 툴팁 참조가 없습니다.");
            string backup = Output + "/GameScene-before-set-title-icon.unity";
            if (!File.Exists(backup)) EditorSceneManager.SaveScene(scene, backup, true);
            ConfigureSetTitleIcon(tooltip);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("GameScene 저장에 실패했습니다.");
            File.WriteAllText(Output + "/set-title-icon.txt", "PASS: 세트 툴팁 제목 아이콘 연결·편집한 제목과 본문 배치 유지");
        }

        [MenuItem("Tools/Nytherion/유물/초월 이름 색상·툴팁 아이콘 적용")]
        public static void ApplyTranscendenceStyle()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("플레이를 종료한 뒤 실행해 주세요.");

            Directory.CreateDirectory(Output);
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            if (!scene.IsValid() || !scene.isLoaded)
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            RelicTooltip owner = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<RelicTooltip>(true)).Single();
            var ownerSerialized = new SerializedObject(owner);
            var tooltip = (RelicEffectTooltip)ownerSerialized.FindProperty("transcendenceEffectTooltip").objectReferenceValue;
            if (tooltip == null) throw new InvalidOperationException("초월 효과 툴팁 참조가 없습니다.");

            // 사용자가 편집 중이던 씬도 별도로 보존한다.
            string backup = Output + "/GameScene-before-transcendence-style.unity";
            if (!File.Exists(backup)) EditorSceneManager.SaveScene(scene, backup, true);

            var colors = new (string asset, Color32 color)[]
            {
                ("BloodFortressTranscendence", new Color32(255, 102, 127, 255)),
                ("CorrosiveFlameTranscendence", new Color32(182, 230, 87, 255)),
                ("CrystalBallisticsTranscendence", new Color32(105, 216, 255, 255)),
                ("GoldenRushTranscendence", new Color32(255, 210, 74, 255)),
                ("LegionTranscendence", new Color32(184, 140, 255, 255)),
                ("LightningPursuitTranscendence", new Color32(126, 155, 255, 255)),
                ("ThermalShockTranscendence", new Color32(255, 157, 97, 255))
            };
            foreach (var entry in colors)
            {
                var data = AssetDatabase.LoadAssetAtPath<RelicTranscendenceData>(
                    "Assets/Nytherion/Data/ScriptableObjects/Relics/" + entry.asset + ".asset");
                if (data == null) throw new InvalidOperationException("초월 데이터 누락: " + entry.asset);
                Undo.RecordObject(data, "초월 효과 이름 색상 적용");
                data.nameColor = entry.color;
                EditorUtility.SetDirty(data);
                AssetDatabase.SaveAssetIfDirty(data);
            }
            ConfigureTitleIcon(tooltip);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("GameScene 저장에 실패했습니다.");
            File.WriteAllText(Output + "/transcendence-style.txt", "PASS: 초월 7종 이름 색상 및 64×64 툴팁 제목 아이콘 연결");
        }

        [MenuItem("Tools/Nytherion/유물/효과 툴팁 하이라키 생성·연결")]
        public static void Create()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("플레이를 종료한 뒤 실행해 주세요.");

            Directory.CreateDirectory(Output);
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            if (!scene.IsValid() || !scene.isLoaded)
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            RelicTooltip owner = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<RelicTooltip>(true)).Single();
            Canvas rootCanvas = owner.GetComponentInParent<Canvas>().rootCanvas;
            var serialized = new SerializedObject(owner);
            var originalTitle = (TextMeshProUGUI)serialized.FindProperty("nameText").objectReferenceValue;
            var originalBody = (TextMeshProUGUI)serialized.FindProperty("descriptionText").objectReferenceValue;
            var originalPanel = (GameObject)serialized.FindProperty("tooltipPanel").objectReferenceValue;
            Sprite background = originalPanel.GetComponent<Image>().sprite;

            // 디스크 상태와 에디터의 미저장 상태를 각각 보관한 뒤 같은 씬에 UI를 추가한다.
            if (!File.Exists(Output + "/GameScene-before-hierarchy-on-disk.unity"))
                File.Copy(ScenePath, Output + "/GameScene-before-hierarchy-on-disk.unity");
            if (!File.Exists(Output + "/GameScene-before-hierarchy-editor.unity"))
                EditorSceneManager.SaveScene(scene, Output + "/GameScene-before-hierarchy-editor.unity", true);

            var importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(background)) as TextureImporter;
            if (importer != null && importer.spriteBorder == Vector4.zero)
            {
                importer.spriteBorder = new Vector4(24f, 200f, 24f, 24f);
                importer.SaveAndReimport();
            }
            TMPDynamicFontAtlasRepair.RepairLoadedFonts();

            Transform container = rootCanvas.transform.Find("RelicEffectTooltips");
            if (container == null)
            {
                var root = new GameObject("RelicEffectTooltips", typeof(RectTransform));
                Undo.RegisterCreatedObjectUndo(root, "효과 툴팁 하이라키 생성");
                root.layer = rootCanvas.gameObject.layer;
                root.transform.SetParent(rootCanvas.transform, false);
                var rect = (RectTransform)root.transform;
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                container = root.transform;
            }

            RelicEffectTooltip setTooltip = CreatePanel(container, "SetEffectTooltipPanel", new Vector2(-430f, 350f),
                rootCanvas, background, originalTitle, originalBody, false);
            RelicEffectTooltip transcendenceTooltip = CreatePanel(container, "TranscendenceEffectTooltipPanel", new Vector2(30f, 350f),
                rootCanvas, background, originalTitle, originalBody, true);
            Undo.RecordObject(owner, "효과 툴팁 참조 연결");
            serialized.Update();
            serialized.FindProperty("setEffectTooltip").objectReferenceValue = setTooltip;
            serialized.FindProperty("transcendenceEffectTooltip").objectReferenceValue = transcendenceTooltip;
            serialized.ApplyModifiedProperties();
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("GameScene 저장에 실패했습니다.");

            Selection.activeGameObject = setTooltip.gameObject;
            File.WriteAllText(Output + "/hierarchy-setup.txt", "PASS: GameScene 하이라키 생성 및 참조 연결\n" +
                GetPath(setTooltip.transform) + "\n" + GetPath(transcendenceTooltip.transform));
        }

        private static RelicEffectTooltip CreatePanel(Transform parent, string name, Vector2 position,
            Canvas rootCanvas, Sprite background, TextMeshProUGUI titleTemplate, TextMeshProUGUI bodyTemplate,
            bool isTranscendence)
        {
            Transform existing = parent.Find(name);
            if (existing != null)
            {
                RelicEffectTooltip view = existing.GetComponent<RelicEffectTooltip>();
                if (view == null) throw new InvalidOperationException(name + "의 RelicEffectTooltip 참조를 확인해 주세요.");
                if (isTranscendence) ConfigureTitleIcon(view);
                else ConfigureSetTitleIcon(view);
                if (isTranscendence) ConfigureRequirementIconTemplate(view);
                return view;
            }

            var panel = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image),
                typeof(Canvas), typeof(CanvasGroup));
            Undo.RegisterCreatedObjectUndo(panel, "효과 툴팁 패널 생성");
            panel.layer = rootCanvas.gameObject.layer;
            panel.transform.SetParent(parent, false);
            RectTransform panelRect = (RectTransform)panel.transform;
            panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0f, 1f);
            panelRect.anchoredPosition = position;
            var image = panel.GetComponent<Image>();
            image.sprite = background;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = rootCanvas.referencePixelsPerUnit / background.pixelsPerUnit;
            image.raycastTarget = false;
            Canvas canvas = panel.GetComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingLayerID = rootCanvas.sortingLayerID;
            canvas.sortingOrder = isTranscendence ? 931 : 930;
            CanvasGroup group = panel.GetComponent<CanvasGroup>();
            group.alpha = 1f;
            group.interactable = group.blocksRaycasts = false;
            TextMeshProUGUI title = CreateText(panel.transform, "TitleText", titleTemplate,
                new Vector2(0f, -24f), new Vector2(352f, 50f));
            TextMeshProUGUI body = CreateText(panel.transform, "EffectText", bodyTemplate,
                new Vector2(0f, -90f), new Vector2(352f, 386f));

            float maximumHeight = 386f;
            if (isTranscendence)
            {
                foreach (string guid in AssetDatabase.FindAssets("t:RelicTranscendenceData"))
                {
                    var data = AssetDatabase.LoadAssetAtPath<RelicTranscendenceData>(AssetDatabase.GUIDToAssetPath(guid));
                    maximumHeight = Mathf.Max(maximumHeight, body.GetPreferredValues(data.BuildTooltipText(null), 352f, Mathf.Infinity).y);
                    if (body.text.Length == 0) { title.text = data.DisplayName; body.text = data.BuildTooltipText(null); }
                }
            }
            else
            {
                foreach (string guid in AssetDatabase.FindAssets("t:RelicSetBonusData"))
                {
                    var data = AssetDatabase.LoadAssetAtPath<RelicSetBonusData>(AssetDatabase.GUIDToAssetPath(guid));
                    maximumHeight = Mathf.Max(maximumHeight, body.GetPreferredValues(data.BuildTooltipText(null), 352f, Mathf.Infinity).y);
                    if (body.text.Length == 0) { title.text = data.DisplayName; body.text = data.BuildTooltipText(null); }
                }
            }
            // 처음 생성할 때만 배치하고 재실행 시에는 편집된 RectTransform을 유지한다.
            body.rectTransform.sizeDelta = new Vector2(352f, Mathf.Ceil(maximumHeight));
            panelRect.sizeDelta = new Vector2(400f, 90f + Mathf.Ceil(maximumHeight) + 224f);
            RelicEffectTooltip tooltip = panel.AddComponent<RelicEffectTooltip>();
            var serialized = new SerializedObject(tooltip);
            serialized.FindProperty("canvasGroup").objectReferenceValue = group;
            serialized.FindProperty("titleText").objectReferenceValue = title;
            serialized.FindProperty("descriptionText").objectReferenceValue = body;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            if (isTranscendence) ConfigureTitleIcon(tooltip);
            else ConfigureSetTitleIcon(tooltip);
            if (isTranscendence) ConfigureRequirementIconTemplate(tooltip);
            return tooltip;
        }

        private static void ConfigureRequirementIconTemplate(RelicEffectTooltip tooltip)
        {
            var serialized = new SerializedObject(tooltip);
            if (serialized.FindProperty("requirementIconTemplate").objectReferenceValue != null) return;
            var body = (TextMeshProUGUI)serialized.FindProperty("descriptionText").objectReferenceValue;
            var iconObject = new GameObject("RequirementIconTemplate", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            Undo.RegisterCreatedObjectUndo(iconObject, "초월 조건 아이콘 템플릿 추가");
            iconObject.layer = tooltip.gameObject.layer;
            iconObject.transform.SetParent(body.transform, false);
            var icon = iconObject.GetComponent<Image>();
            icon.raycastTarget = false;
            icon.preserveAspect = true;
            icon.rectTransform.sizeDelta = Vector2.one * serialized.FindProperty("requirementIconSize").floatValue;
            iconObject.SetActive(false);
            Undo.RecordObject(tooltip, "초월 조건 아이콘 템플릿 연결");
            serialized.FindProperty("requirementIconTemplate").objectReferenceValue = icon;
            serialized.ApplyModifiedProperties();
        }

        private static void ConfigureSetTitleIcon(RelicEffectTooltip tooltip)
        {
            var serialized = new SerializedObject(tooltip);
            if (serialized.FindProperty("titleIcon").objectReferenceValue != null) return;
            var title = (TextMeshProUGUI)serialized.FindProperty("titleText").objectReferenceValue;
            var iconObject = new GameObject("TitleIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            Undo.RegisterCreatedObjectUndo(iconObject, "세트 툴팁 제목 아이콘 추가");
            iconObject.layer = tooltip.gameObject.layer;
            iconObject.transform.SetParent(tooltip.transform, false);
            iconObject.transform.SetAsFirstSibling();
            var icon = iconObject.GetComponent<Image>();
            icon.raycastTarget = false;
            icon.preserveAspect = true;
            RectTransform iconRect = icon.rectTransform;
            iconRect.anchorMin = iconRect.anchorMax = new Vector2(0.5f, 1f);
            iconRect.pivot = new Vector2(1f, 0.5f);
            iconRect.sizeDelta = new Vector2(64f, 64f);
            title.ForceMeshUpdate(true);
            Bounds bounds = title.textBounds;
            float spacing = serialized.FindProperty("titleIconSpacing").floatValue;
            iconRect.position = title.rectTransform.TransformPoint(
                new Vector3(bounds.min.x - spacing, bounds.center.y, 0f));
            var preview = AssetDatabase.FindAssets("t:RelicSetBonusData")
                .Select(guid => AssetDatabase.LoadAssetAtPath<RelicSetBonusData>(AssetDatabase.GUIDToAssetPath(guid)))
                .FirstOrDefault(data => data.DisplayName == title.text);
            if (preview != null) icon.sprite = preview.statusIcon;
            Undo.RecordObject(tooltip, "세트 툴팁 제목 아이콘 연결");
            serialized.FindProperty("titleIcon").objectReferenceValue = icon;
            serialized.ApplyModifiedProperties();
        }

        private static void ConfigureTitleIcon(RelicEffectTooltip tooltip)
        {
            var serialized = new SerializedObject(tooltip);
            if (serialized.FindProperty("titleIcon").objectReferenceValue != null) return;
            var title = (TextMeshProUGUI)serialized.FindProperty("titleText").objectReferenceValue;
            var body = (TextMeshProUGUI)serialized.FindProperty("descriptionText").objectReferenceValue;
            RectTransform panel = (RectTransform)tooltip.transform;
            var iconObject = new GameObject("TitleIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            Undo.RegisterCreatedObjectUndo(iconObject, "초월 툴팁 제목 아이콘 추가");
            iconObject.layer = tooltip.gameObject.layer;
            iconObject.transform.SetParent(tooltip.transform, false);
            iconObject.transform.SetAsFirstSibling();
            var icon = iconObject.GetComponent<Image>();
            icon.raycastTarget = false;
            icon.preserveAspect = true;
            RectTransform iconRect = icon.rectTransform;
            iconRect.anchorMin = iconRect.anchorMax = new Vector2(0f, 1f);
            iconRect.pivot = new Vector2(0f, 1f);
            iconRect.anchoredPosition = new Vector2(24f, -24f);
            iconRect.sizeDelta = new Vector2(64f, 64f);

            Undo.RecordObjects(new UnityEngine.Object[] { title, title.rectTransform, body.rectTransform, tooltip },
                "초월 툴팁 제목 배치");
            title.rectTransform.anchorMin = title.rectTransform.anchorMax = new Vector2(0f, 1f);
            title.rectTransform.pivot = new Vector2(0f, 1f);
            title.rectTransform.anchoredPosition = new Vector2(100f, -24f);
            title.rectTransform.sizeDelta = new Vector2(panel.rect.width - 124f, 64f);
            title.alignment = TextAlignmentOptions.MidlineLeft;
            Vector2 bodyPosition = body.rectTransform.anchoredPosition;
            bodyPosition.y = Mathf.Min(bodyPosition.y, -104f);
            body.rectTransform.anchoredPosition = bodyPosition;

            var preview = AssetDatabase.FindAssets("t:RelicTranscendenceData")
                .Select(guid => AssetDatabase.LoadAssetAtPath<RelicTranscendenceData>(AssetDatabase.GUIDToAssetPath(guid)))
                .FirstOrDefault(data => data.DisplayName == title.text);
            if (preview != null)
            {
                icon.sprite = preview.statusIcon;
                title.color = preview.nameColor;
            }
            serialized.FindProperty("titleIcon").objectReferenceValue = icon;
            serialized.ApplyModifiedProperties();
        }

        private static TextMeshProUGUI CreateText(Transform parent, string name, TextMeshProUGUI template,
            Vector2 position, Vector2 size)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
            root.layer = parent.gameObject.layer;
            root.transform.SetParent(parent, false);
            var rect = (RectTransform)root.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var text = root.AddComponent<TextMeshProUGUI>();
            text.font = template.font;
            text.fontSize = template.fontSize;
            text.fontStyle = template.fontStyle;
            text.alignment = TextAlignmentOptions.TopLeft;
            text.color = Color.white;
            text.richText = true;
            text.enableWordWrapping = true;
            text.raycastTarget = false;
            text.text = string.Empty;
            return text;
        }

        private static string GetPath(Transform node) => node.parent == null ? node.name : GetPath(node.parent) + "/" + node.name;
    }
}
