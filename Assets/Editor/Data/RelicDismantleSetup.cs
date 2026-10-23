using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Nytherion.Core.Data;
using Nytherion.Core.Enums;
using Nytherion.Core.Managers;
using Nytherion.Data.ScriptableObjects.Relics;
using Nytherion.GamePlay.Relics;
using Nytherion.UI.Inventory;
using Nytherion.UI.RelicBoard;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using VContainer;
using Object = UnityEngine.Object;

namespace Nytherion.Editor
{
    [InitializeOnLoad]
    public static class RelicDismantleSetup
    {
        private const string Output = "output/relic-dismantle";
        private const string RewardsPath = "Assets/Nytherion/Data/ScriptableObjects/Relics/RelicDismantleRewards.asset";
        private const string ManagerPath = "Assets/Prefabs/Infrastructure/Managers/RelicManager.prefab";
        private static readonly string[] ScenePaths = { "Assets/Scenes/GameScene.unity", "Assets/Scenes/test2.unity" };
        private static readonly List<string> results = new List<string>();

        static RelicDismantleSetup() => EditorApplication.update += Update;

        private static void Update()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
                EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(Output + "/setup.request")) return;
            File.Delete(Output + "/setup.request");
            try { Apply(); }
            catch (Exception error)
            {
                File.WriteAllText(Output + "/verification.txt", "FAIL " + error);
                Debug.LogException(error);
            }
        }

        [MenuItem("Tools/Nytherion/유물/분해 UI 설정 및 검증")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("플레이 종료 후 실행해 주세요.");
            Directory.CreateDirectory(Output);
            results.Clear();
            RelicDismantleRewards rewards = AssetDatabase.LoadAssetAtPath<RelicDismantleRewards>(RewardsPath);
            if (rewards == null)
            {
                rewards = ScriptableObject.CreateInstance<RelicDismantleRewards>();
                AssetDatabase.CreateAsset(rewards, RewardsPath);
            }
            GameObject managerPrefab = PrefabUtility.LoadPrefabContents(ManagerPath);
            try
            {
                Set(managerPrefab.GetComponent<RelicManager>(), "dismantleRewards", rewards);
                PrefabUtility.SaveAsPrefabAsset(managerPrefab, ManagerPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(managerPrefab); }

            Scene source = SceneManager.GetSceneByPath(ScenePaths[0]);
            bool openedSource = !source.IsValid() || !source.isLoaded;
            if (openedSource) source = EditorSceneManager.OpenScene(ScenePaths[0], OpenSceneMode.Additive);
            try
            {
                GameSceneUIRefs templateRefs = source.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<GameSceneUIRefs>(true)).First();
                Require(templateRefs.DismantlePanel != null && templateRefs.InventoryDismantleButton != null, "기존 장비 분해 UI 참조");
                foreach (string path in ScenePaths)
                {
                    Scene scene = SceneManager.GetSceneByPath(path);
                    bool opened = !scene.IsValid() || !scene.isLoaded;
                    if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                    try
                    {
                        string backup = Output + "/" + scene.name + "-before.unity";
                        if (!File.Exists(backup)) EditorSceneManager.SaveScene(scene, backup, true);
                        foreach (RelicGridUI ui in scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<RelicGridUI>(true)))
                        {
                            Build(ui, templateRefs);
                            VerifyUI(ui, rewards);
                        }
                        EditorSceneManager.MarkSceneDirty(scene);
                        Require(EditorSceneManager.SaveScene(scene), path + " 저장");
                        results.Add("PASS " + path + " 유물 분해 패널·버튼 Inspector 참조 및 격리 UI 검증");
                    }
                    finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
                }
                VerifyLogic(rewards);
            }
            finally { if (openedSource) EditorSceneManager.CloseScene(source, true); }
            File.WriteAllLines(Output + "/verification.txt", results);
        }

        private static void Build(RelicGridUI ui, GameSceneUIRefs templateRefs)
        {
            if (ui.DismantlePanel != null) return;
            RectTransform board = (RectTransform)ui.placedBlocksContainer.parent;
            GameObject panelObject = Object.Instantiate(templateRefs.DismantlePanel.gameObject, board.parent);
            panelObject.name = "RelicDismantlePanel";
            Undo.RegisterCreatedObjectUndo(panelObject, "유물 분해 패널 추가");
            panelObject.SetActive(false);
            RectTransform rect = (RectTransform)panelObject.transform;
            rect.anchorMin = board.anchorMin;
            rect.anchorMax = board.anchorMax;
            rect.pivot = board.pivot;
            rect.anchoredPosition = board.anchoredPosition;
            rect.sizeDelta = board.sizeDelta;
            rect.localScale = board.localScale;
            InventoryDismantlePanel original = panelObject.GetComponent<InventoryDismantlePanel>();
            var serialized = new SerializedObject(original);
            RelicDismantlePanel panel = panelObject.AddComponent<RelicDismantlePanel>();
            foreach (string field in new[] { "slotParent", "dismantleButton", "resetButton", "maxRarityDropdown",
                "bulkStageButton", "expectedGoldText", "emptyHint" })
                Set(panel, field, serialized.FindProperty(field).objectReferenceValue);
            GameObject slotTemplate = ((Component)serialized.FindProperty("slotTemplate").objectReferenceValue).gameObject;
            Object.DestroyImmediate(original);
            Object.DestroyImmediate(slotTemplate);
            Set(panel, "replacedPanels", new[] { ui.gridRoot.gameObject, ui.placedBlocksContainer.gameObject, ui.previewContainer.gameObject });
            panel.transform.Find("Header").GetComponent<TMP_Text>().text = "유물 분해";
            panel.transform.Find("Scroll/Viewport/EmptyHint").GetComponent<TMP_Text>().text =
                "보관함 유물을 드래그하거나 우클릭하세요.\n대기 유물은 우클릭으로 되돌릴 수 있습니다.";
            GridLayoutGroup layout = panel.GetComponentInChildren<GridLayoutGroup>(true);
            layout.cellSize = Vector2.one * 104f;
            layout.spacing = Vector2.one * 8f;
            layout.constraintCount = Mathf.Max(1, Mathf.FloorToInt((board.rect.width - 68f) / 112f));

            Transform pages = ui.blockStorageParent.parent.Find("RelicStoragePages");
            Button sort = pages.Find("Sort").GetComponent<Button>();
            Button toggle = Object.Instantiate(templateRefs.InventoryDismantleButton, pages);
            toggle.name = "Dismantle";
            toggle.onClick = new Button.ButtonClickedEvent();
            RectTransform toggleRect = (RectTransform)toggle.transform;
            RectTransform sortRect = (RectTransform)sort.transform;
            toggleRect.anchorMin = sortRect.anchorMin;
            toggleRect.anchorMax = sortRect.anchorMax;
            toggleRect.pivot = sortRect.pivot;
            toggleRect.anchoredPosition = sortRect.anchoredPosition - new Vector2(sortRect.rect.width + 12f, 0f);
            toggle.gameObject.SetActive(true);
            Set(ui, "storageDismantleButton", toggle);
            Set(ui, "dismantlePanel", panel);
        }

        private static RelicManager CreateManager(RelicDismantleRewards rewards)
        {
            var manager = new GameObject("검증 전용 유물 매니저").AddComponent<RelicManager>();
            typeof(RelicManager).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(manager, null);
            Set(manager, "dismantleRewards", rewards);
            Set(manager, "relicDatabaseSO", AssetDatabase.LoadAssetAtPath<RelicDatabaseSO>(
                "Assets/Nytherion/Data/ScriptableObjects/Relics/RelicDatabase.asset"));
            manager.Initialize();
            manager.AddAllRelicsToStorage();
            return manager;
        }

        private static void VerifyLogic(RelicDismantleRewards rewards)
        {
            RelicManager manager = CreateManager(rewards);
            var currency = new GameObject("검증 전용 통화").AddComponent<CurrencyDataManager>();
            currency.Initialize();
            try
            {
                List<RelicBlock> original = manager.GetStorageBlocks().ToList();
                RelicBlock equipped = original[0];
                manager.StartDraggingFromStorage(equipped);
                manager.EndDrag(Vector2Int.zero);
                Require(!manager.StageForDismantle(equipped), "장착 유물 분해 차단");
                RelicBlock first = manager.GetStorageBlocks().First();
                manager.StartDraggingFromStorage(first);
                Require(!manager.StageForDismantle(first), "드래그 중 분해 차단");
                manager.EndDrag(null);
                Require(manager.StageForDismantle(first) && !manager.StageForDismantle(first), "개별 등록 및 중복 차단");
                manager.StartDraggingFromStorage(first);
                Require(!manager.IsBeingDragged(first), "분해 대기 유물 장착 차단");
                var before = new SaveData();
                manager.PopulateSaveData(before);
                Require(before.relicData.placedBlocks.Count == original.Count, "대기 상태 저장 시 소지 유물 보존");
                int count = manager.GetStorageBlocks().Count();
                manager.AddNewRelicToStorage(first.SourceData);
                Require(manager.GetStorageBlocks().Count() == count, "대기 유물 중복 획득 차단");
                manager.ResetDismantle();
                Require(manager.GetDismantleBlocks().Count == 0 && manager.GetStorageBlocks().SequenceEqual(original.Skip(1)), "취소 시 원래 보관 순서 유지");
                foreach (Rarity rarity in Enum.GetValues(typeof(Rarity)))
                {
                    manager.ResetDismantle();
                    int expected = manager.GetStorageBlocks().Count(block => block.SourceData.rarity <= rarity);
                    Require(manager.StageForDismantleUpToRarity(rarity) == expected &&
                        manager.StageForDismantleUpToRarity(rarity) == 0, rarity + " 이하 일괄 등록 및 중복 차단");
                }
                long reward = manager.GetDismantleGold();
                currency.SetCurrency(CurrencyType.Gold, int.MaxValue);
                Require(!manager.DismantleRelics(currency) && manager.GetDismantleGold() == reward, "골드 상한 초과 시 목록 보존");
                currency.SetCurrency(CurrencyType.Gold, 0);
                Require(manager.DismantleRelics(currency) && currency.GetGold() == reward &&
                    !manager.GetStorageBlocks().Any() && manager.GetBlockAt(0, 0) == equipped, "예상 골드 지급·보관 유물 제거·장착 유물 보존");
                Require(!manager.DismantleRelics(currency), "반복 분해 보상 차단");
                var after = new SaveData();
                manager.PopulateSaveData(after);
                currency.PopulateSaveData(after);
                SaveData roundtrip = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(after));
                manager.LoadFromSaveData(roundtrip);
                currency.LoadFromSaveData(roundtrip);
                Require(manager.EquippedRelicCount == 1 && !manager.GetStorageBlocks().Any() && currency.GetGold() == reward,
                    "JSON 저장·재로드 시 제거 상태와 골드 복원");
                manager.LoadFromSaveData(before);
                Require(manager.GetStorageBlocks().Count() == original.Count - 1 && manager.GetDismantleBlocks().Count == 0,
                    "기존 형식 세이브 및 대기 유물 복원");
                results.Add("PASS 데이터: 장착·드래그 보호, 개별/등급 일괄 등록, 중복 획득 방지, 취소 순서, 골드 상한, 1회 지급, 기존 JSON 호환");
            }
            finally { DestroyManager(manager); Object.DestroyImmediate(currency.gameObject); }
        }

        private static void VerifyUI(RelicGridUI source, RelicDismantleRewards rewards)
        {
            Scene preview = EditorSceneManager.NewPreviewScene();
            RelicManager manager = CreateManager(rewards);
            var currency = new GameObject("검증 통화").AddComponent<CurrencyDataManager>();
            currency.Initialize();
            GameObject clone = Object.Instantiate(source.rootCanvas.gameObject);
            clone.SetActive(false);
            SceneManager.MoveGameObjectToScene(clone, preview);
            var dependencies = new GameObject("검증 의존성");
            try
            {
                RelicGridUI ui = clone.GetComponentInChildren<RelicGridUI>(true);
                RelicDismantlePanel panel = ui.DismantlePanel;
                Require(panel != null && Get<Button>(ui, "storageDismantleButton") != null, "분해 패널 및 토글 Inspector 연결");
                var builder = new ContainerBuilder();
                builder.RegisterInstance(manager);
                builder.RegisterInstance(currency);
                builder.RegisterInstance(ui);
                builder.RegisterInstance(dependencies.AddComponent<InputManager>());
                builder.RegisterInstance(dependencies.AddComponent<AudioManager>());
                RelicTooltip tooltip = clone.GetComponentInChildren<RelicTooltip>(true);
                if (tooltip == null)
                {
                    GameObject tooltipObject = new GameObject("검증 툴팁");
                    tooltipObject.transform.SetParent(dependencies.transform);
                    tooltipObject.SetActive(false);
                    tooltip = tooltipObject.AddComponent<RelicTooltip>();
                    Set(tooltip, "tooltipPanel", tooltipObject);
                }
                builder.RegisterInstance(tooltip);
                using (IObjectResolver resolver = builder.Build())
                {
                    resolver.Inject(ui);
                    var initialization = ui.Initialize();
                    while (initialization.MoveNext()) { }
                    initialization = ui.Initialize();
                    while (initialization.MoveNext()) { }
                    Button toggle = Get<Button>(ui, "storageDismantleButton");
                    toggle.onClick.Invoke();
                    Require(panel.IsOpen, "분해 버튼으로 열기");
                    RelicBlock first = manager.GetStorageBlocks().First();
                    Require(panel.TryStage(first), "UI 개별 등록");
                    Transform slots = Get<Transform>(panel, "slotParent");
                    RelicBlockDraggable staged = slots.GetComponentInChildren<RelicBlockDraggable>(true);
                    Require(staged != null && staged.isDismantleStaged && staged.blockData == first, "대기 슬롯 유물 인스턴스 연결");
                    staged.OnPointerClick(new PointerEventData(null) { button = PointerEventData.InputButton.Right });
                    Require(manager.GetDismantleBlocks().Count == 0, "대기 슬롯 우클릭 복귀");
                    panel.TryStage(first);
                    int gold = currency.GetGold();
                    long reward = manager.GetDismantleGold();
                    Get<Button>(panel, "dismantleButton").onClick.Invoke();
                    Require(currency.GetGold() == gold + reward && manager.GetBlockByID(first.BlockId) == null, "실제 분해 버튼 지급");
                    panel.StageByRarity();
                    toggle.onClick.Invoke();
                    Require(!panel.IsOpen && manager.GetDismantleBlocks().Count == 0 && ui.gridRoot.gameObject.activeSelf,
                        "닫기 시 대기 취소 및 보드 복원");
                }
            }
            finally
            {
                Object.DestroyImmediate(dependencies);
                EditorSceneManager.ClosePreviewScene(preview);
                DestroyManager(manager);
                Object.DestroyImmediate(currency.gameObject);
            }
        }

        private static void DestroyManager(RelicManager manager)
        {
            var data = manager.GetStorageBlocks().Select(block => block.SourceData)
                .Concat(manager.GetEquippedRelics()).Distinct().ToList();
            Object.DestroyImmediate(manager.gameObject);
            foreach (RelicData relic in data) Object.DestroyImmediate(relic);
        }

        private static T Get<T>(object target, string field) => (T)target.GetType()
            .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);

        private static void Set(object target, string field, object value)
        {
            target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
            if (target is Object obj) EditorUtility.SetDirty(obj);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
