using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Nytherion.Core.Data;
using Nytherion.Core.Enums;
using Nytherion.Core.Managers;
using Nytherion.Data.ScriptableObjects.Relics;
using Nytherion.GamePlay.Relics;
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
    /// <summary>GameScene 유물 캔버스 복제본으로 정렬·페이지·드래그·저장 순서를 검증한다.</summary>
    [InitializeOnLoad]
    public static class RelicStorageSortVerification
    {
        private const string Output = "output/relic-storage-sort";
        private const string Pending = "Nytherion.Relics.StorageSortVerification";
        private static readonly List<string> results = new List<string>();
        private static IEnumerator verification;
        private static IObjectResolver resolver;
        private static double startedAt;

        static RelicStorageSortVerification()
        {
            EditorApplication.update += Update;
        }

        // -batchmode -executeMethod Nytherion.Editor.RelicStorageSortVerification.RunBatch
        public static void RunBatch()
        {
            Directory.CreateDirectory(Output);
            Scene target = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Scene source = EditorSceneManager.OpenScene("Assets/Scenes/GameScene.unity", OpenSceneMode.Additive);
            try
            {
                RelicGridUI original = source.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<RelicGridUI>(true)).Single();
                GameObject canvas = Object.Instantiate(original.rootCanvas.gameObject);
                SceneManager.MoveGameObjectToScene(canvas, target);
                canvas.name = "유물 보관함 정렬 검증";
                GameSceneUIRefs refs = source.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<GameSceneUIRefs>(true)).Single();
                Button template = Object.Instantiate(refs.InventorySortButton, canvas.transform);
                template.name = "검증 정렬 버튼 원본";
                template.gameObject.SetActive(false);
                canvas.GetComponentInChildren<RelicGridUI>(true).SetStorageSortButtonTemplate(template);
                CanvasGroup group = canvas.GetComponent<CanvasGroup>();
                group.alpha = 1f;
                group.interactable = group.blocksRaycasts = true;
                CanvasScaler scaler = canvas.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
            }
            finally { EditorSceneManager.CloseScene(source, true); }
            SessionState.SetBool(Pending, true);
            EditorApplication.isPlaying = true;
        }

        private static void Update()
        {
            if (!SessionState.GetBool(Pending, false) || !EditorApplication.isPlaying ||
                EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            try
            {
                if (verification == null)
                {
                    results.Clear();
                    startedAt = EditorApplication.timeSinceStartup;
                    verification = Verify();
                }
                if (EditorApplication.timeSinceStartup - startedAt > 90d)
                    throw new TimeoutException("유물 보관함 정렬 검증 시간 초과");
                if (!verification.MoveNext()) Finish(0);
            }
            catch (Exception error)
            {
                results.Add("FAIL " + error);
                Debug.LogException(error);
                Finish(1);
            }
        }

        private static IEnumerator Verify()
        {
            RelicGridUI ui = Object.FindObjectOfType<RelicGridUI>(true);
            Require(ui != null && ui.gridRoot != null && ui.blockStorageParent != null &&
                ui.storageSlotPrefab != null && ui.rootCanvas != null, "GameScene Inspector의 보드·보관함·프리팹 참조");
            var manager = new GameObject("검증 전용 유물 매니저").AddComponent<RelicManager>();
            Set(manager, "relicDatabaseSO", AssetDatabase.LoadAssetAtPath<RelicDatabaseSO>(
                "Assets/Nytherion/Data/ScriptableObjects/Relics/RelicDatabase.asset"));
            manager.Initialize();
            Require(manager.SortStorageBlocks(), "빈 보관함 정렬");

            RelicTooltip tooltip = Object.FindObjectOfType<RelicTooltip>(true);
            if (tooltip == null)
            {
                var tooltipObject = new GameObject("검증 툴팁");
                tooltipObject.SetActive(false);
                tooltip = tooltipObject.AddComponent<RelicTooltip>();
                Set(tooltip, "tooltipPanel", new GameObject("검증 툴팁 패널", typeof(RectTransform)));
            }
            var builder = new ContainerBuilder();
            builder.RegisterInstance(manager);
            builder.RegisterInstance(ui);
            builder.RegisterInstance(InputManager.Instance);
            builder.RegisterInstance(RootLifetimeScope.Instance.Container.Resolve<AudioManager>());
            builder.RegisterInstance(tooltip);
            resolver = builder.Build();
            ui.Construct(manager, resolver);
            ui.StartCoroutine(ui.Initialize());
            yield return null;
            yield return null;
            Button sort = ui.blockStorageParent.parent.Find("RelicStoragePages/Sort").GetComponent<Button>();
            Require(sort.gameObject.activeInHierarchy && !sort.interactable, "빈 보관함에도 정렬 버튼 표시·비활성화");

            manager.AddAllRelicsToStorage();
            List<RelicBlock> original = manager.GetStorageBlocks().ToList();
            Require(original.Count > RelicGridUI.StorageSlotsPerPage, "여러 페이지 분량의 실제 유물 데이터");
            RelicBlock equipped = original[0];
            manager.StartDraggingFromStorage(equipped);
            manager.EndDrag(new Vector2Int(0, 0));
            foreach (RelicBlock block in manager.GetStorageBlocks()) block.SetRotationState(2);
            var stateBefore = manager.GetStorageBlocks().ToDictionary(block => block.BlockId,
                block => (block.SourceData, block.Level, block.RotationState));
            ui.ChangeStoragePage(1);
            yield return null;
            yield return null;
            sort.onClick.Invoke();
            yield return null;
            yield return null;
            List<RelicBlock> sorted = manager.GetStorageBlocks().ToList();
            for (int i = 1; i < sorted.Count; i++)
            {
                RelicData previous = sorted[i - 1].SourceData;
                RelicData current = sorted[i].SourceData;
                Require(previous.rarity > current.rarity || previous.rarity == current.rarity &&
                    StringComparer.CurrentCultureIgnoreCase.Compare(previous.DisplayName, current.DisplayName) <= 0,
                    "등급 내림차순·표시 이름 오름차순 " + i);
            }
            Require(sorted.All(block => stateBefore.TryGetValue(block.BlockId, out var state) &&
                state.Item1 == block.SourceData && state.Item2 == block.Level && state.Item3 == block.RotationState),
                "정렬 후 유물 인스턴스·레벨·회전 보존");
            Require(manager.GetBlockAt(0, 0) == equipped && manager.EquippedRelicCount == 1,
                "정렬 후 장착 유물·위치 유지");
            Require(Get<int>(ui, "storagePage") == 0 &&
                ui.blockStorageParent.GetChild(0).GetComponentInChildren<RelicBlockDraggable>().blockData == sorted[0],
                "2페이지에서 정렬 클릭 후 첫 페이지 표시와 실제 슬롯 갱신");
            sort.onClick.Invoke();
            yield return null;
            yield return null;
            Require(manager.GetStorageBlocks().SequenceEqual(sorted), "정렬 연속 클릭 시 순서 유지");

            var eventSystem = new GameObject("검증 이벤트 시스템").AddComponent<EventSystem>();
            var pointer = new PointerEventData(eventSystem) { button = PointerEventData.InputButton.Left };
            RelicBlockDraggable dragged = ui.blockStorageParent.GetChild(0).GetComponentInChildren<RelicBlockDraggable>();
            dragged.OnBeginDrag(pointer);
            Require(!manager.SortStorageBlocks() && manager.GetStorageBlocks().SequenceEqual(sorted),
                "드래그 중 정렬 거부·원래 순번 보존");
            dragged.CancelDrag();
            yield return null;
            yield return null;
            ui.ChangeStoragePage(1);
            yield return null;
            yield return null;
            dragged = ui.blockStorageParent.GetChild(0).GetComponentInChildren<RelicBlockDraggable>();
            RelicBlock moved = dragged.blockData;
            dragged.OnBeginDrag(pointer);
            pointer.pointerCurrentRaycast = new RaycastResult { gameObject = ui.blockStorageParent.GetChild(2).gameObject };
            dragged.OnEndDrag(pointer);
            sorted.Remove(moved);
            sorted.Insert(18, moved);
            yield return null;
            yield return null;
            Require(manager.GetStorageBlocks().SequenceEqual(sorted), "정렬 후 2페이지 드롭도 실제 전역 순번에 삽입");

            ui.SortStorage();
            yield return null;
            yield return null;
            var save = new SaveData();
            manager.PopulateSaveData(save);
            string[] savedOrder = manager.GetStorageBlocks().Select(block => block.RelicId).ToArray();
            manager.LoadFromSaveData(JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(save)));
            Require(manager.GetStorageBlocks().Select(block => block.RelicId).SequenceEqual(savedOrder) &&
                manager.GetStorageBlocks().All(block => block.RotationState == 2) && manager.EquippedRelicCount == 1,
                "기존 SaveData JSON 왕복 후 정렬 순서·회전·장착 상태 복원");
            yield return null;
            yield return null;
            Capture(ui.rootCanvas);
            // 실제 저장 파일 대신 검증 전용 매니저의 메모리 상태만 변경한다.
            manager.LoadFromSaveData(new SaveData { relicData = new RelicGridState() });
            RelicData single = original[1].SourceData;
            manager.AddNewRelicToStorage(single);
            yield return null;
            yield return null;
            Require(sort.gameObject.activeInHierarchy && !sort.interactable && manager.SortStorageBlocks(),
                "유물 1개에서도 정렬 버튼 표시·불필요한 클릭 비활성화");
            manager.AddNewRelicToStorage(original[2].SourceData);
            yield return null;
            yield return null;
            Require(sort.isActiveAndEnabled && sort.interactable &&
                !Get<Button>(ui, "previousPageButton").gameObject.activeSelf, "한 페이지의 보관함에서 정렬 가능·페이지 이동 숨김");
        }

        private static void Capture(Canvas canvas)
        {
            var camera = new GameObject("검증 캡처 카메라").AddComponent<Camera>();
            camera.enabled = false;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.08f, 0.08f, 0.1f);
            var target = new RenderTexture(1920, 1080, 24);
            camera.targetTexture = target;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1f;
            Canvas.ForceUpdateCanvases();
            camera.Render();
            RenderTexture previous = RenderTexture.active;
            var pixels = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            RenderTexture.active = target;
            pixels.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
            pixels.Apply();
            File.WriteAllBytes(Output + "/preview.png", pixels.EncodeToPNG());
            RenderTexture.active = previous;
            camera.targetTexture = null;
            target.Release();
            Object.Destroy(target);
            Object.Destroy(pixels);
            Object.Destroy(camera.gameObject);
        }

        private static T Get<T>(object target, string name) =>
            (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
        private static void Set(object target, string name, object value) =>
            target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
            results.Add("PASS " + message);
        }
        private static void Finish(int code)
        {
            SessionState.SetBool(Pending, false);
            resolver?.Dispose();
            File.WriteAllLines(Output + "/verification.txt", results);
            EditorApplication.isPlaying = false;
            if (Application.isBatchMode) EditorApplication.Exit(code);
        }
    }
}
