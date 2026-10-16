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
using Nytherion.UI.Controllers;
using Nytherion.UI.Components;
using Nytherion.UI.Inventory;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using VContainer;
using Object = UnityEngine.Object;

namespace Nytherion.Editor
{
    /// <summary>정렬 버튼의 씬 참조를 Unity 에디터에서 설정하고 인스턴스·저장·UI 갱신을 검증한다.</summary>
    [InitializeOnLoad]
    public static class InventorySortSetup
    {
        private const string Output = "output/inventory-sort";
        private const string SpritePath = "Assets/Nytherion/Art/UI/Inventory/SortButton.png";
        private const string PendingPlay = "Nytherion.InventorySort.VerifyPlay";
        private static readonly string[] ScenePaths = { "Assets/Scenes/GameScene.unity" };
        private static double playReadyAt;

        static InventorySortSetup()
        {
            EditorApplication.update += Update;
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredPlayMode)
                    playReadyAt = EditorApplication.timeSinceStartup + 8d;
            };
        }

        private static void Update()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            if (SessionState.GetBool(PendingPlay, false) && EditorApplication.isPlaying &&
                EditorApplication.timeSinceStartup >= playReadyAt)
            {
                SessionState.SetBool(PendingPlay, false);
                try
                {
                    GameSceneUIRefs refs = Object.FindObjectOfType<GameSceneUIRefs>();
                    Require(refs != null && refs.InventorySortButton != null, "실행 씬의 정렬 버튼 참조");
                    VerifyButton(refs.InventorySortButton, null);
                    File.WriteAllText(Output + "/play-verification.txt",
                        "PASS " + SceneManager.GetActiveScene().name + " 플레이 모드: 씬 정렬 버튼 복제본의 클릭 → 데이터 정렬 → 24개 슬롯 갱신. 실제 소지품/세이브는 수정하지 않음.");
                }
                catch (Exception error) { File.WriteAllText(Output + "/play-verification.txt", "FAIL " + error); }
                finally { if (SessionState.GetBool(PendingPlay + ".Exit", false)) EditorApplication.isPlaying = false; }
                return;
            }
            if (File.Exists(Output + "/play.request"))
            {
                File.Delete(Output + "/play.request");
                SessionState.SetBool(PendingPlay + ".Exit", !EditorApplication.isPlaying);
                SessionState.SetBool(PendingPlay, true);
                playReadyAt = EditorApplication.timeSinceStartup + 12d;
                if (!EditorApplication.isPlaying) EditorApplication.isPlaying = true;
                return;
            }
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (File.Exists(Output + "/verify.request"))
            {
                File.Delete(Output + "/verify.request");
                try { Verify(); }
                catch (Exception error) { File.WriteAllText(Output + "/verification.txt", "FAIL " + error); }
                return;
            }
            if (File.Exists(Output + "/pixels.request"))
            {
                File.Delete(Output + "/pixels.request");
                try { VerifyPixels(); }
                catch (Exception error) { File.WriteAllText(Output + "/pixel-verification.txt", "FAIL " + error); }
                return;
            }
            if (!File.Exists(Output + "/setup.request")) return;
            File.Delete(Output + "/setup.request");
            try { Apply(); Verify(); }
            catch (Exception error)
            {
                File.WriteAllText(Output + "/verification.txt", "FAIL " + error);
                Debug.LogException(error);
            }
        }

        [MenuItem("Tools/Nytherion/Inventory/Apply Sort Button")]
        public static void Apply()
        {
            Directory.CreateDirectory(Output);
            AssetDatabase.ImportAsset(SpritePath, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(SpritePath) as TextureImporter;
            Require(importer != null, "정렬 이미지 임포터");
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.spritePixelsPerUnit = 100f;
            importer.SaveAndReimport();
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
            Require(sprite != null, "정렬 버튼 스프라이트");

            var pendingSaves = new List<string>();
            foreach (string path in ScenePaths)
            {
                Scene scene = SceneManager.GetSceneByPath(path);
                bool alreadyOpen = scene.IsValid() && scene.isLoaded;
                bool wasDirty = alreadyOpen && scene.isDirty;
                if (!alreadyOpen) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                try
                {
                    GameSceneUIRefs refs = FindRefs(scene);
                    Require(refs != null && refs.InventoryCloseButton != null, path + " 인벤토리 UI 참조");
                    Transform panel = refs.InventoryCloseButton.transform.parent;
                    Transform existing = panel.Find("InventorySortButton");
                    var buttonObject = existing != null ? existing.gameObject :
                        new GameObject("InventorySortButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
                    buttonObject.layer = panel.gameObject.layer;
                    buttonObject.transform.SetParent(panel, false);
                    var rect = buttonObject.GetComponent<RectTransform>();
                    // 보관함 헤더 오른쪽에 두고 스크롤 영역 밖에서 항상 표시한다.
                    rect.anchorMin = rect.anchorMax = Vector2.one;
                    rect.pivot = Vector2.one * 0.5f;
                    rect.anchoredPosition = new Vector2(-32f, -28f);
                    rect.sizeDelta = new Vector2(32f, 32f);
                    Image image = buttonObject.GetComponent<Image>();
                    image.sprite = sprite;
                    image.type = Image.Type.Simple;
                    image.preserveAspect = true;
                    image.raycastTarget = true;
                    Button button = buttonObject.GetComponent<Button>();
                    PixelPerfectButtonImage.Apply(image, DetectPixelBlockSize());
                    button.targetGraphic = image;
                    button.interactable = true;
                    var serialized = new SerializedObject(refs);
                    serialized.FindProperty("inventorySortButton").objectReferenceValue = button;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    EditorSceneManager.MarkSceneDirty(scene);
                    if (wasDirty) pendingSaves.Add(path);
                    else Require(EditorSceneManager.SaveScene(scene), path + " 저장");
                }
                finally { if (!alreadyOpen) EditorSceneManager.CloseScene(scene, true); }
            }
            File.WriteAllText(Output + "/setup.txt",
                pendingSaves.Count == 0 ? "PASS GameScene 정렬 버튼 및 Inspector 참조 저장, SortButton 스프라이트 임포트" :
                "PENDING_SAVE 기존 미저장 수정 보존: " + string.Join(", ", pendingSaves) + ". 정렬 버튼/참조는 에디터에 추가했고 해당 씬 저장만 남았습니다.");
        }

        [MenuItem("Tools/Nytherion/Inventory/Verify Sort Button")]
        public static void Verify()
        {
            Directory.CreateDirectory(Output);
            var results = new List<string>();
            Scene scene = EditorSceneManager.NewPreviewScene();
            var assets = new List<ItemData>();
            try
            {
                InventoryDataManager manager = CreateObject(scene, "정렬 데이터 검증").AddComponent<InventoryDataManager>();
                manager.Initialize();
                ItemData[] expected = FillCases(manager, assets);
                int updates = 0;
                manager.OnDataChanged += _ => updates++;
                Require(manager.SortItems(), "정렬 실행");
                CheckOrder(manager, expected);
                Require(updates == 1, "정렬 완료 알림 1회");
                Require(!manager.SortItems() && updates == 1, "반복 정렬 시 순서/알림 유지");
                results.Add("PASS 5개 등급 내림차순, 대소문자 무시 이름순, 한글/ID 이름 대체, 같은 이름 안정 정렬, 빈 슬롯 뒤로 배치");
                results.Add("PASS 동일 ID의 개별 장비/등급/인스턴스 ID와 분리된 스택 수량 보존, 전체 갱신 1회, 반복 클릭 순서 유지");
                VerifySaveRoundTrip(scene, assets);
                results.Add("PASS 기존 JSON 세이브 형식으로 정렬 슬롯·등급·수량·인스턴스 ID 저장 후 새 매니저에 복원");
                var empty = new InventoryModel(0);
                Require(!empty.SortByRarityAndName(), "0개 슬롯 정렬");
                var one = new InventoryModel(1);
                one.AddItem(expected[0], 1);
                Require(!one.SortByRarityAndName(), "1개 슬롯 정렬");
                var full = new InventoryModel(expected.Length);
                for (int i = 0; i < expected.Length; i++) full.AddItemToSlot(expected[expected.Length - 1 - i], 1, i);
                Require(full.SortByRarityAndName(), "가득 찬 인벤토리 정렬");
                for (int i = 0; i < expected.Length; i++) Require(ReferenceEquals(full.GetItemAt(i).item, expected[i == 6 ? 7 : i == 7 ? 6 : i]), "전체 슬롯 정렬");
                results.Add("PASS 빈 인벤토리, 단일 아이템, 가득 찬 인벤토리");

                foreach (string path in ScenePaths)
                {
                    Scene target = SceneManager.GetSceneByPath(path);
                    bool alreadyOpen = target.IsValid() && target.isLoaded;
                    if (!alreadyOpen) target = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                    try
                    {
                        GameSceneUIRefs refs = FindRefs(target);
                        Require(refs.InventorySortButton != null, path + " 버튼 참조");
                        Require(refs.InventorySortButton.image.sprite == AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath), path + " 이미지 참조");
                        VerifyButton(refs.InventorySortButton, scene);
                        CapturePanel(refs);
                        results.Add("PASS " + path + ": Inspector 버튼/스프라이트 참조, 실제 InventoryUI 클릭 연결과 24개 슬롯 즉시 갱신");
                    }
                    finally { if (!alreadyOpen) EditorSceneManager.CloseScene(target, true); }
                }
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
                foreach (ItemData item in assets) Object.DestroyImmediate(item);
            }
            File.WriteAllLines(Output + "/verification.txt", results);
        }

        private static ItemData[] FillCases(InventoryDataManager manager, List<ItemData> assets)
        {
            var template = AssetDatabase.LoadAssetAtPath<WeaponData>("Assets/Nytherion/Data/ScriptableObjects/Weapons/ArcaneDart.asset");
            Require(template != null, "검증 무기 원본");
            ItemData Make(string name, Rarity rarity)
            {
                WeaponData item = Object.Instantiate(template);
                item.itemName_EN = name;
                item.itemName_KR = name;
                item.instanceId = Guid.NewGuid().ToString();
                item.ApplyRarityStats(rarity);
                assets.Add(item);
                return item;
            }
            ItemData a = Make("Alpha", Rarity.Legendary);
            ItemData b = Make("zeta", Rarity.Legendary);
            ItemData c = Make("Alpha", Rarity.Epic);
            ItemData d = Make("Alpha", Rarity.Rare);
            ItemData e = Make("Alpha", Rarity.Uncommon);
            ItemData f = Make("apple", Rarity.Common);
            ItemData same = Make("APPLE", Rarity.Common);
            var potion = ScriptableObject.CreateInstance<ConsumableData>();
            potion.itemName_EN = "Potion";
            assets.Add(potion);
            ItemData kr = Make("", Rarity.Common);
            kr.itemName_KR = "가나다";
            ItemData unnamed = Make("", Rarity.Rare);
            // 같은 ID의 여러 장비는 복제하거나 병합하지 않는다.
            ItemData[] expected = { a, b, c, unnamed, d, e, f, same, potion, potion, kr };
            manager.AddItemToSlot(b, 1, 2);
            manager.AddItemToSlot(same, 1, 15);
            manager.AddItemToSlot(f, 1, 14);
            manager.AddItemToSlot(a, 1, 7);
            manager.AddItemToSlot(c, 1, 4);
            manager.AddItemToSlot(d, 1, 6);
            manager.AddItemToSlot(e, 1, 11);
            manager.AddItemToSlot(potion, 7, 18);
            manager.AddItemToSlot(potion, 3, 20);
            manager.AddItemToSlot(kr, 1, 21);
            manager.AddItemToSlot(unnamed, 1, 22);
            // ID는 GUID이므로 숫자/영문 이름보다 앞에 오도록 명시적으로 고정한다.
            typeof(ItemData).GetField("uniqueID", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(unnamed, "000-unnamed");
            return expected;
        }

        private static void CheckOrder(InventoryDataManager manager, ItemData[] expected)
        {
            for (int i = 0; i < expected.Length; i++)
            {
                var slot = manager.GetSlot(i);
                Require(ReferenceEquals(slot.item, expected[i]), "슬롯 " + i + " 인스턴스 유지");
                Require(slot.count == (i == 8 ? 7 : i == 9 ? 3 : 1), "슬롯 " + i + " 수량 유지");
            }
            for (int i = expected.Length; i < manager.MaxSlotCount; i++)
                Require(manager.GetSlot(i).item == null && manager.GetSlot(i).count == 0, "뒤쪽 빈 슬롯");
        }

        private static void VerifySaveRoundTrip(Scene scene, List<ItemData> assets)
        {
            ItemDatabase.Initialize(AssetDatabase.LoadAssetAtPath<ItemDatabaseSO>(
                "Assets/Nytherion/Data/ScriptableObjects/Items/ItemDatabaseSO.asset"));
            WeaponData template = ItemDatabase.GetAllItems().OfType<WeaponData>().First();
            var source = CreateObject(scene, "저장 검증").AddComponent<InventoryDataManager>();
            var restored = CreateObject(scene, "복원 검증").AddComponent<InventoryDataManager>();
            source.Initialize();
            restored.Initialize();
            foreach (Rarity rarity in new[] { Rarity.Common, Rarity.Legendary, Rarity.Rare })
            {
                WeaponData item = Object.Instantiate(template);
                item.instanceId = Guid.NewGuid().ToString();
                item.ApplyRarityStats(rarity);
                assets.Add(item);
                source.AddItemToSlot(item, 1, (int)rarity + 3);
            }
            source.SortItems();
            var saved = new SaveData();
            source.PopulateSaveData(saved);
            restored.LoadFromSaveData(JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(saved)));
            for (int i = 0; i < source.MaxSlotCount; i++)
            {
                var before = source.GetSlot(i);
                var after = restored.GetSlot(i);
                Require(before.count == after.count && before.item?.ID == after.item?.ID &&
                    before.item?.instanceId == after.item?.instanceId, "저장/복원 슬롯 " + i);
                if (after.item is EquipmentData equipment)
                {
                    Require(equipment.rarity == ((EquipmentData)before.item).rarity, "복원 등급");
                    assets.Add(equipment);
                }
            }
        }

        private static void VerifyButton(Button template, Scene? targetScene)
        {
            var assets = new List<ItemData>();
            GameObject root = new GameObject("정렬 버튼 격리 검증", typeof(RectTransform), typeof(CanvasGroup));
            if (targetScene.HasValue) SceneManager.MoveGameObjectToScene(root, targetScene.Value);
            IObjectResolver resolver = null;
            try
            {
                InventoryDataManager manager = root.AddComponent<InventoryDataManager>();
                manager.Initialize();
                ItemData[] expected = FillCases(manager, assets);
                EventManager events = root.AddComponent<EventManager>();
                var builder = new ContainerBuilder();
                builder.RegisterInstance(manager);
                builder.RegisterInstance(events);
                resolver = builder.Build();
                GameSceneUIRefs refs = root.AddComponent<GameSceneUIRefs>();
                Button button = Object.Instantiate(template, root.transform);
                Button close = new GameObject("닫기", typeof(RectTransform), typeof(Button)).GetComponent<Button>();
                close.transform.SetParent(root.transform, false);
                Transform parent = new GameObject("검증 슬롯", typeof(RectTransform)).transform;
                parent.SetParent(root.transform, false);
                SetField(refs, "inventorySortButton", button);
                SetField(refs, "inventoryCloseButton", close);
                SetField(refs, "inventoryCanvasGroup", root.GetComponent<CanvasGroup>());
                SetField(refs, "inventorySlotParent", parent);
                for (int i = 0; i < manager.MaxSlotCount; i++)
                {
                    var slotObject = new GameObject("검증 슬롯 " + i, typeof(RectTransform));
                    slotObject.transform.SetParent(parent, false);
                    slotObject.AddComponent<InventorySlotUI>().Initialize(i);
                }
                InventoryUI ui = root.AddComponent<InventoryUI>();
                var presenter = root.AddComponent<Nytherion.UI.Presenters.InventoryPresenter>();
                presenter.Construct(resolver, refs, null);
                ui.Construct(resolver, refs);
                ui.Initialize();
                ui.Open(false);
                button.onClick.Invoke();
                CheckOrder(manager, expected);
                InventorySlotUI[] slots = parent.GetComponentsInChildren<InventorySlotUI>(true);
                for (int i = 0; i < expected.Length; i++)
                    Require(ReferenceEquals(slots[i].CurrentItem, expected[i]), "클릭 후 UI 슬롯 " + i);
                button.onClick.Invoke();
                CheckOrder(manager, expected);
                ui.Close();
            }
            finally
            {
                Object.DestroyImmediate(root);
                resolver?.Dispose();
                foreach (ItemData item in assets) Object.DestroyImmediate(item);
            }
        }

        private static void CapturePanel(GameSceneUIRefs source)
        {
            Scene scene = EditorSceneManager.NewPreviewScene();
            RenderTexture target = null;
            Texture2D pixels = null;
            try
            {
                GameObject root = Object.Instantiate(source.InventoryCanvasGroup.gameObject);
                SceneManager.MoveGameObjectToScene(root, scene);
                Canvas canvas = root.GetComponent<Canvas>();
                if (canvas == null) canvas = root.GetComponentInChildren<Canvas>();
                Require(canvas != null, "미리보기 Canvas");
                CanvasGroup group = root.GetComponent<CanvasGroup>();
                group.alpha = 1f;
                root.SetActive(true);
                GameObject cameraObject = CreateObject(scene, "정렬 버튼 미리보기 카메라");
                Camera camera = cameraObject.AddComponent<Camera>();
                camera.scene = scene;
                camera.enabled = false;
                camera.orthographic = true;
                camera.orthographicSize = 540f;
                camera.nearClipPlane = 0.1f;
                camera.farClipPlane = 100f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.08f, 0.08f, 0.1f);
                camera.transform.position = new Vector3(0f, 0f, -10f);
                target = new RenderTexture(1920, 1080, 24);
                camera.targetTexture = target;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1f;
                Canvas.ForceUpdateCanvases();
                camera.Render();
                pixels = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
                RenderTexture previous = RenderTexture.active;
                try
                {
                    RenderTexture.active = target;
                    pixels.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
                    pixels.Apply();
                    File.WriteAllBytes(Output + "/" + source.gameObject.scene.name + "-preview.png", pixels.EncodeToPNG());
                }
                finally { RenderTexture.active = previous; }
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
                if (target != null) { target.Release(); Object.DestroyImmediate(target); }
                if (pixels != null) Object.DestroyImmediate(pixels);
            }
        }


        [MenuItem("Tools/Nytherion/Inventory/Verify Sort Button Pixels")]
        public static void VerifyPixels()
        {
            Directory.CreateDirectory(Output);
            Scene scene = EditorSceneManager.NewPreviewScene();
            RenderTexture target = null;
            Texture2D readback = null;
            Texture2D original = new Texture2D(2, 2);
            var results = new List<string>();
            try
            {
                original.LoadImage(File.ReadAllBytes(SpritePath));
                int block = DetectPixelBlockSize();
                Vector2 logicalSize = new Vector2(original.width / block, original.height / block);
                var camera = CreateObject(scene, "버튼 픽셀 검증 카메라").AddComponent<Camera>();
                camera.scene = scene;
                camera.enabled = false;
                camera.orthographic = true;
                camera.orthographicSize = 320f;
                camera.nearClipPlane = 0.1f;
                camera.farClipPlane = 10f;
                camera.transform.position = new Vector3(20000f, 20000f, -5f);
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.magenta;
                target = new RenderTexture(640, 640, 24) { antiAliasing = 1 };
                camera.targetTexture = target;
                GameObject canvasObject = new GameObject("버튼 검증 Canvas", typeof(RectTransform), typeof(Canvas));
                SceneManager.MoveGameObjectToScene(canvasObject, scene);
                Canvas canvas = canvasObject.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1f;
                var buttonObject = new GameObject("버튼", typeof(RectTransform), typeof(Image));
                buttonObject.transform.SetParent(canvasObject.transform, false);
                Image image = buttonObject.GetComponent<Image>();
                image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
                image.preserveAspect = true;
                PixelPerfectButtonImage.Apply(image, block);
                RectTransform rect = image.rectTransform;
                rect.sizeDelta = new Vector2(32f, 32f);
                readback = new Texture2D(640, 640, TextureFormat.RGB24, false);
                foreach (float density in new[] { 0.24f, 0.5f, 0.75f, 1f, 1.25f, 1.5f, 2f })
                {
                    canvas.scaleFactor = density;
                    foreach (Vector2 position in new[] { Vector2.zero, new Vector2(0.37f, 0.63f), new Vector2(5.21f, -3.47f) })
                    {
                        rect.anchoredPosition = position;
                        image.SetVerticesDirty();
                        Canvas.ForceUpdateCanvases();
                        camera.Render();
                        RenderTexture previous = RenderTexture.active;
                        try
                        {
                            RenderTexture.active = target;
                            readback.ReadPixels(new Rect(0, 0, 640, 640), 0, 0);
                            readback.Apply();
                        }
                        finally { RenderTexture.active = previous; }
                        Vector2 center = RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.center));
                        int scale = Mathf.Max(1, Mathf.FloorToInt(32f * density / logicalSize.x + 0.5001f));
                        int left = Mathf.RoundToInt(center.x - logicalSize.x * scale * 0.5f);
                        int bottom = Mathf.RoundToInt(center.y - logicalSize.y * scale * 0.5f);
                        foreach (Vector2 click in new[] { new Vector2(left + 0.5f, bottom + 0.5f),
                            new Vector2(left + logicalSize.x * scale - 0.5f, bottom + logicalSize.y * scale - 0.5f) })
                            Require(RectTransformUtility.RectangleContainsScreenPoint(rect, click, camera, image.raycastPadding),
                                "보정된 그림 가장자리 클릭 영역");
                        Require(!RectTransformUtility.RectangleContainsScreenPoint(rect,
                            new Vector2(left - 0.5f, center.y), camera, image.raycastPadding), "그림 바깥 클릭 제외");
                        for (int y = 0; y < (int)logicalSize.y; y++)
                            for (int x = 0; x < (int)logicalSize.x; x++)
                            {
                                Color32 expectedBlock = readback.GetPixel(left + x * scale, bottom + y * scale);
                                for (int dy = 0; dy < scale; dy++)
                                    for (int dx = 0; dx < scale; dx++)
                                        Require(((Color32)readback.GetPixel(left + x * scale + dx, bottom + y * scale + dy)).Equals(expectedBlock),
                                            "픽셀 블록 불균일 배율=" + density + " 좌표=" + x + "," + y);
                                // 원본의 불투명 검은 선은 같은 배율의 사각형 블록으로 빠짐없이 남아야 한다.
                                Color32 source = original.GetPixel(x * block, y * block);
                                if (source.a == 255 && source.r == 0 && source.g == 0 && source.b == 0)
                                    Require(expectedBlock.r == 0 && expectedBlock.g == 0 && expectedBlock.b == 0, "검은 선 보존");
                            }
                        if (position == Vector2.zero)
                            File.WriteAllBytes(Output + "/button-scale-" + density.ToString(System.Globalization.CultureInfo.InvariantCulture) + ".png",
                                readback.EncodeToPNG());
                    }
                    results.Add("PASS Canvas 배율 " + density + ": 3개 위치에서 모든 논리 픽셀 블록/검은 선 두께 균일, 그림 가장자리 클릭 영역 일치");
                }
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
                if (target != null) { target.Release(); Object.DestroyImmediate(target); }
                if (readback != null) Object.DestroyImmediate(readback);
                Object.DestroyImmediate(original);
            }
            File.WriteAllLines(Output + "/pixel-verification.txt", results);
        }


        private static int DetectPixelBlockSize()
        {
            var texture = new Texture2D(2, 2);
            try
            {
                Require(texture.LoadImage(File.ReadAllBytes(SpritePath)), "정렬 원본 이미지 읽기");
                Color32[] pixels = texture.GetPixels32();
                for (int block = Mathf.Min(texture.width, texture.height); block > 1; block--)
                {
                    if (texture.width % block != 0 || texture.height % block != 0) continue;
                    bool uniform = true;
                    for (int y = 0; y < texture.height && uniform; y++)
                        for (int x = 0; x < texture.width; x++)
                            if (!pixels[y * texture.width + x].Equals(pixels[(y / block * block) * texture.width + x / block * block]))
                            { uniform = false; break; }
                    if (uniform) return block;
                }
                return 1;
            }
            finally { Object.DestroyImmediate(texture); }
        }

        private static GameSceneUIRefs FindRefs(Scene scene) => scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<GameSceneUIRefs>(true)).FirstOrDefault();

        private static GameObject CreateObject(Scene scene, string name)
        {
            var root = new GameObject(name);
            SceneManager.MoveGameObjectToScene(root, scene);
            return root;
        }

        private static void SetField(object target, string name, object value) =>
            target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
