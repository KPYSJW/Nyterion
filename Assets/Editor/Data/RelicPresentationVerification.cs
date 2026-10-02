using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Nytherion.Core.Data;
using Nytherion.Core.Interfaces;
using Nytherion.Core.Managers;
using Nytherion.Data.ScriptableObjects.Player;
using Nytherion.Data.ScriptableObjects.Relics;
using Nytherion.Data.ScriptableObjects.Weapons;
using Nytherion.GamePlay.Characters.Player;
using Nytherion.GamePlay.Combat;
using Nytherion.GamePlay.Relics;
using Nytherion.UI.RelicBoard;
using Nytherion.UI.Skill;
using Nytherion.UI.Components;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using VContainer;
using Object = UnityEngine.Object;

namespace Nytherion.Editor
{
    /// <summary>실제 유물 에셋의 저장·장착 경로, 풀 재사용, UI 갱신과 드래그 규격을 플레이 모드에서 검증한다.</summary>
    [InitializeOnLoad]
    public static class RelicPresentationVerification
    {
        private const string Pending = "Nytherion.Relics.PresentationVerification";
        private const string Relics = "Assets/Nytherion/Data/ScriptableObjects/Relics/";
        private static readonly List<string> results = new List<string>();
        private static readonly List<Object> temporary = new List<Object>();
        private static double readyAt;
        private static bool running;
        private static ObjectPoolManager previousPool;
        private static ObjectPoolManager pool;
        private static IObjectResolver resolver;
        private static readonly FieldInfo PoolInstance = typeof(ObjectPoolManager)
            .GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);
        private static bool previousRunInBackground;

