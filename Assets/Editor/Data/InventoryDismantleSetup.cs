using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Nytherion.Core.Data;
using Nytherion.Core.Enums;
using Nytherion.Core.Managers;
using Nytherion.Core.Systems;
using Nytherion.Data.ScriptableObjects.Items;
using Nytherion.Data.ScriptableObjects.Weapons;
using Nytherion.UI.Inventory;
using Nytherion.UI.Controllers;
using Nytherion.UI.Components;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using VContainer;
using Object = UnityEngine.Object;

namespace Nytherion.Editor
{
    [InitializeOnLoad]
    public static class InventoryDismantleSetup
    {
        private const string Output = "output/inventory-dismantle";
        private const string RoundedSpritePath = "Assets/Nytherion/Art/UI/Inventory/DismantleScrollRounded.png";
        private static double playReadyAt;
        static InventoryDismantleSetup()
        {
            EditorApplication.update += Update;
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredPlayMode) playReadyAt = EditorApplication.timeSinceStartup + 8d;
            };
        }

        private static void Update()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            if (SessionState.GetBool("Nytherion.Dismantle.VerifyPlay", false) && EditorApplication.isPlaying &&
                EditorApplication.timeSinceStartup >= playReadyAt)
            {
                SessionState.SetBool("Nytherion.Dismantle.VerifyPlay", false);
                try
                {
                    GameSceneUIRefs refs = Object.FindObjectsOfType<GameSceneUIRefs>(true).First(item => item.DismantlePanel != null);
                    InventorySlotUI[] liveSlots = refs.InventorySlotParent.GetComponentsInChildren<InventorySlotUI>(true);
                    FieldInfo owner = typeof(InventorySlotUI).GetField("inventoryUI", BindingFlags.Instance | BindingFlags.NonPublic);
                    Require(liveSlots.Length > 0 && liveSlots.All(slot => owner.GetValue(slot) != null), "실제 인벤토리 슬롯의 분해 UI 의존성 주입");
                    InventoryUI liveUI = (InventoryUI)owner.GetValue(liveSlots[0]);
                    bool wasOpen = liveUI.IsOpen;
                    try
                    {
                        liveUI.Initialize();
                        liveUI.Open(false);
                        refs.InventoryDismantleButton.onClick.Invoke();
                        Require(liveUI.IsDismantleOpen && refs.DismantlePanel.gameObject.activeInHierarchy,
                            "실제 분해 버튼 클릭 후 패널 활성화(중복 초기화 회귀)");
                        refs.InventoryDismantleButton.onClick.Invoke();
                        Require(!liveUI.IsDismantleOpen, "실제 분해 버튼 재클릭 후 닫기");
                    }
                    finally { if (!wasOpen) liveUI.Close(); }
                    VerifyUI(refs, null);
                    File.WriteAllText(Output + "/play-verification.txt", "PASS " + SceneManager.GetActiveScene().name +
                        " 플레이 모드: 격리 UI 버튼 토글, 우클릭/드래그, 동적 슬롯/등급, 초기화, 골드 지급, 닫기 복귀. 실제 소지품/세이브 미수정.");
                }
                catch (Exception error) { File.WriteAllText(Output + "/play-verification.txt", "FAIL " + error); }
                finally { if (SessionState.GetBool("Nytherion.Dismantle.ExitPlay", false)) EditorApplication.isPlaying = false; }
                return;
            }
            if (File.Exists(Output + "/play.request"))
            {
                File.Delete(Output + "/play.request");
                SessionState.SetBool("Nytherion.Dismantle.ExitPlay", !EditorApplication.isPlaying);
                SessionState.SetBool("Nytherion.Dismantle.VerifyPlay", true);
                playReadyAt = EditorApplication.timeSinceStartup + 8d;
                if (!EditorApplication.isPlaying) EditorApplication.isPlaying = true;
                return;
            }
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (File.Exists(Output + "/bulk-setup.request"))
            {
                File.Delete(Output + "/bulk-setup.request");
                try { ApplyBulkSelection(); }
                catch (Exception error)
                {
                    File.WriteAllText(Output + "/bulk-result.txt", "FAIL " + error);
                    Debug.LogException(error);
                }
                return;
            }
            if (File.Exists("output/inventory-buttons/pixels48.request"))
            {
                File.Delete("output/inventory-buttons/pixels48.request");
                try { MatchButtonSizes(); }
                catch (Exception error)
                {
                    File.WriteAllText("output/inventory-buttons/result.txt", "FAIL " + error);
                    Debug.LogException(error);
                }
                return;
            }
            if (File.Exists(Output + "/rounded.request"))
            {
                File.Delete(Output + "/rounded.request");
                try { ApplyRoundedBackground(); }
                catch (Exception error)
                {
                    File.WriteAllText(Output + "/rounded-result.txt", "FAIL " + error);
                    Debug.LogException(error);
                }
                return;
            }
            if (File.Exists(Output + "/verify.request"))
            {
                File.Delete(Output + "/verify.request");
                try { Verify(); }
                catch (Exception error) { File.WriteAllText(Output + "/verification.txt", "FAIL " + error); }
                return;
            }
            if (File.Exists(Output + "/ui-verify.request"))
            {
                File.Delete(Output + "/ui-verify.request");
                VerifyUIOnly();
                return;
            }
            if (!File.Exists(Output + "/setup.request")) return;
            File.Delete(Output + "/setup.request");
            try { Apply(); File.WriteAllText(Output + "/result.txt", "PASS 씬 설정과 분해 데이터/UI 검증 완료"); }
            catch (Exception error) { File.WriteAllText(Output + "/result.txt", "FAIL " + error); Debug.LogException(error); }
        }

        [MenuItem("Tools/Nytherion/Inventory/분해 패널 설정 및 검증")]
        public static void Apply()
        {
            Directory.CreateDirectory(Output);
            var report = new List<string>();
            foreach (string path in new[] { "Assets/Scenes/GameScene.unity" })
            {
                Scene scene = SceneManager.GetSceneByPath(path);
                bool loaded = scene.IsValid() && scene.isLoaded;
                if (!loaded) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                try
                {
                    GameSceneUIRefs refs = scene.GetRootGameObjects().SelectMany(root =>
                        root.GetComponentsInChildren<GameSceneUIRefs>(true)).First();
                    Build(refs);
                    VerifyUI(refs, EditorSceneManager.NewPreviewScene());
                    EditorSceneManager.MarkSceneDirty(scene);
                    Require(EditorSceneManager.SaveScene(scene), path + " 저장 실패");
                    report.Add("PASS " + path + " 분해 패널/버튼 및 Inspector 참조");
                    Capture(refs);
                }
                finally { if (!loaded) EditorSceneManager.CloseScene(scene, true); }
            }
            File.WriteAllLines(Output + "/setup.txt", report);
            Verify();
        }

        [MenuItem("Tools/Nytherion/Inventory/정렬·분해 버튼 크기 맞추기")]
        public static void MatchButtonSizes()
        {
            const string output = "output/inventory-buttons";
            const string path = "Assets/Scenes/GameScene.unity";
            Directory.CreateDirectory(output);
            Scene scene = SceneManager.GetSceneByPath(path);
            bool loaded = scene.IsValid() && scene.isLoaded;
            if (!loaded) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                GameSceneUIRefs refs = scene.GetRootGameObjects().SelectMany(root =>
                    root.GetComponentsInChildren<GameSceneUIRefs>(true)).Single();
                Button sort = refs.InventorySortButton;
                Button dismantle = refs.InventoryDismantleButton;
                Require(sort != null && dismantle != null, "정렬·분해 버튼 Inspector 참조");
                RectTransform sortRect = (RectTransform)sort.transform;
                RectTransform dismantleRect = (RectTransform)dismantle.transform;
                Image sortImage = sort.GetComponent<Image>();
                Image dismantleImage = dismantle.GetComponent<Image>();
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(
                    "Assets/Nytherion/Art/UI/Inventory/SalvageButton.png");
                Require(sortImage.sprite != null && sprite != null && sprite.rect.size == sortImage.sprite.rect.size,
                    "정렬·분해 스프라이트의 원본 크기 일치");
                Require(sortRect.parent == dismantleRect.parent, "버튼의 부모 배율 일치");
                // 열려 있는 씬의 미저장 변경도 복사본으로 보존한다.
                string backup = output + "/GameScene-before-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".unity";
                Require(EditorSceneManager.SaveScene(scene, backup, true), "변경 전 씬 백업");
                File.WriteAllText(output + "/before.txt",
                    $"sort={sortRect.sizeDelta}, dismantle={dismantleRect.sizeDelta}, backup={backup}");
                Undo.RecordObjects(new Object[] { sortRect, dismantleRect, sortImage, dismantleImage, dismantle },
                    "정렬·분해 버튼 크기 맞추기");
                sortRect.sizeDelta = new Vector2(48f, 48f);
                dismantleRect.anchorMin = sortRect.anchorMin;
                dismantleRect.anchorMax = sortRect.anchorMax;
                dismantleRect.pivot = sortRect.pivot;
                dismantleRect.localScale = sortRect.localScale;
                dismantleRect.localRotation = sortRect.localRotation;
                dismantleRect.sizeDelta = sortRect.sizeDelta;
                dismantleImage.sprite = sprite;
                dismantleImage.overrideSprite = null;
                dismantleImage.color = Color.white;
                sortImage.type = dismantleImage.type = Image.Type.Simple;
                sortImage.preserveAspect = dismantleImage.preserveAspect = true;
                sortImage.useSpriteMesh = dismantleImage.useSpriteMesh = false;
                // 분해 아이콘의 1픽셀 선까지 보존하도록 두 원본의 24×24 픽셀 격자를 기준으로 맞춘다.
                foreach (Image image in new[] { sortImage, dismantleImage })
                {
                    PixelPerfectButtonImage effect = image.GetComponent<PixelPerfectButtonImage>();
                    if (effect != null)
                    {
                        Undo.RecordObject(effect, "버튼 원본 픽셀 보정 복원");
                    }
                    PixelPerfectButtonImage.Apply(image, 1);
                    image.raycastPadding = Vector4.zero;
                }
                dismantle.targetGraphic = dismantleImage;
                Transform label = dismantle.transform.Find("Label");
                if (label != null)
                {
                    Undo.RecordObject(label.gameObject, "분해 아이콘의 기존 글자 숨김");
                    label.gameObject.SetActive(false);
                }
                EditorSceneManager.MarkSceneDirty(scene);
                Require(EditorSceneManager.SaveScene(scene), "버튼 설정 씬 저장");
                Require(sortRect.rect.size == dismantleRect.rect.size &&
                    sortImage.sprite.rect.size == dismantleImage.sprite.rect.size &&
                    sortImage.GetComponent<PixelPerfectButtonImage>().enabled &&
                    dismantleImage.GetComponent<PixelPerfectButtonImage>().enabled,
                    "버튼 영역·스프라이트·원본 픽셀 보정 일치");
                VerifyButtonSizes(sort, dismantle, output);
                File.WriteAllText(output + "/result.txt",
                    "PASS GameScene 정렬·분해 버튼: 48×48, 두 원본의 24×24 격자 기준 정수 배율 보정 활성, 같은 렌더 크기와 원본 픽셀/검은 선 보존 검증, 씬 저장 완료");
            }
            finally { if (!loaded) EditorSceneManager.CloseScene(scene, true); }
        }

        private static void VerifyButtonSizes(Button sort, Button dismantle, string output)
        {
            Scene preview = EditorSceneManager.NewPreviewScene();
            RenderTexture target = null;
            Texture2D pixels = null;
            var originals = new List<Texture2D>();
            try
            {
                GameObject cameraObject = new GameObject("버튼 크기 검증 카메라", typeof(Camera));
                SceneManager.MoveGameObjectToScene(cameraObject, preview);
                Camera camera = cameraObject.GetComponent<Camera>();
                camera.scene = preview;
                camera.enabled = false;
                camera.orthographic = true;
                camera.orthographicSize = 80f;
                camera.transform.position = new Vector3(20000f, 20000f, -5f);
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.9f, 0.83f, 0.68f);
                target = new RenderTexture(320, 160, 24);
                camera.targetTexture = target;
                GameObject root = new GameObject("버튼 크기 검증", typeof(RectTransform), typeof(Canvas));
                SceneManager.MoveGameObjectToScene(root, preview);
                Canvas canvas = root.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1f;
                Button[] buttons = { Object.Instantiate(sort, root.transform), Object.Instantiate(dismantle, root.transform) };
                for (int i = 0; i < buttons.Length; i++)
                {
                    Texture2D original = new Texture2D(2, 2);
                    originals.Add(original);
                    Require(original.LoadImage(File.ReadAllBytes(AssetDatabase.GetAssetPath(buttons[i].image.sprite))),
                        "버튼 원본 픽셀 읽기");
                    RectTransform rect = (RectTransform)buttons[i].transform;
                    rect.anchorMin = rect.anchorMax = Vector2.one * 0.5f;
                    rect.anchoredPosition = new Vector2(i == 0 ? -48f : 48f, 0f);
                    buttons[i].gameObject.SetActive(true);
                }
                pixels = new Texture2D(320, 160, TextureFormat.RGB24, false);
                var report = new List<string>();
                foreach (float scale in new[] { 0.5f, 0.75f, 1.25f, 1.5f, 2f, 1f })
                {
                    canvas.scaleFactor = scale;
                    foreach (Button button in buttons) button.image.SetVerticesDirty();
                    Canvas.ForceUpdateCanvases();
                    camera.Render();
                    Mesh sortMesh = buttons[0].image.canvasRenderer.GetMesh();
                    Require(sortMesh != null && sortMesh.vertexCount > 0, "정렬 버튼 렌더 메시");
                    Vector3 sortSize = sortMesh.bounds.size;
                    Mesh dismantleMesh = buttons[1].image.canvasRenderer.GetMesh();
                    Require(dismantleMesh != null && dismantleMesh.vertexCount > 0, "분해 버튼 렌더 메시");
                    Require(Vector3.Distance(sortSize, dismantleMesh.bounds.size) < 0.01f, "배율별 렌더 크기 일치: " + scale);
                    RenderTexture previousTarget = RenderTexture.active;
                    try
                    {
                        RenderTexture.active = target;
                        pixels.ReadPixels(new Rect(0, 0, 320, 160), 0, 0);
                        pixels.Apply();
                    }
                    finally { RenderTexture.active = previousTarget; }
                    for (int i = 0; i < buttons.Length; i++)
                    {
                        Image image = buttons[i].image;
                        Bounds bounds = image.canvasRenderer.GetMesh().bounds;
                        Vector2 min = RectTransformUtility.WorldToScreenPoint(camera, image.rectTransform.TransformPoint(bounds.min));
                        Vector2 max = RectTransformUtility.WorldToScreenPoint(camera, image.rectTransform.TransformPoint(bounds.max));
                        int pixelScale = Mathf.RoundToInt((max.x - min.x) / originals[i].width);
                        Require(pixelScale >= 1 && Mathf.Abs(max.x - min.x - originals[i].width * pixelScale) < 0.01f &&
                            Mathf.Abs(max.y - min.y - originals[i].height * pixelScale) < 0.01f,
                            "원본 픽셀의 가로·세로 정수 배율 일치");
                        Require(Mathf.Abs(min.x - Mathf.Round(min.x)) < 0.01f && Mathf.Abs(min.y - Mathf.Round(min.y)) < 0.01f,
                            "화면 픽셀 격자 정렬");
                        int left = Mathf.RoundToInt(min.x);
                        int bottom = Mathf.RoundToInt(min.y);
                        for (int y = 0; y < originals[i].height; y++)
                            for (int x = 0; x < originals[i].width; x++)
                            {
                                Color32 rendered = pixels.GetPixel(left + x * pixelScale, bottom + y * pixelScale);
                                for (int dy = 0; dy < pixelScale; dy++)
                                    for (int dx = 0; dx < pixelScale; dx++)
                                        Require(((Color32)pixels.GetPixel(left + x * pixelScale + dx, bottom + y * pixelScale + dy)).Equals(rendered),
                                            "원본 픽셀 블록의 균일한 확대");
                                Color32 source = originals[i].GetPixel(x, y);
                                bool sourceBlack = source.a == 255 && source.r == 0 && source.g == 0 && source.b == 0;
                                bool renderedBlack = rendered.r == 0 && rendered.g == 0 && rendered.b == 0;
                                Require(sourceBlack == renderedBlack, "원본의 검은 외곽선과 세부 선 보존");
                            }
                    }
                    Require(scale != 1f || (Mathf.Abs(sortSize.x - 48f) < 0.01f && Mathf.Abs(sortSize.y - 48f) < 0.01f),
                        "화면 배율 1에서 48×48 표시");
                    report.Add($"PASS Canvas 배율 {scale}: 같은 렌더 크기, 원본의 모든 픽셀 블록 균일, 외곽선·세부 선 보존");
                }
                RenderTexture previous = RenderTexture.active;
                try
                {
                    RenderTexture.active = target;
                    pixels.ReadPixels(new Rect(0, 0, 320, 160), 0, 0);
                    pixels.Apply();
                    File.WriteAllBytes(output + "/preview.png", pixels.EncodeToPNG());
                }
                finally { RenderTexture.active = previous; }
                File.WriteAllLines(output + "/render-verification.txt", report);
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(preview);
                if (target != null) { target.Release(); Object.DestroyImmediate(target); }
                if (pixels != null) Object.DestroyImmediate(pixels);
                foreach (Texture2D original in originals) Object.DestroyImmediate(original);
            }
        }

        [MenuItem("Tools/Nytherion/Inventory/분해 Scroll 둥근 배경 적용")]
        public static void ApplyRoundedBackground()
        {
            Directory.CreateDirectory(Output);
            Sprite sprite = GetRoundedSprite();
            var report = new List<string>();
            foreach (string path in new[] { "Assets/Scenes/GameScene.unity" })
            {
                Scene scene = SceneManager.GetSceneByPath(path);
                bool loaded = scene.IsValid() && scene.isLoaded;
                if (!loaded) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                try
                {
                    GameSceneUIRefs refs = scene.GetRootGameObjects().SelectMany(root =>
                        root.GetComponentsInChildren<GameSceneUIRefs>(true)).First(item => item.DismantlePanel != null);
                    Image image = refs.DismantlePanel.transform.Find("Scroll").GetComponent<Image>();
                    Require(image != null, path + " 분해 Scroll 배경");
                    // 기존 미저장 씬도 복사본을 남긴 뒤 배경 변경과 함께 보존한다.
                    if (scene.isDirty)
                        Require(EditorSceneManager.SaveScene(scene, Output + "/" + scene.name + "-before-rounded.unity", true),
                            path + " 기존 미저장 씬 백업");
                    Undo.RecordObject(image, "분해 Scroll 둥근 배경");
                    image.sprite = sprite;
                    image.type = Image.Type.Sliced;
                    image.fillCenter = true;
                    image.pixelsPerUnitMultiplier = 1f;
                    EditorSceneManager.MarkSceneDirty(scene);
                    Require(EditorSceneManager.SaveScene(scene), path + " 저장 실패");
                    Require(image.sprite == sprite && image.type == Image.Type.Sliced && sprite.border == Vector4.one * 16f,
                        path + " 둥근 배경 Inspector 설정");
                    Capture(refs);
                    report.Add("PASS " + path + " 분해 Scroll: 둥근 배경, Sliced, 반경 12px, 기존 색상/레이아웃 보존 및 미리보기 렌더");
                }
                finally { if (!loaded) EditorSceneManager.CloseScene(scene, true); }
            }
            File.WriteAllLines(Output + "/rounded-result.txt", report);
        }

        private static Sprite GetRoundedSprite()
        {
            if (!File.Exists(RoundedSpritePath))
            {
                const int size = 64;
                const float radius = 12f;
                Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
                try
                {
                    // 흰색 모양에 알파만 넣어 기존 Image.color로 색상을 지정한다.
                    for (int y = 0; y < size; y++)
                        for (int x = 0; x < size; x++)
                        {
                            Vector2 delta = new Vector2(Mathf.Abs(x + 0.5f - size * 0.5f),
                                Mathf.Abs(y + 0.5f - size * 0.5f)) - Vector2.one * (size * 0.5f - radius);
                            float distance = new Vector2(Mathf.Max(delta.x, 0f), Mathf.Max(delta.y, 0f)).magnitude +
                                Mathf.Min(Mathf.Max(delta.x, delta.y), 0f) - radius;
                            texture.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(0.5f - distance)));
                        }
                    texture.Apply();
                    File.WriteAllBytes(RoundedSpritePath, texture.EncodeToPNG());
                }
                finally { Object.DestroyImmediate(texture); }
            }
            AssetDatabase.ImportAsset(RoundedSpritePath, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(RoundedSpritePath) as TextureImporter;
            Require(importer != null, "분해 Scroll 둥근 배경 임포터");
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spriteBorder = Vector4.one * 16f;
            importer.spritePixelsPerUnit = 100f;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(RoundedSpritePath);
            Require(sprite != null, "분해 Scroll 둥근 배경 스프라이트");
            return sprite;
        }

        private static void Build(GameSceneUIRefs refs)
        {
            RectTransform inventoryPanel = refs.InventoryCloseButton.transform.parent as RectTransform;
            Require(inventoryPanel != null && refs.InventorySlotPrefab != null, "인벤토리 패널/슬롯 프리팹");
            TMP_FontAsset font = refs.InventoryCanvasGroup.GetComponentsInChildren<TMP_Text>(true)
                .Select(text => text.font).FirstOrDefault(asset => asset != null);
            Require(font != null, "기존 UI 폰트");
            Transform existing = inventoryPanel.parent.Find("InventoryDismantlePanel");
            // 재실행 시 이미 연결한 보상 비율과 UI를 보존한다.
            if (existing != null) { UpdatePresentation(refs); BuildBulkSelection(refs.DismantlePanel); return; }
            RectTransform panel = Rect("InventoryDismantlePanel", inventoryPanel.parent);
            panel.anchorMin = inventoryPanel.anchorMin;
            panel.anchorMax = inventoryPanel.anchorMax;
            panel.pivot = inventoryPanel.pivot;
            panel.sizeDelta = inventoryPanel.sizeDelta;
            panel.anchoredPosition = new Vector2(-310f, 30f);
            Image background = panel.gameObject.AddComponent<Image>();
            background.color = new Color(0.09f, 0.085f, 0.1f, 0.98f);
            InventoryDismantlePanel controller = panel.gameObject.AddComponent<InventoryDismantlePanel>();

            RectTransform header = Rect("Header", panel);
            Place(header, new Vector2(0.5f, 1f), new Vector2(0f, -35f), new Vector2(panel.sizeDelta.x - 32f, 44f));
            Text(header, "장비 분해", font, 26f, new Color(1f, 0.85f, 0.55f));

            RectTransform scrollRect = Rect("Scroll", panel);
            Stretch(scrollRect, new Vector2(18f, 132f), new Vector2(-18f, -72f));
            Image scrollImage = scrollRect.gameObject.AddComponent<Image>();
            scrollImage.color = new Color(0.035f, 0.03f, 0.045f, 0.85f);
            ScrollRect scroll = scrollRect.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            RectTransform viewport = Rect("Viewport", scrollRect);
            Stretch(viewport, Vector2.zero, Vector2.zero);
            viewport.gameObject.AddComponent<RectMask2D>();
            Image viewportImage = viewport.gameObject.AddComponent<Image>();
            viewportImage.color = Color.clear;
            RectTransform content = Rect("Content", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = Vector2.one;
            content.pivot = new Vector2(0.5f, 1f);
            content.sizeDelta = Vector2.zero;
            GridLayoutGroup grid = content.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(80f, 80f);
            grid.spacing = new Vector2(12f, 12f);
            grid.padding = new RectOffset(16, 16, 16, 16);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = Mathf.Max(1, Mathf.FloorToInt((panel.sizeDelta.x - 56f) / 92f));
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = content;
            scroll.viewport = viewport;
            RectTransform hint = Rect("EmptyHint", viewport);
            Stretch(hint, new Vector2(20f, 20f), new Vector2(-20f, -20f));
            Text(hint, "분해할 장비를 여기에 드래그하거나\n인벤토리에서 우클릭하세요.", font, 21f, new Color(0.7f, 0.68f, 0.64f));

            GameObject template = Object.Instantiate(refs.InventorySlotPrefab, panel);
            template.name = "DismantleSlotTemplate";
            InventorySlotUI oldSlot = template.GetComponent<InventorySlotUI>();
            DismantleSlotUI newSlot = template.AddComponent<DismantleSlotUI>();
            foreach (FieldInfo field in typeof(BaseSlotUI).GetFields(BindingFlags.Instance | BindingFlags.NonPublic))
                if (field.IsDefined(typeof(SerializeField), true)) field.SetValue(newSlot, field.GetValue(oldSlot));
            Object.DestroyImmediate(oldSlot);
            template.SetActive(false);
            RectTransform reward = Rect("ExpectedGold", panel);
            Place(reward, new Vector2(0.5f, 0f), new Vector2(0f, 101f), new Vector2(panel.sizeDelta.x - 32f, 38f));
            TMP_Text rewardText = Text(reward, "예상 획득 골드: 0 G", font, 22f, new Color(1f, 0.85f, 0.55f));
            Button confirm = Button("DismantleButton", panel, font, "분해", new Vector2(-96f, 47f), new Vector2(168f, 48f));
            Button reset = Button("ResetButton", panel, font, "초기화", new Vector2(96f, 47f), new Vector2(168f, 48f));
            Set(controller, "slotParent", content);
            Set(controller, "slotTemplate", newSlot);
            Set(controller, "dismantleButton", confirm);
            Set(controller, "resetButton", reset);
            Set(controller, "expectedGoldText", rewardText);
            Set(controller, "emptyHint", hint.gameObject);
            panel.gameObject.SetActive(false);

            Button toggle = Button("InventoryDismantleButton", inventoryPanel, font, "분해",
                new Vector2(-90f, -28f), new Vector2(70f, 32f));
            RectTransform toggleRect = toggle.transform as RectTransform;
            toggleRect.anchorMin = toggleRect.anchorMax = Vector2.one;
            toggleRect.anchoredPosition = new Vector2(-90f, -28f);
            Set(refs, "inventoryDismantleButton", toggle);
            Set(refs, "dismantlePanel", controller);
            UpdatePresentation(refs);
            BuildBulkSelection(controller);
        }

        [MenuItem("Tools/Nytherion/Inventory/분해 등급 선택·일괄 등록 설정")]
        public static void ApplyBulkSelection()
        {
            Directory.CreateDirectory(Output);
            const string path = "Assets/Scenes/GameScene.unity";
            Scene scene = SceneManager.GetSceneByPath(path);
            bool loaded = scene.IsValid() && scene.isLoaded;
            if (!loaded) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                GameSceneUIRefs refs = scene.GetRootGameObjects().SelectMany(root =>
                    root.GetComponentsInChildren<GameSceneUIRefs>(true)).Single();
                Require(refs.DismantlePanel != null, "기존 분해 패널 Inspector 참조");
                Require(EditorSceneManager.SaveScene(scene, Output + "/GameScene-before-bulk-" +
                    DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".unity", true), "미저장 변경 포함 씬 백업");
                BuildBulkSelection(refs.DismantlePanel);
                Verify();
                VerifyUI(refs, EditorSceneManager.NewPreviewScene());
                EditorSceneManager.MarkSceneDirty(scene);
                Require(EditorSceneManager.SaveScene(scene), "일괄 등록 UI 씬 저장");
                Capture(refs);
                File.WriteAllText(Output + "/bulk-result.txt",
                    "PASS GameScene 등급 선택·일괄 등록 Inspector 연결, 전체 등급 경계/중복/복귀/장비 필터, UI 버튼 검증 및 씬 저장");
            }
            finally { if (!loaded) EditorSceneManager.CloseScene(scene, true); }
        }

        private static void BuildBulkSelection(InventoryDismantlePanel controller)
        {
            Transform panel = controller.transform;
            TMP_Text sourceLabel = panel.Find("ResetButton/Label").GetComponent<TMP_Text>();
            Color ink = panel.Find("Header").GetComponent<TMP_Text>().color;
            Transform existingDropdown = panel.Find("MaxRarityDropdown");
            TMP_Dropdown dropdown;
            if (existingDropdown != null) dropdown = existingDropdown.GetComponent<TMP_Dropdown>();
            else
            {
                // TMP 표준 템플릿을 사용해 키보드 선택, 목록 스크롤, 외부 클릭 닫기를 유지한다.
                GameObject dropdownObject = TMP_DefaultControls.CreateDropdown(new TMP_DefaultControls.Resources());
                dropdownObject.name = "MaxRarityDropdown";
                dropdownObject.transform.SetParent(panel, false);
                foreach (Transform child in dropdownObject.GetComponentsInChildren<Transform>(true))
                    child.gameObject.layer = panel.gameObject.layer;
                dropdown = dropdownObject.GetComponent<TMP_Dropdown>();
                RectTransform rect = (RectTransform)dropdown.transform;
                Place(rect, new Vector2(0.5f, 1f), new Vector2(-94f, -91f), new Vector2(216f, 40f));
                dropdown.GetComponent<Image>().color = new Color(0.89f, 0.8f, 0.65f);
                dropdown.template.GetComponent<Image>().color = new Color(0.94f, 0.86f, 0.71f);
                dropdown.transform.Find("Arrow").GetComponent<Image>().enabled = false;
                RectTransform arrow = Rect("ArrowLabel", dropdown.transform);
                Place(arrow, new Vector2(1f, 0.5f), new Vector2(-17f, 0f), new Vector2(24f, 32f));
                Text(arrow, "▼", sourceLabel.font, 16f, ink);
                foreach (TMP_Text label in dropdown.GetComponentsInChildren<TMP_Text>(true))
                {
                    label.font = sourceLabel.font;
                    label.fontSize = 20f;
                    label.fontStyle = sourceLabel.fontStyle;
                    label.color = ink;
                    label.raycastTarget = false;
                }
                dropdown.captionText.alignment = TextAlignmentOptions.MidlineLeft;
                dropdown.itemText.alignment = TextAlignmentOptions.MidlineLeft;
                RectTransform item = (RectTransform)dropdown.itemText.transform.parent;
                item.sizeDelta = new Vector2(0f, 36f);
                ((RectTransform)item.parent).sizeDelta = new Vector2(0f, 44f);
                dropdown.template.sizeDelta = new Vector2(0f, 210f);
                dropdown.ClearOptions();
                dropdown.AddOptions(new List<string> { "일반 이하", "고급 이하", "희귀 이하", "영웅 이하", "전설 이하" });
                dropdown.SetValueWithoutNotify((int)Rarity.Common);
                dropdown.RefreshShownValue();
            }
            Transform existingButton = panel.Find("BulkStageButton");
            Button bulk;
            if (existingButton != null) bulk = existingButton.GetComponent<Button>();
            else
            {
                bulk = Object.Instantiate(panel.Find("ResetButton").GetComponent<Button>(), panel);
                bulk.name = "BulkStageButton";
                bulk.onClick = new Button.ButtonClickedEvent();
                Place((RectTransform)bulk.transform, new Vector2(0.5f, 1f), new Vector2(128f, -91f), new Vector2(200f, 40f));
                bulk.GetComponentInChildren<TMP_Text>(true).text = "일괄 등록";
            }
            // 제목 아래 한 줄을 확보하고 기존 골드/확정/초기화 영역은 유지한다.
            RectTransform scroll = (RectTransform)panel.Find("Scroll");
            scroll.offsetMax = new Vector2(scroll.offsetMax.x, -126f);
            Set(controller, "maxRarityDropdown", dropdown);
            Set(controller, "bulkStageButton", bulk);
            EditorUtility.SetDirty(controller);
        }

        private static void UpdatePresentation(GameSceneUIRefs refs)
        {
            Transform panel = refs.DismantlePanel.transform;
            panel.GetComponent<Image>().color = Color.clear;
            panel.Find("Scroll").GetComponent<Image>().color = new Color(0.3f, 0.2f, 0.12f, 0.12f);
            Image scrollImage = panel.Find("Scroll").GetComponent<Image>();
            scrollImage.sprite = GetRoundedSprite();
            scrollImage.type = Image.Type.Sliced;
            Color ink = new Color(0.36f, 0.23f, 0.17f);
            panel.Find("Header").GetComponent<TMP_Text>().color = ink;
            panel.Find("ExpectedGold").GetComponent<TMP_Text>().color = ink;
            panel.Find("Scroll/Viewport/EmptyHint").GetComponent<TMP_Text>().color = new Color(0.46f, 0.34f, 0.25f);
            foreach (string name in new[] { "DismantleButton", "ResetButton" })
                panel.Find(name).GetComponent<Image>().color = new Color(0.45f, 0.31f, 0.2f);
            refs.InventoryDismantleButton.GetComponent<Image>().color = new Color(0.45f, 0.31f, 0.2f);
            (refs.InventoryDismantleButton.transform as RectTransform).anchoredPosition = new Vector2(-126f, -28f);
        }

        private static RectTransform Rect(string name, Transform parent)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.gameObject.layer = parent.gameObject.layer;
            rect.SetParent(parent, false);
            return rect;
        }
        private static void Stretch(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = min; rect.offsetMax = max;
        }
        private static void Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = anchor;
            rect.anchoredPosition = position; rect.sizeDelta = size;
        }
        private static TMP_Text Text(RectTransform rect, string value, TMP_FontAsset font, float size, Color color)
        {
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = font; text.text = value; text.fontSize = size;
            text.alignment = TextAlignmentOptions.Center; text.color = color; text.raycastTarget = false;
            return text;
        }
        private static Button Button(string name, Transform parent, TMP_FontAsset font, string title, Vector2 position, Vector2 size)
        {
            RectTransform rect = Rect(name, parent);
            Place(rect, new Vector2(0.5f, 0f), position, size);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = new Color(0.27f, 0.23f, 0.2f);
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            RectTransform label = Rect("Label", rect);
            Stretch(label, Vector2.zero, Vector2.zero);
            Text(label, title, font, 21f, new Color(1f, 0.91f, 0.75f));
            return button;
        }
        private static void Set(object target, string field, object value) => target.GetType()
            .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        public static void Verify()
        {
            var report = new List<string>();
            var root = new GameObject("분해 격리 검증");
            var assets = new List<EquipmentData>();
            try
            {
                InventoryDataManager inventory = root.AddComponent<InventoryDataManager>();
                CurrencyDataManager currency = root.AddComponent<CurrencyDataManager>();
                inventory.Initialize(); currency.Initialize();
                WeaponData template = AssetDatabase.LoadAssetAtPath<WeaponData>("Assets/Nytherion/Data/ScriptableObjects/Weapons/ArcaneDart.asset");
                for (int i = 0; i < 24; i++)
                {
                    WeaponData item = Object.Instantiate(template);
                    item.instanceId = Guid.NewGuid().ToString();
                    item.ApplyRarityStats((Rarity)(i % 5));
                    assets.Add(item);
                    Require(inventory.AddItemToSlot(item, 1, i), "장비 배치");
                }
                VerifyBulkModel(assets);
                report.Add("PASS 모든 등급 이하 경계, 같은 종류의 개별 장비, 비장비/빈 슬롯 제외, 기존 대기 유지, 중복 방지, 일괄 갱신 1회, 원래 슬롯 복귀");
                Require(inventory.StageForDismantle(3, assets[3]), "장비 이동");
                Require(inventory.GetSlot(3).item == null && inventory.GetDismantleItems()[0].item == assets[3], "정확한 인스턴스 이동");
                Require(!inventory.StageForDismantle(3, assets[3]), "중복 이동 방지");
                Require(!inventory.AddItemToSlot(assets[0], 1, 3, true) && !inventory.SwapItems(0, 3), "원래 슬롯 예약 보호");
                Require(inventory.IsFull(), "복귀 공간 보호");
                SaveData saved = new SaveData();
                inventory.PopulateSaveData(saved);
                Require(saved.inventoryData.Count == 24 && saved.inventoryData[3].instanceId == assets[3].instanceId, "분해 대기 장비 저장 보존");
                ItemDatabase.Initialize(AssetDatabase.LoadAssetAtPath<ItemDatabaseSO>(
                    "Assets/Nytherion/Data/ScriptableObjects/Items/ItemDatabaseSO.asset"));
                InventoryDataManager restored = root.AddComponent<InventoryDataManager>();
                restored.Initialize();
                restored.LoadFromSaveData(JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(saved)));
                Require(restored.GetSlot(3).item.instanceId == assets[3].instanceId &&
                    ((EquipmentData)restored.GetSlot(3).item).rarity == assets[3].rarity &&
                    restored.GetDismantleItems().Count == 0, "재시작 시 대기 장비 복원");
                foreach (var entry in restored.GetAllItems()) assets.Add((EquipmentData)entry.item);
                inventory.SortItems();
                inventory.ResetDismantle();
                Require(inventory.GetSlot(3).item == assets[3], "정렬 후 초기화 시 원래 인스턴스 복귀");
                report.Add("PASS 정확한 인스턴스 이동, 중복/덮어쓰기/교환 방지, 가득 찬 인벤토리 복귀, 대기 장비 저장 보존, 정렬 후 초기화");
                EquipmentData first = (EquipmentData)inventory.GetSlot(0).item;
                EquipmentData second = (EquipmentData)inventory.GetSlot(1).item;
                Require(inventory.StageForDismantle(0, first) && inventory.StageForDismantle(1, second), "일괄 이동");
                long expected = Mathf.RoundToInt(first.baseValue * 0.7f) + Mathf.RoundToInt(second.baseValue * 0.7f);
                Require(inventory.GetDismantleGold(0.7f) == expected, "70% 합계");
                int before = currency.GetGold();
                Require(inventory.DismantleEquipment(currency, 0.7f), "분해 확정");
                Require(currency.GetGold() == before + expected && inventory.GetDismantleItems().Count == 0, "지급/대기 비우기");
                Require(!inventory.DismantleEquipment(currency, 0.7f) && currency.GetGold() == before + expected, "중복 지급 방지");
                inventory.PopulateSaveData(saved);
                Require(saved.inventoryData.Count == 22, "확정 장비 저장 제외");
                report.Add("PASS 70% 예상 합계와 실제 골드 일치, 일괄 분해, 중복 지급 방지, 확정 장비 저장 제외");
                EquipmentData zero = (EquipmentData)inventory.GetSlot(2).item;
                zero.baseValue = 0;
                Require(inventory.StageForDismantle(2, zero) && inventory.DismantleEquipment(currency, 0.7f), "0골드 장비 분해");
                Require(currency.GetGold() == before + expected, "0골드 지급 유지");
                EquipmentData overflow = (EquipmentData)inventory.GetSlot(3).item;
                Require(inventory.StageForDismantle(3, overflow), "실패 검증 장비 이동");
                currency.SetCurrency(CurrencyType.Gold, int.MaxValue);
                Require(!inventory.DismantleEquipment(currency, 0.7f) && inventory.GetDismantleItems().Count == 1, "재화 초과 시 장비 유지");
                Require(!inventory.DismantleEquipment(null, 0.7f), "지급 매니저 누락 시 장비 유지");
                inventory.ResetDismantle();
                report.Add("PASS 가치 0 장비 분해, 재화 초과/매니저 누락 시 장비 보존");
            }
            finally { Object.DestroyImmediate(root); foreach (var asset in assets) Object.DestroyImmediate(asset); }
            File.WriteAllLines(Output + "/verification.txt", report);
        }

        private static void VerifyBulkModel(List<EquipmentData> assets)
        {
            var model = new InventoryModel(8);
            ConsumableData consumable = ScriptableObject.CreateInstance<ConsumableData>();
            try
            {
                for (int i = 0; i < 6; i++) model.AddItemToSlot(assets[i], 1, i);
                model.AddItemToSlot(consumable, 3, 6);
                int notifications = 0;
                model.OnInventoryUpdated += _ => notifications++;
                foreach (Rarity rarity in Enum.GetValues(typeof(Rarity)))
                {
                    notifications = 0;
                    int expected = assets.Take(6).Count(item => item.rarity <= rarity);
                    Require(model.StageForDismantleUpToRarity(rarity) == expected &&
                        model.DismantleSlots.Count == expected && notifications == 1, "등급 이하 경계/일괄 갱신: " + rarity);
                    Require(model.StageForDismantleUpToRarity(rarity) == 0 && notifications == 1,
                        "동일 버튼 반복 등록/알림 방지: " + rarity);
                    Require(model.GetItemAt(6).item == consumable && model.GetItemAt(6).count == 3 &&
                        model.GetItemAt(7).item == null, "소모품/빈 슬롯 제외");
                    model.ResetDismantle();
                    for (int i = 0; i < 6; i++) Require(model.GetItemAt(i).item == assets[i], "원래 장비 인스턴스 복귀");
                }
                Require(model.StageForDismantle(4, assets[4]), "기존 전설 장비 수동 등록");
                Require(model.StageForDismantleUpToRarity(Rarity.Common) == 2 &&
                    model.DismantleSlots.Count == 3 && model.IsDismantleReserved(4), "기존 상위 등급 대기 유지");
                Require(model.StageForDismantleUpToRarity((Rarity)(-1)) == 0 &&
                    model.StageForDismantleUpToRarity((Rarity)99) == 0, "유효하지 않은 등급 거부");
                model.ResetDismantle();
            }
            finally { Object.DestroyImmediate(consumable); }
        }

        [MenuItem("Tools/Nytherion/Inventory/Verify Dismantle UI")]
        public static void VerifyUIOnly()
        {
            Directory.CreateDirectory(Output);
            const string path = "Assets/Scenes/GameScene.unity";
            Scene scene = SceneManager.GetSceneByPath(path);
            bool alreadyOpen = scene.IsValid() && scene.isLoaded;
            try
            {
                if (!alreadyOpen) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                GameSceneUIRefs refs = scene.GetRootGameObjects().SelectMany(root =>
                    root.GetComponentsInChildren<GameSceneUIRefs>(true)).First();
                VerifyUI(refs, EditorSceneManager.NewPreviewScene());
                File.WriteAllText(Output + "/ui-verification.txt", "PASS 격리 UI의 초기화·버튼 토글·우클릭/드롭·예약/복귀·보상 지급. 원본 씬과 소지품 미수정.");
            }
            catch (Exception error)
            {
                File.WriteAllText(Output + "/ui-verification.txt", "FAIL " + error);
                Debug.LogException(error);
            }
            finally { if (!alreadyOpen && scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true); }
        }

        private static void VerifyUI(GameSceneUIRefs source, Scene? preview)
        {
            GameObject root = Object.Instantiate(source.InventoryCanvasGroup.gameObject);
            if (preview.HasValue) SceneManager.MoveGameObjectToScene(root, preview.Value);
            var assets = new List<WeaponData>();
            IObjectResolver resolver = null;
            try
            {
                root.SetActive(true);
                Transform Find(string name) => root.GetComponentsInChildren<Transform>(true).First(item => item.name == name);
                GameSceneUIRefs refs = root.AddComponent<GameSceneUIRefs>();
                Set(refs, "equipmentPanel", Find(source.EquipmentPanel.name).gameObject);
                Set(refs, "statsPanel", Find(source.StatsPanel.name).gameObject);
                Set(refs, "inventorySlotParent", Find(source.InventorySlotParent.name));
                Set(refs, "inventoryCanvasGroup", root.GetComponent<CanvasGroup>());
                Set(refs, "inventoryCloseButton", Find(source.InventoryCloseButton.name).GetComponent<Button>());
                Set(refs, "inventoryDismantleButton", Find("InventoryDismantleButton").GetComponent<Button>());
                Set(refs, "dismantlePanel", Find("InventoryDismantlePanel").GetComponent<InventoryDismantlePanel>());
                InventoryDataManager inventory = root.AddComponent<InventoryDataManager>();
                CurrencyDataManager currency = root.AddComponent<CurrencyDataManager>();
                EventManager events = root.AddComponent<EventManager>();
                inventory.Initialize(); currency.Initialize();
                var builder = new ContainerBuilder();
                builder.RegisterInstance(inventory); builder.RegisterInstance(currency); builder.RegisterInstance(events);
                resolver = builder.Build();
                foreach (Transform child in refs.InventorySlotParent.Cast<Transform>().ToArray()) Object.DestroyImmediate(child.gameObject);
                var sourceSlots = new List<InventorySlotUI>();
                for (int i = 0; i < 2; i++)
                {
                    var slot = Object.Instantiate(source.InventorySlotPrefab, refs.InventorySlotParent).GetComponent<InventorySlotUI>();
                    // 편집 모드 검증에서는 일반 MonoBehaviour의 Awake가 자동 실행되지 않는다.
                    if (!Application.isPlaying) typeof(InventorySlotUI).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(slot, null);
                    slot.Initialize(i); sourceSlots.Add(slot);
                }
                InventoryUI ui = root.AddComponent<InventoryUI>();
                var presenter = root.AddComponent<Nytherion.UI.Presenters.InventoryPresenter>();
                presenter.Construct(resolver, refs, null);
                ui.Construct(resolver, refs);
                ui.Initialize(); ui.Initialize(); ui.Open(false);
                foreach (var slot in sourceSlots) { slot.Construct(null, null, inventory, null, null); slot.BindInventoryUI(ui); }
                refs.EquipmentPanel.SetActive(true); refs.StatsPanel.SetActive(true);
                WeaponData template = AssetDatabase.LoadAssetAtPath<WeaponData>("Assets/Nytherion/Data/ScriptableObjects/Weapons/ArcaneDart.asset");
                for (int i = 0; i < 2; i++)
                {
                    WeaponData item = Object.Instantiate(template); item.instanceId = Guid.NewGuid().ToString();
                    item.ApplyRarityStats(i == 0 ? Rarity.Epic : Rarity.Legendary); assets.Add(item);
                    inventory.AddItemToSlot(item, 1, i);
                }
                refs.InventoryDismantleButton.onClick.Invoke();
                Require(ui.IsDismantleOpen && !refs.EquipmentPanel.activeSelf && !refs.StatsPanel.activeSelf, "분해 패널 토글/기존 패널 숨김");
                sourceSlots[0].OnPointerClick(new PointerEventData(null) { button = PointerEventData.InputButton.Right });
                refs.DismantlePanel.OnDrop(new PointerEventData(null) { button = PointerEventData.InputButton.Left, pointerDrag = sourceSlots[1].gameObject });
                Transform content = refs.DismantlePanel.transform.Find("Scroll/Viewport/Content");
                Require(inventory.GetDismantleItems().Count == 2 && content.GetComponentsInChildren<DismantleSlotUI>().Length == 2,
                    $"우클릭/드래그 동적 슬롯: 대기={inventory.GetDismantleItems().Count}, 표시={content.GetComponentsInChildren<DismantleSlotUI>().Length}, 원본={sourceSlots[0].CurrentItem}/{sourceSlots[1].CurrentItem}, 열림={ui.IsDismantleOpen}");
                DismantleSlotUI[] staged = content.GetComponentsInChildren<DismantleSlotUI>();
                Require(staged[0].CurrentItem == assets[0] && staged[0].CurrentSlotBackgroundSprite != staged[1].CurrentSlotBackgroundSprite, "장비 인스턴스/등급 배경");
                Find("ResetButton").GetComponent<Button>().onClick.Invoke();
                Require(inventory.GetSlot(0).item == assets[0] && inventory.GetSlot(1).item == assets[1], "초기화 버튼 복귀");
                TMP_Dropdown rarityDropdown = Find("MaxRarityDropdown").GetComponent<TMP_Dropdown>();
                Button bulkButton = Find("BulkStageButton").GetComponent<Button>();
                Require(rarityDropdown.options.Count == 5 && rarityDropdown.value == (int)Rarity.Common &&
                    !bulkButton.interactable, "전체 등급 목록/일반 기본값/대상 없을 때 버튼 비활성화");
                rarityDropdown.value = (int)Rarity.Epic;
                Require(bulkButton.interactable && inventory.GetDismantleItems().Count == 0, "등급 변경만으로 등록하지 않음");
                int goldBeforeStage = currency.GetGold();
                bulkButton.onClick.Invoke();
                Require(inventory.GetDismantleItems().Count == 1 && inventory.GetDismantleItems()[0].item == assets[0] &&
                    inventory.GetSlot(1).item == assets[1] && !bulkButton.interactable && currency.GetGold() == goldBeforeStage,
                    "선택 등급 이하만 일괄 등록/상위 등급 보존/즉시 분해 방지");
                bulkButton.onClick.Invoke();
                Require(inventory.GetDismantleItems().Count == 1, "버튼 중복 호출 방지");
                rarityDropdown.value = (int)Rarity.Legendary;
                Require(bulkButton.interactable, "등급 변경 후 추가 대상 반영");
                bulkButton.onClick.Invoke();
                Require(inventory.GetDismantleItems().Count == 2 && !bulkButton.interactable, "기존 대기 유지/전설까지 추가 등록");
                Find("ResetButton").GetComponent<Button>().onClick.Invoke();
                Require(bulkButton.interactable && inventory.GetSlot(0).item == assets[0] && inventory.GetSlot(1).item == assets[1],
                    "초기화 후 일괄 등록 대상 복구");
                sourceSlots[0].OnPointerClick(new PointerEventData(null) { button = PointerEventData.InputButton.Right });
                long expected = inventory.GetDismantleGold(0.7f);
                int gold = currency.GetGold();
                Find("DismantleButton").GetComponent<Button>().onClick.Invoke();
                Require(currency.GetGold() == gold + expected && inventory.GetSlot(0).item == null, "분해 버튼 지급");
                sourceSlots[1].OnPointerClick(new PointerEventData(null) { button = PointerEventData.InputButton.Right });
                refs.InventoryDismantleButton.onClick.Invoke();
                Require(!ui.IsDismantleOpen && refs.EquipmentPanel.activeSelf && refs.StatsPanel.activeSelf && inventory.GetSlot(1).item == assets[1], "재클릭 패널 복원/장비 복귀");
                refs.InventoryDismantleButton.onClick.Invoke();
                sourceSlots[1].OnPointerClick(new PointerEventData(null) { button = PointerEventData.InputButton.Right });
                ui.Close();
                Require(!ui.IsDismantleOpen && inventory.GetSlot(1).item == assets[1], "인벤토리 닫기 복귀");
                File.WriteAllText(Output + "/" + source.gameObject.scene.name + "-ui-verification.txt",
                    "PASS 버튼 토글, 우클릭/드래그 이동, 등급 선택/일괄 등록/중복 방지/상위 등급 보존, 동적 슬롯과 등급 배경, 초기화 복귀, 분해 지급, 재클릭/닫기 복귀");
            }
            finally
            {
                Object.DestroyImmediate(root); resolver?.Dispose();
                foreach (var asset in assets) Object.DestroyImmediate(asset);
                if (preview.HasValue) EditorSceneManager.ClosePreviewScene(preview.Value);
            }
        }

        private static void Capture(GameSceneUIRefs refs)
        {
            Scene scene = EditorSceneManager.NewPreviewScene();
            RenderTexture target = null;
            Texture2D pixels = null;
            var assets = new List<WeaponData>();
            try
            {
                GameObject root = Object.Instantiate(refs.InventoryCanvasGroup.gameObject);
                SceneManager.MoveGameObjectToScene(root, scene);
                root.SetActive(true);
                root.GetComponent<CanvasGroup>().alpha = 1f;
                InventoryDismantlePanel controller = root.GetComponentsInChildren<InventoryDismantlePanel>(true).First();
                Transform equip = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(item => item.name == refs.EquipmentPanel.name);
                Transform stats = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(item => item.name == refs.StatsPanel.name);
                if (equip != null) equip.gameObject.SetActive(false);
                if (stats != null) stats.gameObject.SetActive(false);
                var inventory = root.AddComponent<InventoryDataManager>();
                var currency = root.AddComponent<CurrencyDataManager>();
                inventory.Initialize(); currency.Initialize();
                controller.Initialize(inventory, currency, null, null, null);
                controller.Toggle();
                WeaponData template = AssetDatabase.LoadAssetAtPath<WeaponData>("Assets/Nytherion/Data/ScriptableObjects/Weapons/ArcaneDart.asset");
                foreach (Rarity rarity in new[] { Rarity.Rare, Rarity.Epic, Rarity.Legendary })
                {
                    WeaponData item = Object.Instantiate(template); item.ApplyRarityStats(rarity); assets.Add(item);
                    int index = assets.Count - 1;
                    inventory.AddItemToSlot(item, 1, index);
                    inventory.StageForDismantle(index, item);
                }
                Canvas canvas = root.GetComponent<Canvas>() ?? root.GetComponentInChildren<Canvas>();
                GameObject cameraObject = new GameObject("분해 미리보기 카메라");
                SceneManager.MoveGameObjectToScene(cameraObject, scene);
                Camera camera = cameraObject.AddComponent<Camera>();
                camera.scene = scene; camera.enabled = false; camera.orthographic = true; camera.orthographicSize = 540f;
                camera.transform.position = new Vector3(0f, 0f, -10f);
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(0.07f, 0.07f, 0.09f);
                target = new RenderTexture(1920, 1080, 24);
                camera.targetTexture = target;
                canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1f;
                Canvas.ForceUpdateCanvases(); camera.Render();
                pixels = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
                RenderTexture previous = RenderTexture.active;
                try { RenderTexture.active = target; pixels.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0); pixels.Apply();
                    File.WriteAllBytes(Output + "/" + refs.gameObject.scene.name + "-preview.png", pixels.EncodeToPNG()); }
                finally { RenderTexture.active = previous; }
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
                if (target != null) { target.Release(); Object.DestroyImmediate(target); }
                if (pixels != null) Object.DestroyImmediate(pixels);
                foreach (var asset in assets) Object.DestroyImmediate(asset);
            }
        }
    }
}
