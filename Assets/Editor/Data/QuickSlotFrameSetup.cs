using System;
using System.IO;
using System.Linq;
using Nytherion.UI.Inventory;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Nytherion.Editor
{
    /// <summary>분리한 키 이미지의 내부 영역을 각 슬롯에 설정합니다.</summary>
    [InitializeOnLoad]
    public static class QuickSlotFrameSetup
    {
        private const string Output = "output/quick-slot-frames";
        private static readonly string[] Keys = { "Q", "R", "E" };
        private static readonly Rect IconArea = new Rect(4f / 36f, 4f / 50f, 28f / 36f, 28f / 50f);

        static QuickSlotFrameSetup() => EditorApplication.update += Update;

        private static void Update()
        {
            if (!EditorApplication.isCompiling && !EditorApplication.isUpdating && File.Exists(Output + "/color.request"))
            {
                File.Delete(Output + "/color.request");
                try { ApplyBackgroundColor(); }
                catch (Exception error) { File.WriteAllText(Output + "/color-result.txt", "FAIL " + error); Debug.LogException(error); }
                return;
            }
            if (!EditorApplication.isCompiling && !EditorApplication.isUpdating && File.Exists(Output + "/inspect.request"))
            {
                File.Delete(Output + "/inspect.request");
                InspectLayout();
            }
            if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
                EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(Output + "/setup.request")) return;
            File.Delete(Output + "/setup.request");
            try { Apply(); }
            catch (Exception error)
            {
                File.WriteAllText(Output + "/result.txt", "FAIL " + error);
                Debug.LogException(error);
            }
        }

        private static void ApplyBackgroundColor()
        {
            Color color = new Color32(43, 33, 28, 255);
            for (int i = 0; i < Keys.Length; i++)
            {
                string path = "Assets/Prefabs/UI/QuickSlot/QuickSlotUI " + (i + 1) + ".prefab";
                GameObject prefab = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    Image background = prefab.transform.Find("SkillIconBackground").GetComponent<Image>();
                    background.color = color;
                    PrefabUtility.SaveAsPrefabAsset(prefab, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(prefab); }
            }
            // 실행 중인 슬롯도 다음 장착 변경을 기다리지 않고 같은 색으로 갱신합니다.
            if (EditorApplication.isPlaying)
                foreach (QuickSlotUI slot in UnityEngine.Object.FindObjectsOfType<QuickSlotUI>(true))
                    slot.SetSkill(slot.CurrentSkill);
            File.WriteAllText(Output + "/color-result.txt", "PASS Q/R/E 배경 #2B211C 적용, 크기와 위치 유지");
        }

        private static void InspectLayout()
        {
            var report = new System.Text.StringBuilder();
            report.AppendLine("playing=" + EditorApplication.isPlaying);
            foreach (QuickSlotUI slot in UnityEngine.Object.FindObjectsOfType<QuickSlotUI>(true))
            {
                report.AppendLine(slot.name + " scene=" + slot.gameObject.scene.path);
                var data = new SerializedObject(slot);
                Image background = data.FindProperty("skillIconBackground").objectReferenceValue as Image;
                foreach (Image image in new[] { slot.GetComponent<Image>(), background, slot.IconImage })
                {
                    if (image == null) continue;
                    Canvas canvas = image.canvas;
                    Camera camera = canvas != null && canvas.rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.rootCanvas.worldCamera : null;
                    Rect adjusted = image.GetPixelAdjustedRect();
                    Vector2 min = RectTransformUtility.WorldToScreenPoint(camera, image.rectTransform.TransformPoint(adjusted.min));
                    Vector2 max = RectTransformUtility.WorldToScreenPoint(camera, image.rectTransform.TransformPoint(adjusted.max));
                    report.AppendLine(image.name + " local=" + image.rectTransform.rect.ToString("F5") + " pos=" + image.rectTransform.anchoredPosition.ToString("F5") + " adjusted=" + adjusted.ToString("F5") + " screenMin=" + min.ToString("F5") + " screenMax=" + max.ToString("F5") + " canvasScale=" + (canvas != null ? canvas.scaleFactor : 0f) + " pixelPerfect=" + (canvas != null && canvas.pixelPerfect));
                }
            }
            File.WriteAllText(Output + "/inspection.txt", report.ToString());
        }

        [MenuItem("Tools/Nytherion/Skills/QRE 개별 슬롯 배경 적용")]
        public static void Apply()
        {
            Directory.CreateDirectory(Output);
            const string scenePath = "Assets/Scenes/GameScene.unity";
            Scene scene = SceneManager.GetSceneByPath(scenePath);
            bool alreadyLoaded = scene.IsValid() && scene.isLoaded;
            if (alreadyLoaded && scene.isDirty)
            {
                // 열려 있는 씬의 변경도 보존할 수 있도록 적용 직전 사본을 남깁니다.
                string backup = Output + "/GameScene-before-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".unity";
                if (!EditorSceneManager.SaveScene(scene, backup, true))
                    throw new InvalidOperationException("변경 전 씬 사본을 저장하지 못했습니다.");
            }

            Sprite[] sprites = Keys.Select(ImportSprite).ToArray();
            for (int i = 0; i < Keys.Length; i++)
            {
                string path = "Assets/Prefabs/UI/QuickSlot/QuickSlotUI " + (i + 1) + ".prefab";
                GameObject prefab = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    Configure(prefab.GetComponent<QuickSlotUI>(), sprites[i]);
                    PrefabUtility.SaveAsPrefabAsset(prefab, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(prefab); }
            }

            if (!alreadyLoaded) scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            try
            {
                QuickSlotUI[] slots = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<QuickSlotUI>(true))
                    .OrderBy(slot => slot.name).ToArray();
                if (slots.Length != 3) throw new InvalidOperationException("GameScene의 QRE 슬롯이 3개가 아닙니다.");
                Transform panel = slots[0].transform.parent;
                Canvas quickSlotCanvas = panel.GetComponentInParent<Canvas>();
                if (quickSlotCanvas == null || quickSlotCanvas.name != "QuickSlotCanvas")
                    throw new InvalidOperationException("퀵슬롯 전용 Canvas를 찾을 수 없습니다.");
                // 테두리와 내부 배경을 별도로 반올림하면 비정수 배율에서 중심이 어긋납니다.
                Undo.RecordObject(quickSlotCanvas, "퀵슬롯 픽셀 반올림 해제");
                quickSlotCanvas.overridePixelPerfect = true;
                quickSlotCanvas.pixelPerfect = false;
                Transform oldBackground = panel.parent.Find("Background");
                if (oldBackground == null) throw new InvalidOperationException("기존 통합 배경을 찾을 수 없습니다.");
                Undo.RecordObject(oldBackground.gameObject, "QRE 개별 배경 적용");
                oldBackground.gameObject.SetActive(false);
                for (int i = 0; i < slots.Length; i++)
                {
                    Configure(slots[i], sprites[i]);
                    RectTransform rect = (RectTransform)slots[i].transform;
                    // 배경끼리 겹치지 않게 간격을 확보하고 내부 중심의 기존 높이를 유지합니다.
                    rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
                    rect.anchoredPosition = new Vector2(72f + i * 120f, -117f);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(rect);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(slots[i]);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(slots[i].GetComponent<Image>());
                    PrefabUtility.RecordPrefabInstancePropertyModifications(slots[i].IconImage.rectTransform);
                }
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                File.WriteAllText(Output + "/result.txt",
                    "PASS Q/R/E 개별 이미지 연결, 통합 배경 숨김, 슬롯 108x150 / 아이콘 84x84 / 검은 배경 96x96 / 중심 (0,-21), 퀵슬롯 Canvas 개별 픽셀 반올림 해제, 프리팹 3개 및 GameScene 저장");
            }
            finally { if (!alreadyLoaded) EditorSceneManager.CloseScene(scene, true); }
        }

        private static Sprite ImportSprite(string key)
        {
            string path = "Assets/Nytherion/Art/UI/HUD/QuickSlot" + key + ".png";
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100f;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.npotScale = TextureImporterNPOTScale.None;
            TextureImporterSettings settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static void Configure(QuickSlotUI slot, Sprite sprite)
        {
            Undo.RecordObjects(new UnityEngine.Object[] { slot, slot.transform, slot.GetComponent<Image>(), slot.IconImage.rectTransform }, "QRE 슬롯 설정");
            RectTransform rect = (RectTransform)slot.transform;
            rect.sizeDelta = new Vector2(108f, 150f);
            Image frame = slot.GetComponent<Image>();
            frame.sprite = sprite;
            frame.color = Color.white;
            frame.type = Image.Type.Simple;
            frame.preserveAspect = false;
            frame.raycastTarget = false;
            SerializedObject data = new SerializedObject(slot);
            data.FindProperty("skillIconArea").rectValue = IconArea;
            data.ApplyModifiedPropertiesWithoutUndo();
            RectTransform icon = slot.IconImage.rectTransform;
            icon.anchorMin = icon.anchorMax = icon.pivot = Vector2.one * 0.5f;
            icon.sizeDelta = Vector2.one * 84f;
            icon.anchoredPosition = new Vector2(0f, -21f);
            icon.localScale = Vector3.one;
            icon.localRotation = Quaternion.identity;

            // 편집 모드에서도 실행 중과 같은 배경 오브젝트와 크기를 사용합니다.
            Transform existing = slot.transform.Find("SkillIconBackground");
            Image background;
            if (existing != null) background = existing.GetComponent<Image>();
            else
            {
                GameObject child = new GameObject("SkillIconBackground", typeof(RectTransform), typeof(Image));
                child.layer = slot.gameObject.layer;
                child.transform.SetParent(slot.transform, false);
                background = child.GetComponent<Image>();
            }
            background.transform.SetSiblingIndex(icon.GetSiblingIndex());
            // 기존 배경을 재설정해도 아이콘 뒤의 순서를 유지합니다.
            icon.SetSiblingIndex(background.transform.GetSiblingIndex() + 1);
            background.color = new Color32(43, 33, 28, 255);
            background.raycastTarget = false;
            background.enabled = slot.IconImage.enabled && slot.IconImage.sprite != null;
            RectTransform backgroundRect = background.rectTransform;
            backgroundRect.anchorMin = backgroundRect.anchorMax = backgroundRect.pivot = Vector2.one * 0.5f;
            backgroundRect.anchoredPosition = icon.anchoredPosition;
            backgroundRect.sizeDelta = Vector2.one * 96f;
            backgroundRect.localScale = Vector3.one;
            backgroundRect.localRotation = Quaternion.identity;
            data.Update();
            data.FindProperty("skillIconBackground").objectReferenceValue = background;
            data.FindProperty("skillBackgroundSize").vector2Value = Vector2.one * 96f;
            data.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