        static RelicPresentationVerification()
        {
            EditorApplication.update += Update;
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Pending, false))
                    readyAt = EditorApplication.timeSinceStartup + 3d;
            };
        }

        private static void Update()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            string request = RelicPresentationSetup.Output + "/verify.request";
            bool presentationOnly = File.Exists(RelicPresentationSetup.Output + "/presentation-verify.request");
            if (presentationOnly) request = RelicPresentationSetup.Output + "/presentation-verify.request";
            if (File.Exists(request) && !running)
            {
                File.Delete(request);
                SessionState.SetBool(Pending + ".Exit", !EditorApplication.isPlaying);
                SessionState.SetBool(Pending + ".PresentationOnly", presentationOnly);
                SessionState.SetBool(Pending, true);
                readyAt = EditorApplication.timeSinceStartup + 3d;
                if (!EditorApplication.isPlaying) EditorApplication.isPlaying = true;
            }
            if (!SessionState.GetBool(Pending, false) || !EditorApplication.isPlaying ||
                EditorApplication.timeSinceStartup < readyAt) return;
            SessionState.SetBool(Pending, false);
            running = true;
            results.Clear();
            try
            {
                previousRunInBackground = Application.runInBackground;
                Application.runInBackground = true;
                previousPool = ObjectPoolManager.Instance;
                PoolInstance.SetValue(null, null);
                pool = Track(new GameObject("[RelicVerification] 전용 풀")).AddComponent<ObjectPoolManager>();
                pool.Initialize();
                pool.StartCoroutine(Execute(Verify(SessionState.GetBool(Pending + ".PresentationOnly", false))));
            }
            catch (Exception error) { Finish(error); }
        }

        private static IEnumerator Verify(bool presentationOnly)
        {
            var manager = Track(new GameObject("[RelicVerification] 저장 매니저")).AddComponent<RelicManager>();
            Set(manager, "relicDatabaseSO", AssetDatabase.LoadAssetAtPath<RelicDatabaseSO>(Relics + "RelicDatabase.asset"));
            manager.Initialize();
            Require(!manager.GetStorageBlocks().Any(), "새 저장 매니저에서 임의의 테스트 유물 생성 안 함");
            if (!presentationOnly)
            {
            var playerRoot = Track(new GameObject("[RelicVerification] 플레이어"));
            var player = playerRoot.AddComponent<PlayerManager>();
            var playerRelics = playerRoot.AddComponent<PlayerRelicManager>();
            var effects = playerRoot.AddComponent<RelicEffectController>();
            var events = playerRoot.AddComponent<EventManager>();
            PlayerData baseData = Track(ScriptableObject.CreateInstance<PlayerData>());
            baseData.rangedDamage = 20f;
            baseData.rangedSpeed = 1f;
            Set(player, "basePlayerData", baseData);
            player.Construct(null, null, events, manager);
            player.Initialize();
            temporary.Add(player.currentPlayerData);
            Load(manager, ("SimpleStats/PouchOfAbundance.asset", 0, 0), ("CombatUtility/Globe.asset", 4, 4));
            yield return null;
            Require(Mathf.Approximately(player.currentPlayerData.extraProjectiles, 1f) &&
                Mathf.Approximately(player.currentPlayerData.rangedDamage, 21f),
                "Start 전 저장 상태 복원: 풍요의 주머니 투사체 +1·원거리 피해 +5%");
            Require(playerRelics.CombatModifiers.HasProjectilePiercing, "지구본 장착: 관통 스냅샷 활성화");
            effects.ReevaluateAllConditions();
            Require(player.currentPlayerData.extraProjectiles == 1f, "반복 조건 평가에서 투사체 수 중복 증가 없음");

            WeaponData guidance = AssetDatabase.LoadAssetAtPath<WeaponData>(
                "Assets/Nytherion/Data/ScriptableObjects/Weapons/SpiritsGuidance.asset");
            playerRoot.transform.position = new Vector3(10000f, 10000f, 0f);
            GameObject weaponObject = Track(Object.Instantiate(guidance.weaponPrefab.gameObject, playerRoot.transform, false));
            RangedWeapon weapon = weaponObject.GetComponent<RangedWeapon>();
            weapon.Initialize(guidance);
            weapon.extraProjectileMode = ExtraProjectileMode.Spread;
            typeof(RangedWeapon).GetMethod("FireProjectiles", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(weapon, new object[] { Vector2.right, 1, 15f, 0f, 1f });
            CollisionObject[] bolts = ActiveBolts();
            Require(bolts.Length == 2, "풍요의 주머니 장착 시 실제 원거리 투사체 2발 생성");
            var enemyA = Track(new GameObject("검증 적 A"));
            enemyA.tag = "Enemy";
            Collider2D targetA = enemyA.AddComponent<BoxCollider2D>();
            var healthA = enemyA.AddComponent<RelicVerificationDamageTarget>();
            var enemyB = Track(new GameObject("검증 적 B"));
            enemyB.tag = "Enemy";
            Collider2D targetB = enemyB.AddComponent<BoxCollider2D>();
            var healthB = enemyB.AddComponent<RelicVerificationDamageTarget>();
            var wall = Track(new GameObject("검증 벽"));
            wall.tag = "Wall";
            Collider2D wallCollider = wall.AddComponent<BoxCollider2D>();
            CollisionObject bolt = bolts[0];
            bolt.SendMessage("OnTriggerEnter2D", targetA);
            bolt.SendMessage("OnTriggerEnter2D", targetB);
            Require(bolt.gameObject.activeSelf && healthA.Hits == 1 && healthB.Hits == 1,
                "관통 충돌 처리: 두 적에게 순서대로 피해를 주고 투사체 유지");
            bolt.SendMessage("OnTriggerEnter2D", wallCollider);
            Require(!bolt.gameObject.activeSelf, "지구본 관통 중에도 벽 충돌 시 풀 반환");
            ReturnBolts();

            Load(manager, ("SimpleStats/PouchOfAbundance.asset", 0, 0), ("SimpleStats/PandorasBow.asset", 4, 4));
            Require(player.currentPlayerData.extraProjectiles == 3f && !playerRelics.CombatModifiers.HasProjectilePiercing,
                "황금 성배 추가 장착 시 +3 누적, 지구본 해제 시 관통 제거");
            GameObject recycled = weapon.SpawnProj(Vector2.right);
            Require(!recycled.GetComponent<PiercingModifier>().enabled, "풀 재사용 시 해제된 유물 관통이 남지 않음");
            recycled.GetComponent<CollisionObject>().SendMessage("OnTriggerEnter2D", targetA);
            Require(!recycled.activeSelf, "관통 해제 후 첫 적 충돌에서 풀 반환");

            Load(manager, ("SimpleStats/PouchOfAbundance.asset", 0, 0), ("CombatUtility/Globe.asset", 4, 4));
            foreach (Type type in new[]
            {
                typeof(Nytherion.GamePlay.Combat.Weapons.ForestThornWeapon),
                typeof(Nytherion.GamePlay.Combat.Weapons.ChargedPiercingWeapon)
            })
            {
                var go = Track(new GameObject("검증 " + type.Name));
                go.transform.SetParent(playerRoot.transform, false);
                RangedWeapon charged = (RangedWeapon)go.AddComponent(type);
                charged.firePoint = go.transform;
                charged.Initialize(guidance);
                charged.extraProjectileMode = ExtraProjectileMode.Spread;
                type.GetMethod("FireChargedAttack", BindingFlags.NonPublic | BindingFlags.Instance)
                    .Invoke(charged, new object[] { Vector2.right, 0f });
                yield return new WaitForSeconds(0.12f);
                Require(ActiveBolts().Length == 2 && ActiveBolts().All(projectile => projectile.GetComponent<PiercingModifier>().enabled),
                    type.Name + ": 비차징 발사에서도 추가 투사체·유물 관통 유지");
                ReturnBolts();
            }

            SaveData saved = new SaveData();
            manager.PopulateSaveData(saved);
            string json = JsonUtility.ToJson(saved);
            manager.LoadFromSaveData(JsonUtility.FromJson<SaveData>(json));
            Require(manager.EquippedRelicCount == 2 && player.currentPlayerData.extraProjectiles == 1f &&
                playerRelics.CombatModifiers.HasProjectilePiercing, "기존 SaveData JSON 왕복 후 장착·투사체·관통 복원");
            }
            if (presentationOnly)
                Load(manager, ("SimpleStats/PouchOfAbundance.asset", 0, 0), ("CombatUtility/Globe.asset", 4, 4));

            // 실제 프리팹을 사용하되 검증 전용 UI와 매니저로 기존 보관함 데이터를 보존한다.
            var canvasRoot = Track(new GameObject("[RelicVerification] Canvas", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster)));
            Canvas canvas = canvasRoot.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32000;
            CanvasScaler scaler = canvasRoot.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            var uiRoot = new GameObject("유물 검증 보드", typeof(RectTransform));
            uiRoot.transform.SetParent(canvasRoot.transform, false);
            var ui = uiRoot.AddComponent<RelicGridUI>();
            ui.rootCanvas = canvas;
            ui.gridRoot = Rect("Grid", canvasRoot.transform, new Vector2(-470f, 0f));
            GridLayoutGroup gridLayout = ui.gridRoot.gameObject.AddComponent<GridLayoutGroup>();
            gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            gridLayout.constraintCount = 5;
            gridLayout.childAlignment = TextAnchor.MiddleCenter;
            ui.gridRoot.sizeDelta = new Vector2(440f, 440f);
            ui.placedBlocksContainer = Rect("Placed", canvasRoot.transform, Vector2.zero);
            ui.previewContainer = Rect("Preview", canvasRoot.transform, Vector2.zero);
            ui.blockStorageParent = Rect("Storage", canvasRoot.transform, new Vector2(240f, 270f));
            ui.blockStorageParent.pivot = new Vector2(0.5f, 1f);
            ui.blockStorageParent.sizeDelta = Vector2.one * 527f;
            var storageLayout = ui.blockStorageParent.gameObject.AddComponent<GridLayoutGroup>();
            storageLayout.spacing = Vector2.one * 5f;
            storageLayout.padding = new RectOffset(10, 10, 10, 10);
            ui.blockStorageParent.anchorMin = Vector2.zero;
            ui.blockStorageParent.anchorMax = Vector2.one;
            ui.storageSlotPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/Relic/StorageSlot.prefab");
            Set(ui, "slotCellPrefab", AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/Relic/RelicSlotCell.prefab"));
            Set(ui, "draggableBlockPrefab", AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/Relic/RelicBlockDraggable.prefab"));
            InputManager input = InputManager.Instance;
            Require(input != null, "기존 RootLifetimeScope 입력 매니저 사용");
            var builder = new ContainerBuilder();
            builder.RegisterInstance(manager);
            builder.RegisterInstance(ui);
            builder.RegisterInstance(input);
            RelicTooltip tooltip = Object.FindObjectOfType<RelicTooltip>(true);
            Require(tooltip != null, "씬의 비활성 툴팁 Inspector 참조 확인");
            builder.RegisterInstance(tooltip);
            resolver = builder.Build();
            ui.Construct(manager, resolver);
            manager.AddAllRelicsToStorage();
            yield return ui.Initialize();
            yield return null;
            yield return null;
            Require(ui.blockStorageParent.childCount == 16 && storageLayout.constraintCount == 4,
                "전체 유물 보유 상태에도 보관함은 페이지당 정확히 16칸·4열");
            Require(ui.blockStorageParent.rect.size == Vector2.one * 547f &&
                ui.blockStorageParent.anchorMin == ui.blockStorageParent.anchorMax,
                "늘어나는 앵커를 고정하고 4×4 슬롯·여백을 포함한 547×547 규격 적용");
            LayoutRebuilder.ForceRebuildLayoutImmediate(ui.blockStorageParent);
            RectTransform firstSlot = (RectTransform)ui.blockStorageParent.GetChild(0);
            RectTransform fourthSlot = (RectTransform)ui.blockStorageParent.GetChild(3);
            RectTransform fifthSlot = (RectTransform)ui.blockStorageParent.GetChild(4);
            RectTransform lastSlot = (RectTransform)ui.blockStorageParent.GetChild(15);
            Require(Mathf.Approximately(firstSlot.anchoredPosition.y, fourthSlot.anchoredPosition.y) &&
                Mathf.Approximately(firstSlot.anchoredPosition.x, fifthSlot.anchoredPosition.x) &&
                Mathf.Approximately(fourthSlot.anchoredPosition.x, lastSlot.anchoredPosition.x),
                "실제 LayoutRebuild 후 4×4 행렬의 슬롯 좌표 확인");
            Require(ui.gridRoot.GetComponent<GridLayoutGroup>().cellSize == Vector2.one * 96f,
                "유물 장착 칸 96×96 축소·32픽셀 슬롯 배경 3배");
            yield return ui.Initialize();
            yield return ui.Initialize();
            yield return null;
            yield return null;
            Require(ui.blockStorageParent.childCount == 16 && ui.gridRoot.childCount == 25,
                "창 반복 초기화와 같은 프레임 이벤트에서 슬롯 중복 생성 없음");
            RelicBlockDraggable draggable = ui.blockStorageParent.GetComponentInChildren<RelicBlockDraggable>();
            Image storedIcon = draggable.transform.parent.Find("Icon").GetComponent<Image>();
            Require(storedIcon.rectTransform.sizeDelta == Vector2.one * 96f && storedIcon.preserveAspect,
                "StorageSlot/Icon 실제 아이콘 96×96·비율 보존");
            var pointer = new PointerEventData(EventSystem.current)
                { button = PointerEventData.InputButton.Left, position = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f) };
            pointer.position = RectTransformUtility.WorldToScreenPoint(null, storedIcon.rectTransform.position);
            var raycasts = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, raycasts);
            Require(raycasts.Any(hit => hit.gameObject == draggable.gameObject),
                "보관함 Icon 자식 표시 중에도 마우스 Raycast가 유물 드래그 컴포넌트에 도달");
            // 실제 씬처럼 최상위 캔버스보다 표시 순서가 높은 유물 캔버스에서 드래그한다.
            RectTransform relicCanvasRoot = Rect("유물 드래그 검증 캔버스", canvasRoot.transform, Vector2.zero);
            relicCanvasRoot.sizeDelta = ((RectTransform)canvasRoot.transform).rect.size;
            Canvas relicCanvas = relicCanvasRoot.gameObject.AddComponent<Canvas>();
            relicCanvas.overrideSorting = true;
            relicCanvas.sortingOrder = canvas.sortingOrder + 1;
            ui.rootCanvas = relicCanvas;
            string[] originalStorageOrder = manager.GetStorageBlocks().Select(block => block.BlockId).ToArray();
            Transform originalStorageSlot = draggable.transform.parent;
            draggable.OnBeginDrag(pointer);
            Image draggedIcon = (Image)Get(draggable, "iconImage");
            yield return null;
            yield return null;
            Require(draggedIcon.isActiveAndEnabled && draggedIcon.canvas == relicCanvas &&
                draggable.transform.GetSiblingIndex() == relicCanvasRoot.childCount - 1,
                "중첩 유물 캔버스에서 드래그·UI 갱신 후에도 아이콘 표시 순서 유지");
            Require(draggedIcon.rectTransform.sizeDelta == Vector2.one * 96f &&
                draggable.transform.localScale == Vector3.one, "유물 드래그 96×96·Canvas 이동 후 배율 유지");
            Require(manager.GetStorageBlocks().Select(block => block.BlockId).SequenceEqual(originalStorageOrder) &&
                originalStorageSlot.GetComponentInChildren<RelicBlockDraggable>() == null && !storedIcon.enabled &&
                originalStorageSlot.GetComponent<Image>().enabled,
                "드래그 중 원래 슬롯은 빈 테두리로 유지하고 나머지 유물 순번 보존");
            draggable.CancelDrag();
            ui.rootCanvas = canvas;
            yield return null;
            yield return null;
            Require(manager.GetStorageBlocks().Select(block => block.BlockId).SequenceEqual(originalStorageOrder) &&
                originalStorageSlot.GetComponentInChildren<RelicBlockDraggable>().blockData.BlockId == originalStorageOrder[0],
                "드래그 취소 후 원래 보관 슬롯과 전체 순번 복원");
            yield return VerifyStorageDrag(manager, ui, pointer);
            string firstPageRelic = ui.blockStorageParent.GetComponentInChildren<RelicBlockDraggable>().blockData.BlockId;
            ui.ChangeStoragePage(1);
            yield return null;
            yield return null;
            Require(ui.blockStorageParent.childCount == 16 &&
                ui.blockStorageParent.GetComponentInChildren<RelicBlockDraggable>().blockData.BlockId != firstPageRelic,
                "다음 페이지로 16개 초과 유물 접근 가능");
            Sprite equippedFrame = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Nytherion/Art/UI/Relics/EquipedRelic_Slot.png");
            Require(ui.gridRoot.GetChild(0).GetComponent<Image>().sprite == equippedFrame,
                "유물 장착 칸에 EquipedRelic_Slot.png 적용");
            Sprite common = ui.storageSlotPrefab.GetComponent<Image>().sprite;
            foreach (string prefab in new[] { "SkillStorageSlot", "SkillEquipSlot", "SkillEquipSlot 1", "SkillEquipSlot 2" })
            {
                GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/" + prefab + ".prefab");
                Require(asset.GetComponent<Image>().sprite == common && asset.GetComponent<Image>().type == Image.Type.Sliced &&
                    asset.GetComponent<PixelPerfectSlotFrame>() != null && asset.GetComponent<RectTransform>().sizeDelta ==
                    Vector2.one * (prefab == "SkillStorageSlot" ? 128f : 160f),
                    prefab + ": 유물 슬롯과 동일한 배경 이미지·비율 보존");
            }
            Sprite iconSprite = AssetDatabase.LoadAssetAtPath<RelicData>(Relics + "SimpleStats/PouchOfAbundance.asset").Image;
            var skill = Track(ScriptableObject.CreateInstance<Nytherion.Data.ScriptableObjects.Skill.SkillData>());
            skill.icon = iconSprite;
            GameObject skillObject = Track(Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/UI/SkillEquipSlot.prefab"), relicCanvasRoot));
            var skillSlot = skillObject.GetComponent<SkillSlotUI>();
            skillSlot.Setup(skill);
            skillSlot.OnBeginDrag(pointer);
            Image skillIcon = (Image)Get(skillSlot, "skillIcon");
            Canvas.ForceUpdateCanvases();
            Require(skillIcon.isActiveAndEnabled && skillIcon.canvas == relicCanvas &&
                skillIcon.transform.GetSiblingIndex() == relicCanvasRoot.childCount - 1,
                "중첩 스킬 캔버스에서 드래그 아이콘 표시 순서 유지");
            Require(skillIcon.rectTransform.sizeDelta == Vector2.one * 96f && skillIcon.preserveAspect,
                "스킬 드래그도 96×96·비율 보존");
            skillSlot.OnEndDrag(pointer);
            Require(skillIcon.transform.parent == skillObject.transform,
                "스킬 드래그 종료 시 원래 슬롯으로 아이콘 복원");
            Require(skillIcon.rectTransform.sizeDelta == Vector2.one * 64f,
                "스킬 드래그 종료 시 장착 아이콘 64×64 복원");
            tooltip.Show(manager.GetBlockAt(0, 0));
            Require(((TextMeshProUGUI)Get(tooltip, "nameText")).color == Color.white,
                "유물 툴팁 제목 흰색");
            tooltip.Hide();
            Object.Destroy(skillObject);
            var missingObject = Track(Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/UI/Relic/RelicBlockDraggable.prefab"), canvasRoot.transform));
            var missing = missingObject.GetComponent<RelicBlockDraggable>();
            missing.blockData = new Nytherion.GamePlay.Relics.RelicBlock(AssetDatabase.LoadAssetAtPath<RelicData>(Relics + "ExtraTrigger.asset"));
            missing.BuildVisualFromShape();
            Require(missing.transform.Find("MissingIcon").gameObject.activeSelf &&
                missing.transform.Find("MissingIcon").GetComponent<TextMeshProUGUI>().text == "?",
                "아이콘 참조가 누락된 유물도 빈 슬롯 대신 ? 표시");
            Object.Destroy(missingObject);
            yield return null;
            ScreenCapture.CaptureScreenshot(RelicPresentationSetup.Output + "/slots.png");
            yield return new WaitForEndOfFrame();
            yield return VerifyFrameRendering();
        }

        private static IEnumerator VerifyFrameRendering()
        {
            // Game View 크기와 별개로 실제 UGUI를 텍스처에 렌더링해 네 방향 테두리 픽셀 수를 비교한다.
            var cameraObject = Track(new GameObject("[RelicVerification] 렌더 카메라"));
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.enabled = false;
            camera.orthographic = true;
            camera.orthographicSize = 320f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 10f;
            camera.cullingMask = 1 << 31;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            cameraObject.transform.position = new Vector3(20000f, 20000f, -5f);
            RenderTexture target = Track(new RenderTexture(640, 640, 24));
            target.antiAliasing = 1;
            camera.targetTexture = target;
            var root = Track(new GameObject("[RelicVerification] 렌더 Canvas", typeof(RectTransform), typeof(Canvas)));
            root.layer = 31;
            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1f;
            Texture2D readback = Track(new Texture2D(640, 640, TextureFormat.RGB24, false));
            string[] prefabs = { "Relic/StorageSlot", "Relic/RelicSlotCell", "SkillStorageSlot", "SkillEquipSlot" };
            foreach (string path in prefabs)
            {
                GameObject slot = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/" + path + ".prefab"), root.transform, false);
                slot.layer = 31;
                foreach (Transform child in slot.transform) child.gameObject.SetActive(false);
                RectTransform rect = slot.GetComponent<RectTransform>();
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = new Vector2(0.37f, 0.63f);
                var sizes = new List<string>();
                foreach (float scale in new[] { 0.24f, 0.75f, 1f, 1.25f, 1.5f })
                {
                    canvas.scaleFactor = scale;
                    yield return null;
                    Canvas.ForceUpdateCanvases();
                    slot.GetComponent<PixelPerfectSlotFrame>().Refresh();
                    Canvas.ForceUpdateCanvases();
                    camera.Render();
                    RenderTexture previous = RenderTexture.active;
                    RenderTexture.active = target;
                    readback.ReadPixels(new Rect(0, 0, 640, 640), 0, 0);
                    readback.Apply();
                    RenderTexture.active = previous;
                    Vector3[] corners = new Vector3[4];
                    rect.GetWorldCorners(corners);
                    Vector2 min = camera.WorldToScreenPoint(corners[0]);
                    Vector2 max = camera.WorldToScreenPoint(corners[2]);
                    int left = Mathf.RoundToInt(min.x), right = Mathf.RoundToInt(max.x) - 1;
                    int bottom = Mathf.RoundToInt(min.y), top = Mathf.RoundToInt(max.y) - 1;
                    int x = (left + right) / 2, y = (bottom + top) / 2;
                    int[] widths = {
                        EdgeWidth(readback, left, y, 1, 0), EdgeWidth(readback, right, y, -1, 0),
                        EdgeWidth(readback, x, bottom, 0, 1), EdgeWidth(readback, x, top, 0, -1)
                    };
                    int expected = Mathf.Max(1, Mathf.FloorToInt(rect.rect.width * scale / 32f + 0.5f));
                    if (widths.Any(width => width != expected))
                        throw new InvalidOperationException(path + " 배율 " + scale + " 테두리=" + string.Join(",", widths) + " 예상=" + expected);
                    sizes.Add(scale + "=" + expected + "px");
                    if (Mathf.Approximately(scale, 0.75f)) File.WriteAllBytes(RelicPresentationSetup.Output +
                        "/frame-" + path.Replace('/', '-') + ".png", readback.EncodeToPNG());
                }
                Require(true, path + ": 5개 화면 배율에서 좌·우·상·하 선 굵기 동일 (" + string.Join(" / ", sizes) + ")");
                Object.Destroy(slot);
                yield return null;
            }
        }

        private static int EdgeWidth(Texture2D texture, int x, int y, int dx, int dy)
        {
            Color32 edge = texture.GetPixel(x, y);
            if (edge.r == 0 && edge.g == 0 && edge.b == 0) return 0;
            for (int width = 1; width <= 12; width++)
            {
                Color32 pixel = texture.GetPixel(x + dx * width, y + dy * width);
                if (pixel.r != edge.r || pixel.g != edge.g || pixel.b != edge.b) return width;
            }
            return -1;
        }

        private static void Load(RelicManager manager, params (string path, int row, int col)[] relics)
        {
            var saved = new SaveData { relicData = new RelicGridState() };
            foreach (var relic in relics)
                saved.relicData.placedBlocks.Add(new RelicGridState.SavedRelicBlock
                {
                    relicId = AssetDatabase.LoadAssetAtPath<RelicData>(Relics + relic.path).relicName,
                    gridRow = relic.row, gridCol = relic.col
                });
            manager.LoadFromSaveData(saved);
        }

        private static CollisionObject[] ActiveBolts() => pool.GetComponentsInChildren<CollisionObject>()
            .Where(projectile => projectile.gameObject.activeSelf && projectile.transform.position.x > 9900f).ToArray();
        private static void ReturnBolts() { foreach (CollisionObject bolt in ActiveBolts()) bolt.ReturnToPool(); }
        private static T Track<T>(T value) where T : Object { temporary.Add(value); return value; }
        private static object Get(object target, string field) => target.GetType().GetField(field,
            BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
        private static void Set(object target, string field, object value) => target.GetType().GetField(field,
            BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        private static RectTransform Rect(string name, Transform parent, Vector2 position)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchoredPosition = position;
            return rect;
        }
        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
            results.Add("PASS " + message);
            File.WriteAllLines(RelicPresentationSetup.Output + "/verification-progress.txt", results);
        }
        private static IEnumerator Execute(IEnumerator verification)
        {
            var pending = new Stack<IEnumerator>();
            pending.Push(verification);
            while (pending.Count > 0)
            {
                object yielded;
                try
                {
                    IEnumerator current = pending.Peek();
                    if (!current.MoveNext()) { pending.Pop(); continue; }
                    yielded = current.Current;
                }
                catch (Exception error) { Finish(error); yield break; }
                if (yielded is IEnumerator nested) { pending.Push(nested); continue; }
                yield return yielded;
            }
            Finish(null);
        }
        private static IEnumerator VerifyStorageDrag(RelicManager manager, RelicGridUI ui, PointerEventData pointer)
        {
            foreach (var move in new[] { (from: 4, to: 1), (from: 1, to: 5), (from: 5, to: 5) })
            {
                var expected = manager.GetStorageBlocks().ToList();
                var block = expected[move.from];
                expected.RemoveAt(move.from);
                expected.Insert(move.to, block);
                RelicBlockDraggable draggable = ui.blockStorageParent.GetChild(move.from)
                    .GetComponentInChildren<RelicBlockDraggable>();
                draggable.OnBeginDrag(pointer);
                yield return null;
                yield return null;
                pointer.pointerCurrentRaycast = new RaycastResult { gameObject = ui.blockStorageParent.GetChild(move.to).gameObject };
                draggable.OnEndDrag(pointer);
                yield return null;
                yield return null;
                Require(manager.GetStorageBlocks().SequenceEqual(expected) &&
                    ui.blockStorageParent.GetChild(move.to).GetComponentInChildren<RelicBlockDraggable>().blockData == block,
                    $"보관함 {move.from + 1}→{move.to + 1} 삽입: 대상 슬롯 표시·다른 유물 상대 순서·중복 없음");
            }

            var original = manager.GetStorageBlocks().ToList();
            foreach (GameObject target in new[] { (GameObject)null, ui.gridRoot.GetChild(0).gameObject })
            {
                RelicBlockDraggable draggable = ui.blockStorageParent.GetChild(2).GetComponentInChildren<RelicBlockDraggable>();
                int rotation = draggable.blockData.RotationState;
                draggable.OnBeginDrag(pointer);
                manager.RotateDraggedBlock();
                pointer.pointerCurrentRaycast = new RaycastResult { gameObject = target };
                draggable.OnEndDrag(pointer);
                yield return null;
                yield return null;
                Require(manager.GetStorageBlocks().SequenceEqual(original) && original[2].RotationState == rotation &&
                    ui.blockStorageParent.GetChild(2).GetComponentInChildren<RelicBlockDraggable>().blockData == original[2],
                    target == null ? "패널 밖 드롭: 원래 슬롯·순번·회전 복원" : "점유된 장착 슬롯 드롭: 원래 보관 슬롯 복원");
            }

            RelicBlockDraggable equip = ui.blockStorageParent.GetChild(2).GetComponentInChildren<RelicBlockDraggable>();
            equip.OnBeginDrag(pointer);
            pointer.pointerCurrentRaycast = new RaycastResult { gameObject = ui.gridRoot.GetChild(1).gameObject };
            equip.OnEndDrag(pointer);
            yield return null;
            yield return null;
            Require(manager.GetBlockAt(0, 1) == original[2] && !manager.GetStorageBlocks().Contains(original[2]) &&
                ui.blockStorageParent.GetChild(2).GetComponentInChildren<RelicBlockDraggable>().blockData == original[3],
                "정상 장착이 확정된 뒤에만 보관함 빈 자리 정리");

            // 두 번째 페이지에서도 전역 순번에 삽입하고 저장/로드 후 순서를 유지한다.
            manager.UnequipFromGrid(original[2], new Vector2Int(1, 0));
            yield return null;
            yield return null;
            var expectedAcrossPages = manager.GetStorageBlocks().ToList();
            RelicBlockDraggable crossPage = ui.blockStorageParent.GetChild(1).GetComponentInChildren<RelicBlockDraggable>();
            RelicBlock moved = crossPage.blockData;
            crossPage.OnBeginDrag(pointer);
            ui.ChangeStoragePage(1);
            yield return null;
            yield return null;
            pointer.pointerCurrentRaycast = new RaycastResult { gameObject = ui.blockStorageParent.GetChild(2).gameObject };
            crossPage.OnEndDrag(pointer);
            expectedAcrossPages.Remove(moved);
            expectedAcrossPages.Insert(18, moved);
            yield return null;
            yield return null;
            Require(manager.GetStorageBlocks().SequenceEqual(expectedAcrossPages), "다른 페이지의 슬롯에도 전역 순번으로 삽입");
            SaveData save = new SaveData();
            manager.PopulateSaveData(save);
            manager.LoadFromSaveData(JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(save)));
            Require(manager.GetStorageBlocks().Select(block => block.RelicId)
                .SequenceEqual(expectedAcrossPages.Select(block => block.RelicId)), "기존 저장 형식 JSON 왕복 후 보관함 순서 보존");
            ui.ChangeStoragePage(-1);
            yield return null;
            yield return null;
        }

        private static void Finish(Exception error)
        {
            if (error != null) results.Add("FAIL " + error);
            File.WriteAllLines(RelicPresentationSetup.Output + "/verification.txt", results);
            resolver?.Dispose();
            resolver = null;
            foreach (GameObject go in temporary.OfType<GameObject>())
            {
                if (go == null) continue;
                RelicEffectController effects = go.GetComponent<RelicEffectController>();
                if (effects != null) typeof(RelicEffectController).GetMethod("RemoveAllActiveEffects",
                    BindingFlags.Instance | BindingFlags.NonPublic).Invoke(effects, null);
            }
            for (int i = temporary.Count - 1; i >= 0; i--)
                if (temporary[i] != null) Object.Destroy(temporary[i]);
            temporary.Clear();
            PoolInstance.SetValue(null, previousPool);
            Application.runInBackground = previousRunInBackground;
            running = false;
            if (SessionState.GetBool(Pending + ".Exit", false)) EditorApplication.isPlaying = false;
        }
    }

    public class RelicVerificationDamageTarget : MonoBehaviour, IDamageable
    {
        public int Hits { get; private set; }
        public void TakeDamage(float damageAmount, bool isChain = false) { Hits++; }
    }
}
