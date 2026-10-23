using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Nytherion.Core.Managers;
using Nytherion.Core.Enums;
using Nytherion.Data.ScriptableObjects.Weapons;
using Nytherion.GamePlay.Characters.Player;
using Nytherion.GamePlay.Combat;
using Nytherion.UI.Progression;
using Nytherion.UI.Skill;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using VContainer;
using Object = UnityEngine.Object;

namespace Nytherion.Editor
{
    [InitializeOnLoad]
    public static class PlayerAttackVerification
    {
        public const string Output = "output/player-attack";
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        static PlayerAttackVerification() => EditorApplication.update += Update;
        private static void Update()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode ||
                !File.Exists(Output + "/verify.request")) return;
            File.Delete(Output + "/verify.request");
            Verify();
        }

        [MenuItem("Tools/Nytherion/Review/Verify Attack Input Lifecycle")]
        public static void Verify()
        {
            Directory.CreateDirectory(Output);
            var results = new List<string>();
            Scene scene = EditorSceneManager.NewPreviewScene();
            var instanceField = typeof(InputManager).GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);
            InputManager previous = InputManager.Instance;
            var inputs = new List<InputManager>();
            try
            {
                InputManager MakeInput(string name)
                {
                    var root = new GameObject(name);
                    root.SetActive(false);
                    SceneManager.MoveGameObjectToScene(root, scene);
                    var input = root.AddComponent<InputManager>();
                    instanceField.SetValue(null, null);
                    Call(input, "Awake");
                    inputs.Add(input);
                    return input;
                }
                var first = MakeInput("첫 입력");
                var second = MakeInput("교체 입력");
                var combatRoot = new GameObject("공격 입력 수명 검증");
                SceneManager.MoveGameObjectToScene(combatRoot, scene);
                var combat = combatRoot.AddComponent<PlayerCombat>();
                Call(combat, "Start");
                combat.Construct(first);
                Require(Count(first, combat, "onAttackDown") == 1 && Count(first, combat, "onAttackUp") == 1,
                    "Start 이후 주입해도 누름·해제 입력 각 1회 구독");
                Raise(first, "onAttackDown");
                Require((bool)Field(combat, "isAttackHeld"), "누름 이벤트가 PlayerCombat에 전달");
                Raise(first, "onAttackUp");
                Require(!(bool)Field(combat, "isAttackHeld"), "해제 이벤트가 PlayerCombat에 전달");
                results.Add("PASS Start 이후 주입 및 실제 입력 이벤트 누름·해제 전달");

                combat.Construct(first);
                combat.Construct(first);
                Require(Count(first, combat, "onAttackDown") == 1, "반복 주입 시 중복 구독 없음");
                Call(combat, "OnDisable");
                Require(Count(first, combat, "onAttackDown") == 0, "비활성화 시 입력 구독 해제");
                Call(combat, "OnEnable");
                Call(combat, "OnEnable");
                Require(Count(first, combat, "onAttackDown") == 1, "재활성화·반복 활성화 시 한 번만 재연결");
                results.Add("PASS 반복 주입·비활성화·재활성화 후 입력 연결 복구 및 중복 방지");

                combat.Construct(second);
                Require(Count(first, combat, "onAttackDown") == 0 && Count(second, combat, "onAttackDown") == 1,
                    "입력 매니저 교체 시 이전 구독 해제");
                combat.enabled = false;
                Call(combat, "OnDisable");
                combat.Construct(first);
                Require(Count(first, combat, "onAttackDown") == 0 && Count(second, combat, "onAttackDown") == 0,
                    "비활성 상태의 주입은 입력을 받지 않음");
                combat.enabled = true;
                Call(combat, "OnEnable");
                Require(Count(first, combat, "onAttackDown") == 1, "다시 활성화되면 새 입력에 연결");
                Call(combat, "OnDisable");
                Require(Count(first, combat, "onAttackDown") == 0, "최종 구독 정리");
                results.Add("PASS 입력 매니저 교체·비활성 중 주입·재활성화·정리");
            }
            catch (Exception error) { results.Add("FAIL " + error); }
            finally
            {
                var actionAssets = new List<Object>();
                foreach (InputManager input in inputs)
                {
                    Call(input, "OnDisable");
                    actionAssets.Add(((PlayerAction)Field(input, "playerActions")).asset);
                }
                EditorSceneManager.ClosePreviewScene(scene);
                foreach (Object asset in actionAssets) if (asset != null) Object.DestroyImmediate(asset);
                instanceField.SetValue(null, previous);
            }
            File.WriteAllLines(Output + "/verification.txt", results);
        }

        public static int Count(InputManager input, PlayerCombat combat, string name) =>
            (Field(input, name) as Delegate)?.GetInvocationList().Count(handler => ReferenceEquals(handler.Target, combat)) ?? 0;
        public static void Raise(InputManager input, string name) => (Field(input, name) as Action)?.Invoke();

        public static bool VerifyEquippedWeapon()
        {
            Directory.CreateDirectory(Output);
            var results = new List<string>();
            EquipmentDataManager equipment = null;
            Nytherion.Data.ScriptableObjects.Items.EquipmentData original = null;
            var temporary = new List<Object>();
            GameObject firePoint = null;
            Mouse virtualMouse = null;
            Mouse previousMouse = Mouse.current;
            try
            {
                Require(Application.isPlaying && Application.persistentDataPath.Contains("_ScopeVerification_"), "격리 플레이 검증 경로에서만 실행");
                var player = Object.FindObjectOfType<PlayerManager>();
                Require(player != null && player.PlayerCombat != null, "실제 씬 플레이어와 전투 컴포넌트");
                PlayerCombat combat = player.PlayerCombat;
                InputManager input = InputManager.Instance;
                Require(input != null && Count(input, combat, "onAttackDown") == 1 && Count(input, combat, "onAttackUp") == 1,
                    "실제 씬 플레이어의 공격 누름·해제 구독 각 1회");
                Require(!UIPanelBase.IsAnyPanelOpen, "공격 검증 전 UI 패널 닫힘");
                Require(((PlayerAction)Field(input, "playerActions")).Player.Attack.enabled, "실제 공격 액션 활성화");
                var scope = Object.FindObjectOfType<GameSceneLifetimeScope>();
                var milestones = Object.FindObjectOfType<MilestoneUIController>();
                var skills = Object.FindObjectOfType<SkillUIController>();
                var refs = scope.Container.Resolve<GameSceneUIRefs>();
                Require(milestones != null && milestones.IsInitialized && skills != null && skills.IsInitialized,
                    "씬 진입 시 마일스톤·스킬 컨트롤러 생성 및 초기화");
                Require(!refs.ProgressionMainPanel.activeSelf && !milestones.IsOpen && !skills.IsOpen, "초기 패널 닫힘");
                int milestoneSlots = refs.ProgressionSlotParent.childCount;
                int skillSlots = refs.storageContent.childCount;
                milestones.Initialize();
                skills.Initialize();
                Require(milestoneSlots > 0 && refs.ProgressionSlotParent.childCount == milestoneSlots &&
                    skillSlots > 0 && refs.storageContent.childCount == skillSlots, "반복 초기화 시 UI 슬롯 중복 생성 없음");
                results.Add("PASS 마일스톤·스킬 UI 자동 초기화·초기 숨김·중복 초기화 방지");
                equipment = scope.Container.Resolve<EquipmentDataManager>();
                original = equipment.GetEquipment(EquipmentSlotType.Weapon);
                var pool = ObjectPoolManager.Instance;
                firePoint = new GameObject("검증용 총구");
                firePoint.transform.position = new Vector3(20000f, 20000f);
                Vector2 pointerPosition = FindClearPointerPosition();
                virtualMouse = InputSystem.AddDevice<Mouse>();
                InputSystem.QueueStateEvent(virtualMouse, new MouseState { position = pointerPosition });
                InputSystem.Update();
                void Click()
                {
                    InputSystem.QueueStateEvent(virtualMouse, new MouseState { position = pointerPosition }.WithButton(MouseButton.Left));
                    InputSystem.Update();
                    InputSystem.QueueStateEvent(virtualMouse, new MouseState { position = pointerPosition });
                    InputSystem.Update();
                }
                foreach (string name in new[] { "ShamanStaff", "Afterglow" })
                {
                    var data = Object.Instantiate(AssetDatabase.LoadAssetAtPath<WeaponData>(
                        "Assets/Nytherion/Data/ScriptableObjects/Weapons/" + name + ".asset"));
                    temporary.Add(data);
                    equipment.SetEquipment(EquipmentSlotType.Weapon, data, false);
                    Require(combat.currentWeapon != null && combat.currentWeapon.weaponData == data, name + " 실제 장비 이벤트로 무기 생성");
                    var ranged = combat.currentWeapon as RangedWeapon;
                    Require(ranged != null, name + " 원거리 무기 타입");
                    ranged.firePoint = firePoint.transform;
                    int Before() => pool.GetComponentsInChildren<CollisionObject>().Count(projectile => projectile.transform.position.x > 19000f);
                    int before = Before();
                    if (name == "ShamanStaff")
                    {
                        foreach (var panel in new UIPanelBase[] { milestones, skills, scope.Container.Resolve<Nytherion.UI.Controllers.InventoryUI>() })
                        {
                            // 실제 입력 이벤트 구독도 중복 초기화 후 1회만 반응해야 한다.
                            if (panel == milestones) Raise(input, "onToggleProgressionUI");
                            else if (panel == skills) Raise(input, "onToggleSkillUI");
                            else panel.Open(false);
                            Require(panel.IsOpen && UIPanelBase.IsAnyPanelOpen, panel.name + " 열림");
                            Click();
                            Require(Before() == before && !(bool)Field(combat, "isAttackHeld"), panel.name + " 열린 동안 공격 차단");
                            if (panel == milestones) Raise(input, "onToggleProgressionUI");
                            else if (panel == skills) Raise(input, "onToggleSkillUI");
                            else panel.Close();
                            Require(!panel.IsOpen && !UIPanelBase.IsAnyPanelOpen, panel.name + " 닫힘 및 차단 해제");
                        }
                        results.Add("PASS 마일스톤·스킬 단축키 이벤트 및 인벤토리 열림 중 공격 차단·닫힘 후 해제");
                    }
                    // 입력 차단 검사까지 포함한 실제 액션 경로를 가상 마우스로 실행한다.
                    Click();
                    Require(Before() > before, name + " 입력 이벤트 → 장착 무기 → 실제 투사체 생성");
                    results.Add("PASS " + name + ": 장비 변경·가상 마우스 클릭·공격 액션·실제 투사체 생성");
                    combat.enabled = false;
                    Require(Count(input, combat, "onAttackDown") == 0, "실제 컴포넌트 비활성화 시 구독 해제");
                    combat.enabled = true;
                    Require(Count(input, combat, "onAttackDown") == 1, "실제 컴포넌트 재활성화 시 구독 복구");
                }
                results.Add("PASS 실제 플레이어의 비활성화·재활성화 후 다음 무기 공격 유지");
                return true;
            }
            catch (Exception error) { results.Add("FAIL " + error); return false; }
            finally
            {
                if (virtualMouse != null)
                {
                    InputSystem.QueueStateEvent(virtualMouse, new MouseState());
                    InputSystem.Update();
                    InputSystem.RemoveDevice(virtualMouse);
                    previousMouse?.MakeCurrent();
                }
                if (equipment != null) equipment.SetEquipment(EquipmentSlotType.Weapon, original, false);
                if (firePoint != null) Object.Destroy(firePoint);
                foreach (Object asset in temporary) if (asset != null) Object.Destroy(asset);
                File.WriteAllLines(Output + "/equipped-weapon-verification.txt", results);
            }
        }

        internal static Vector2 FindClearPointerPosition()
        {
            if (EventSystem.current == null) return new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            var pointer = new PointerEventData(EventSystem.current);
            var hits = new List<RaycastResult>();
            foreach (float y in new[] { 0.5f, 0.3f, 0.7f })
            foreach (float x in new[] { 0.5f, 0.3f, 0.7f })
            {
                pointer.position = new Vector2(Screen.width * x, Screen.height * y);
                hits.Clear();
                EventSystem.current.RaycastAll(pointer, hits);
                if (!hits.Any(hit => hit.module is GraphicRaycaster)) return pointer.position;
            }
            throw new InvalidOperationException("검증 클릭 영역 전체가 UI에 가려져 있습니다: " + string.Join(",", hits.Select(hit => hit.gameObject.name)));
        }
        private static object Field(object target, string name) => target.GetType().GetField(name, Private).GetValue(target);
        private static void Call(object target, string name) => target.GetType().GetMethod(name, Private).Invoke(target, null);
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    }
}
