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
            if (existing != null) { UpdatePresentation(refs); return; }
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
                ui.Construct(resolver, refs, null);
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
                    "PASS 버튼 토글, 우클릭/드래그 이동, 동적 슬롯과 등급 배경, 초기화 복귀, 분해 지급, 재클릭/닫기 복귀");
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
