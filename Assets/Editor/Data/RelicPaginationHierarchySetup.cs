using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Nytherion.UI.RelicBoard;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Nytherion.Editor
{
    /// <summary>보관함 페이지 UI를 씬에 저장하고 Inspector 참조를 연결한다.</summary>
    [InitializeOnLoad]
    public static class RelicPaginationHierarchySetup
    {
        private const string Output = "output/relic-pagination-hierarchy";
        private static readonly string[] ScenePaths =
        {
            "Assets/Scenes/GameScene.unity", "Assets/Scenes/test2.unity"
        };

        static RelicPaginationHierarchySetup() => EditorApplication.update += Update;

        private static void Update()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
                EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(Output + "/setup.request")) return;
            File.Delete(Output + "/setup.request");
            try { Apply(); }
            catch (Exception error)
            {
                File.WriteAllText(Output + "/setup.txt", "FAIL " + error);
                Debug.LogException(error);
            }
        }

        [MenuItem("Tools/Nytherion/유물/보관함 페이지 하이라키 생성·연결")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("플레이를 종료한 뒤 실행해 주세요.");

            Directory.CreateDirectory(Output);
            var report = new List<string>();
            foreach (string path in ScenePaths)
            {
                Scene scene = SceneManager.GetSceneByPath(path);
                bool opened = !scene.IsValid() || !scene.isLoaded;
                if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                try
                {
                    string backup = Output + "/" + scene.name + "-before.unity";
                    if (!File.Exists(backup)) EditorSceneManager.SaveScene(scene, backup, true);
                    foreach (RelicGridUI ui in scene.GetRootGameObjects()
                        .SelectMany(root => root.GetComponentsInChildren<RelicGridUI>(true)))
                    {
                        CreateAndConnect(ui, scene);
                        Verify(ui);
                        report.Add("PASS " + path + ": " + HierarchyPath(ui.blockStorageParent.parent) +
                            "/RelicStoragePages (Previous/Label, Next/Label, Page, Sort)");
                    }
                    EditorSceneManager.MarkSceneDirty(scene);
                    if (!EditorSceneManager.SaveScene(scene))
                        throw new InvalidOperationException("씬 저장 실패: " + path);
                }
                finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
            }
            File.WriteAllLines(Output + "/setup.txt", report);
            Debug.Log("유물 보관함 페이지 하이라키 생성·연결 및 페이지 전환 검증 완료");
        }

        private static void CreateAndConnect(RelicGridUI ui, Scene scene)
        {
            var serialized = new SerializedObject(ui);
            RectTransform storage = ui.blockStorageParent;
            RectTransform root = storage.parent.Find("RelicStoragePages") as RectTransform;
            if (root == null)
            {
                var go = new GameObject("RelicStoragePages", typeof(RectTransform));
                Undo.RegisterCreatedObjectUndo(go, "유물 페이지 UI 생성");
                root = go.GetComponent<RectTransform>();
                root.SetParent(storage.parent, false);
                GridLayoutGroup layout = storage.GetComponent<GridLayoutGroup>();
                float width = RelicGridUI.StorageSlotSize * RelicGridUI.StorageColumns +
                    5f * (RelicGridUI.StorageColumns - 1) + (layout != null ? layout.padding.horizontal : 0);
                float height = RelicGridUI.StorageSlotSize * RelicGridUI.StorageColumns +
                    5f * (RelicGridUI.StorageColumns - 1) + (layout != null ? layout.padding.vertical : 0);
                bool stretched = storage.anchorMin != storage.anchorMax;
                root.anchorMin = root.anchorMax = stretched ? new Vector2(0.5f, 1f) : storage.anchorMin;
                root.sizeDelta = new Vector2(width, 36f);
                root.anchoredPosition = (stretched ? Vector2.zero : storage.anchoredPosition) +
                    new Vector2(0f, -height - 52f);
            }

            Button previous = FindOrCreateButton(root, "Previous", "<", -80f, 48f);
            Button next = FindOrCreateButton(root, "Next", ">", 80f, 48f);
            TextMeshProUGUI page = root.Find("Page")?.GetComponent<TextMeshProUGUI>();
            if (page == null) page = CreateLabel(root, "Page", "1/1");
            Button sort = root.Find("Sort")?.GetComponent<Button>();
            if (sort == null)
            {
                Button template = scene.GetRootGameObjects()
                    .SelectMany(go => go.GetComponentsInChildren<GameSceneUIRefs>(true))
                    .Select(refs => refs.InventorySortButton).FirstOrDefault(button => button != null);
                if (template != null)
                {
                    sort = Object.Instantiate(template, root);
                    Undo.RegisterCreatedObjectUndo(sort.gameObject, "유물 정렬 버튼 생성");
                    sort.name = "Sort";
                    sort.onClick = new Button.ButtonClickedEvent();
                    RectTransform rect = sort.GetComponent<RectTransform>();
                    rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * 0.5f;
                    rect.localScale = Vector3.one;
                    rect.anchoredPosition = new Vector2(root.rect.width * 0.5f - rect.rect.width * 0.5f, 0f);
                    sort.gameObject.SetActive(true);
                }
                else sort = FindOrCreateButton(root, "Sort", "정렬", root.rect.width * 0.5f - 48f, 96f);
            }

            serialized.FindProperty("paginationRoot").objectReferenceValue = root;
            serialized.FindProperty("previousPageButton").objectReferenceValue = previous;
            serialized.FindProperty("nextPageButton").objectReferenceValue = next;
            serialized.FindProperty("storageSortButton").objectReferenceValue = sort;
            serialized.FindProperty("pageText").objectReferenceValue = page;
            serialized.ApplyModifiedProperties();
        }

        private static Button FindOrCreateButton(RectTransform root, string name, string label, float x, float width)
        {
            Button existing = root.Find(name)?.GetComponent<Button>();
            if (existing != null) return existing;
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            Undo.RegisterCreatedObjectUndo(go, "유물 페이지 버튼 생성");
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(root, false);
            rect.sizeDelta = new Vector2(width, 36f);
            rect.anchoredPosition = new Vector2(x, 0f);
            go.GetComponent<Image>().color = new Color(0.18f, 0.18f, 0.18f, 1f);
            Button button = go.GetComponent<Button>();
            button.targetGraphic = go.GetComponent<Image>();
            CreateLabel(rect, "Label", label);
            return button;
        }

        private static TextMeshProUGUI CreateLabel(Transform parent, string name, string value)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            Undo.RegisterCreatedObjectUndo(go, "유물 페이지 텍스트 생성");
            go.transform.SetParent(parent, false);
            TextMeshProUGUI label = go.GetComponent<TextMeshProUGUI>();
            label.rectTransform.sizeDelta = new Vector2(64f, 36f);
            label.font = TMP_Settings.defaultFontAsset;
            label.fontSize = 22f;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white;
            label.raycastTarget = false;
            label.text = value;
            return label;
        }

        private static void Verify(RelicGridUI source)
        {
            // 씬 원본은 그대로 두고 비활성 복제본에서 연결·표시·클릭 동작을 검사한다.
            GameObject clone = Object.Instantiate(source.rootCanvas.gameObject);
            clone.SetActive(false);
            try
            {
                RelicGridUI ui = clone.GetComponentInChildren<RelicGridUI>(true);
                var serialized = new SerializedObject(ui);
                foreach (string field in new[] { "paginationRoot", "previousPageButton", "nextPageButton", "storageSortButton", "pageText" })
                {
                    if (serialized.FindProperty(field).objectReferenceValue == null)
                        throw new InvalidOperationException("페이지 UI 참조 누락: " + field);
                }
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
                Type type = typeof(RelicGridUI);
                Action refresh = () => type.GetMethod("UpdatePagination", flags).Invoke(ui, null);
                Button previous = (Button)serialized.FindProperty("previousPageButton").objectReferenceValue;
                Button next = (Button)serialized.FindProperty("nextPageButton").objectReferenceValue;
                var page = (TextMeshProUGUI)serialized.FindProperty("pageText").objectReferenceValue;
                Transform root = ((RectTransform)serialized.FindProperty("paginationRoot").objectReferenceValue);
                int childCount = root.childCount;
                type.GetMethod("InitializePagination", flags).Invoke(ui, null);
                type.GetMethod("InitializePagination", flags).Invoke(ui, null);
                refresh();
                if (previous.gameObject.activeSelf || next.gameObject.activeSelf || page.gameObject.activeSelf)
                    throw new InvalidOperationException("한 페이지에서 페이지 UI가 숨겨지지 않습니다.");
                type.GetField("storagePageCount", flags).SetValue(ui, 3);
                refresh();
                if (page.text != "1/3" || previous.interactable || !next.interactable)
                    throw new InvalidOperationException("첫 페이지 표시 오류");
                next.onClick.Invoke();
                refresh();
                if (page.text != "2/3") throw new InvalidOperationException("다음 페이지 또는 중복 리스너 오류");
                next.onClick.Invoke();
                next.onClick.Invoke();
                refresh();
                if (page.text != "3/3" || next.interactable) throw new InvalidOperationException("마지막 페이지 제한 오류");
                previous.onClick.Invoke();
                refresh();
                if (page.text != "2/3" || root.childCount != childCount)
                    throw new InvalidOperationException("이전 페이지 또는 UI 중복 생성 오류");
            }
            finally { Object.DestroyImmediate(clone); }
        }

        private static string HierarchyPath(Transform target) =>
            target.parent == null ? target.name : HierarchyPath(target.parent) + "/" + target.name;
    }
}
