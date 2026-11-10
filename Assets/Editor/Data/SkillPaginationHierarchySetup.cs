using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Nytherion.Core.Data;
using Nytherion.Core.Managers;
using Nytherion.Data.ScriptableObjects.Skill;
using Nytherion.UI.Skill;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Nytherion.Editor
{
    /// <summary>스킬 보관함 페이지 버튼을 씬에 생성하고 UI 참조를 연결한다.</summary>
    [InitializeOnLoad]
    public static class SkillPaginationHierarchySetup
    {
        private const string Output = "output/skill-pagination";
        static SkillPaginationHierarchySetup() => EditorApplication.update += Update;

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

        [MenuItem("Tools/Nytherion/스킬/보관함 페이지 하이라키 생성·연결")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("플레이를 종료한 뒤 실행해 주세요.");
            Directory.CreateDirectory(Output);
            const string path = "Assets/Scenes/GameScene.unity";
            Scene scene = SceneManager.GetSceneByPath(path);
            bool opened = !scene.IsValid() || !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                if (!File.Exists(Output + "/GameScene-before.unity") &&
                    !EditorSceneManager.SaveScene(scene, Output + "/GameScene-before.unity", true))
                    throw new InvalidOperationException("변경 전 씬 사본 저장 실패");
                GameSceneUIRefs refs = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<GameSceneUIRefs>(true))
                    .Single(ui => ui.storageContent != null);
                CreateAndConnect(refs);
                Verify(refs);
                RectTransform viewport = refs.storageContent.parent as RectTransform;
                File.WriteAllText(Output + "/layout.txt", $"viewport={viewport.rect} anchors={viewport.anchorMin}/{viewport.anchorMax} " +
                    $"driven={viewport.drivenByObject} content={((RectTransform)refs.storageContent).rect}");
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("씬 저장 실패");
                File.WriteAllText(Output + "/setup.txt",
                    $"PASS GameScene 페이지·정렬 버튼 연결, {SkillUIController.StorageSlotsPerPage}칸 배치, 페이지 경계, 레벨·영문·한글 정렬, 저장 데이터 왕복");
            }
            finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
        }

        private static void CreateAndConnect(GameSceneUIRefs refs)
        {
            RectTransform content = (RectTransform)refs.storageContent;
            ScrollRect scroll = content.GetComponentInParent<ScrollRect>(true);
            if (scroll == null) throw new InvalidOperationException("스킬 보관함 ScrollRect 참조 누락");
            Transform parent = scroll.transform.parent;
            RectTransform pages = parent.Find("SkillStoragePages") as RectTransform;
            if (pages == null)
            {
                RectTransform template = refs.GetComponentsInChildren<RectTransform>(true)
                    .FirstOrDefault(rect => rect.name == "RelicStoragePages");
                if (template == null)
                    template = refs.gameObject.scene.GetRootGameObjects()
                        .SelectMany(root => root.GetComponentsInChildren<RectTransform>(true))
                        .First(rect => rect.name == "RelicStoragePages");
                pages = Object.Instantiate(template, parent, false);
                pages.name = "SkillStoragePages";
                Undo.RegisterCreatedObjectUndo(pages.gameObject, "스킬 페이지 UI 생성");
            }
            pages.anchorMin = pages.anchorMax = new Vector2(0.5f, 0f);
            pages.pivot = new Vector2(0.5f, 0.5f);
            pages.anchoredPosition = new Vector2(0f, 32f);
            pages.sizeDelta = new Vector2(((RectTransform)parent).rect.width, 36f);
            pages.gameObject.SetActive(true);
            Undo.RecordObject(refs, "스킬 보관함 페이지 참조 연결");
            refs.SkillPreviousPageButton = pages.Find("Previous").GetComponent<Button>();
            refs.SkillNextPageButton = pages.Find("Next").GetComponent<Button>();
            refs.SkillPageText = pages.Find("Page").GetComponent<TMP_Text>();
            refs.SkillPreviousPageButton.onClick = new Button.ButtonClickedEvent();
            refs.SkillNextPageButton.onClick = new Button.ButtonClickedEvent();
            refs.SkillPageText.text = "1/1";
            Button sort = pages.Find("Sort")?.GetComponent<Button>();
            if (sort == null)
            {
                Button template = refs.gameObject.scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<Button>(true))
                    .First(button => button.name == "Sort" && button.transform.parent.name == "RelicStoragePages");
                sort = Object.Instantiate(template, pages, false);
                sort.name = "Sort";
                Undo.RegisterCreatedObjectUndo(sort.gameObject, "스킬 정렬 버튼 생성");
            }
            RectTransform sortRect = (RectTransform)sort.transform;
            sortRect.anchorMin = sortRect.anchorMax = sortRect.pivot = Vector2.one * 0.5f;
            // 유물 페이지에서 복제된 불필요한 삭제 버튼을 제거하고 그 자리에 정렬 버튼을 둔다.
            RectTransform dismantleRect = pages.Find("Dismantle") as RectTransform;
            sortRect.anchoredPosition = dismantleRect != null
                ? dismantleRect.anchoredPosition
                : new Vector2(179.5f, 0f);
            if (dismantleRect != null) Undo.DestroyObjectImmediate(dismantleRect.gameObject);
            sort.onClick = new Button.ButtonClickedEvent();
            sort.gameObject.SetActive(true);
            refs.SkillStorageSortButton = sort;

            // 스크롤바 자동 숨김이 Viewport 크기를 바꾸지 않도록 페이지 영역으로 고정한다.
            Undo.RecordObject(scroll, "스킬 보관함 스크롤 해제");
            if (scroll.horizontalScrollbar != null) scroll.horizontalScrollbar.gameObject.SetActive(false);
            if (scroll.verticalScrollbar != null) scroll.verticalScrollbar.gameObject.SetActive(false);
            scroll.horizontalScrollbar = null;
            scroll.verticalScrollbar = null;
            scroll.horizontal = scroll.vertical = false;
            scroll.enabled = false;
            RectTransform viewport = scroll.viewport;
            Undo.RecordObject(viewport, "스킬 보관함 표시 영역 설정");
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.offsetMin = new Vector2(0f, 64f);
            viewport.offsetMax = Vector2.zero;
            Undo.RecordObject(content, "스킬 보관함 페이지 크기 설정");
            content.anchoredPosition = Vector2.zero;
            GridLayoutGroup layout = content.GetComponent<GridLayoutGroup>();
            Undo.RecordObject(layout, "스킬 보관함 페이지 배치 설정");
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = SkillUIController.StorageColumns;
            layout.cellSize = Vector2.one * SkillUIController.StorageSlotSize;
            content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,
                layout.padding.vertical + SkillUIController.StorageRows * SkillUIController.StorageSlotSize +
                (SkillUIController.StorageRows - 1) * layout.spacing.y);
            EditorUtility.SetDirty(refs);
        }

        private static void Verify(GameSceneUIRefs refs)
        {
            GameObject root = new GameObject("SkillPaginationVerification");
            root.SetActive(false);
            try
            {
                SkillUIController ui = Object.Instantiate(AssetDatabase.LoadAssetAtPath<SkillUIController>(
                    "Assets/Prefabs/Infrastructure/Managers/SkillUIController.prefab"), root.transform);
                SkillDataManager manager = root.AddComponent<SkillDataManager>();
                manager.equippedSkills = new SkillData[0];
                var content = new GameObject("Content", typeof(RectTransform), typeof(GridLayoutGroup));
                content.transform.SetParent(root.transform, false);
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
                Type type = typeof(SkillUIController);
                Action<string, object> set = (field, value) => type.GetField(field, flags).SetValue(ui, value);
                Action refresh = () => ui.SyncUIFromData();
                set("storageContent", content.transform);
                set("skillDataManager", manager);
                set("equipSlots", new SkillSlotUI[0]);
                Button previous = Object.Instantiate(refs.SkillPreviousPageButton, root.transform);
                Button next = Object.Instantiate(refs.SkillNextPageButton, root.transform);
                TMP_Text label = Object.Instantiate(refs.SkillPageText, root.transform);
                set("previousPageButton", previous);
                set("nextPageButton", next);
                set("pageText", label);
                Button sort = Object.Instantiate(refs.SkillStorageSortButton, root.transform);
                set("storageSortButton", sort);
                ui.Initialize();
                ui.Initialize();
                // 실제 사용 가능한 데이터로 숨겨진 슬롯의 보존까지 검사한다.
                SkillData skill = AssetDatabase.FindAssets("t:SkillData", new[] { "Assets/Nytherion/Data/ScriptableObjects/Skill" })
                    .Select(guid => AssetDatabase.LoadAssetAtPath<SkillData>(AssetDatabase.GUIDToAssetPath(guid)))
                    .First(data => data != null && data.IsRuntimeAvailable);
                int pageSize = SkillUIController.StorageSlotsPerPage;
                int overflowCount = pageSize * 2 + 1;
                foreach (int count in new[] { 0, pageSize, pageSize + 1, overflowCount })
                {
                    manager.storageSkills = Enumerable.Repeat(skill, count).ToArray();
                    refresh();
                    int pages = Mathf.Max(1, Mathf.CeilToInt(count / (float)pageSize));
                    ui.ChangeStoragePage(-100);
                    if (label.text != $"1/{pages}" || previous.interactable || next.interactable != (pages > 1))
                        throw new InvalidOperationException("첫 페이지 표시 오류: " + count);
                    if (pages > 1)
                    {
                        next.onClick.Invoke();
                        if (label.text != $"2/{pages}")
                            throw new InvalidOperationException("다음 페이지 버튼 연결 또는 중복 구독 오류");
                        previous.onClick.Invoke();
                        if (label.text != $"1/{pages}")
                            throw new InvalidOperationException("이전 페이지 버튼 연결 오류");
                    }
                    ui.ChangeStoragePage(100);
                    SkillSlotUI[] slots = (SkillSlotUI[])type.GetField("storageSlots", flags).GetValue(ui);
                    if (label.text != $"{pages}/{pages}" || next.interactable ||
                        slots.Count(slot => slot.gameObject.activeSelf) != pageSize ||
                        slots.Count(slot => slot.GetSkill() != null) != count)
                        throw new InvalidOperationException("마지막 페이지 또는 숨겨진 스킬 보존 오류: " + count);
                }
                type.GetMethod("UpdatePlayerSkills", flags).Invoke(ui, null);
                if (manager.storageSkills.Count(data => data != null) != overflowCount)
                    throw new InvalidOperationException("페이지 밖 스킬 저장 동기화 오류");
                manager.storageSkills = new SkillData[pageSize];
                refresh();
                if (label.text != "1/1" || previous.gameObject.activeSelf || next.gameObject.activeSelf || label.gameObject.activeSelf)
                    throw new InvalidOperationException("페이지 감소 후 표시 오류");
                VerifySorting(root, ui, manager, sort, label);
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static void VerifySorting(GameObject root, SkillUIController ui, SkillDataManager manager,
            Button sort, TMP_Text label)
        {
            var database = ScriptableObject.CreateInstance<SkillDatabaseSO>();
            database.allSkills = new System.Collections.Generic.List<SkillData>();
            try
            {
                manager.skillDatabase = database;
                foreach (string[] names in new[] { new[] { "Zulu", "Beta", "alpha" }, new[] { "하늘", "나무", "가방" } })
                {
                    SkillData[] skills = names.Select((name, index) =>
                    {
                        SkillData data = ScriptableObject.CreateInstance<SkillData>();
                        data.skillID = "pagination-sort-test-" + database.allSkills.Count;
                        data.skillName = name;
                        database.allSkills.Add(data);
                        return data;
                    }).ToArray();
                    manager.skillStates.Clear();
                    for (int i = 0; i < skills.Length; i++)
                        manager.skillStates[skills[i].skillID] = new SkillState { level = i == 0 ? 5 : 2, exp = i };
                    manager.equippedSkills = new[] { skills[1] };
                    manager.storageSkills = new SkillData[SkillUIController.StorageSlotsPerPage + 3];
                    manager.storageSkills[0] = skills[1];
                    manager.storageSkills[SkillUIController.StorageSlotsPerPage] = skills[2];
                    manager.storageSkills[SkillUIController.StorageSlotsPerPage + 2] = skills[0];
                    ui.SyncUIFromData();
                    ui.ChangeStoragePage(1);
                    sort.onClick.Invoke();
                    SkillData[] expected = { skills[0], skills[2], skills[1] };
                    if (!manager.storageSkills.Take(3).SequenceEqual(expected) ||
                        manager.storageSkills.Skip(3).Any(data => data != null) || label.text != "1/1" ||
                        manager.equippedSkills[0] != skills[1] || !sort.gameObject.activeSelf)
                        throw new InvalidOperationException("레벨·이름 정렬, 페이지 초기화 또는 장착 유지 오류");

                    // 모든 레벨이 같을 때 이름순으로만 정렬되는지 검사한다.
                    foreach (SkillData skill in skills) manager.skillStates[skill.skillID].level = 3;
                    sort.onClick.Invoke();
                    expected = new[] { skills[2], skills[1], skills[0] };
                    if (!manager.storageSkills.Take(3).SequenceEqual(expected))
                        throw new InvalidOperationException("동일 레벨 이름순 정렬 오류");

                    SaveData save = new SaveData();
                    manager.PopulateSaveData(save);
                    manager.LoadFromSaveData(JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(save)));
                    if (!manager.storageSkills.Take(3).SequenceEqual(expected) || manager.equippedSkills[0] != skills[1] ||
                        manager.skillStates[skills[2].skillID].level != 3 || manager.skillStates[skills[2].skillID].exp != 2)
                        throw new InvalidOperationException("정렬 결과·장착·레벨·경험치 저장 복원 오류");
                }
            }
            finally
            {
                foreach (SkillData data in database.allSkills) Object.DestroyImmediate(data);
                Object.DestroyImmediate(database);
            }
        }
    }
}
