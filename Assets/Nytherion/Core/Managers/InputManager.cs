using UnityEngine;
using System;
using System.Collections.Generic;
using Nytherion.Core.Data;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Nytherion.Core.Managers
{
    public class InputManager : BaseManager
    {
        public static InputManager Instance { get; private set; }
        private PlayerAction playerActions;
        private bool isAttackPressed;
        private EventSystem attackEventSystem;
        private PointerEventData attackPointerData;
        private readonly List<RaycastResult> attackUIRaycasts = new List<RaycastResult>();

        [Header("유물 UI 입력")]
        [SerializeField, Tooltip("유물 창을 열고 닫는 키")]
        private Key relicUIToggleKey = Key.U;

        [SerializeField, Tooltip("주변 유물을 모두 획득하는 키")]
        private Key collectAllRelicsKey = Key.G;
        public Key CollectAllRelicsKey => collectAllRelicsKey;
        public bool CanCollectAllRelics => isActiveAndEnabled && playerActions != null &&
            playerActions.Player.Interact.enabled && Time.timeScale > 0f && !UIPanelBase.IsAnyPanelOpen;

        public Vector2 MoveInput { get; private set; }
        public bool Dash { get; private set; }
        public bool IsControlPressed { get; private set; }
        public bool IsShiftPressed { get; private set; }

        public event Action onAttackDown;

        public event Action onAttackUp;

        public event Action<int> onQuickSlotInput;

        public event Action<int> onSkillInput;
        public event Action onToggleSkillUI;
        public event Action onToggleProgressionUI;
        public event Action onToggleRelicUI;

        public event Action OnPausePressed;

        public event Action onInteract;
        public event Action onCollectAllRelics;

        public event Action onMap;

        public event Action onRelicRotate;

     
        protected override void Awake()
        {
            base.Awake();

            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            playerActions = new PlayerAction();
        }
        protected override void OnInitializeInternal()
        {
            playerActions.Player.Enable();
            playerActions.UI.Enable();
            playerActions.UI.Pause.performed += ctx =>
            {
                OnPausePressed?.Invoke();
            };
            playerActions.Player.Move.performed += ctx =>
            {
                MoveInput = ctx.ReadValue<Vector2>();
            };
            playerActions.Player.Move.canceled += ctx =>
            {
                MoveInput = Vector2.zero;
            };

            playerActions.Player.Attack.performed += HandleAttackPerformed;
            playerActions.Player.Attack.canceled += HandleAttackCanceled;

            playerActions.Player.Dash.started += ctx => Dash = true;
            playerActions.Player.Dash.canceled += ctx => Dash = false;

            playerActions.Player.Skill.performed += ctx => onToggleSkillUI?.Invoke();

            playerActions.Player.Skill_Q.performed += ctx => TriggerSkillInput(0);
            playerActions.Player.Skill_E.performed += ctx => TriggerSkillInput(2);
            playerActions.Player.Skill_R.performed += ctx => TriggerSkillInput(1);

            playerActions.Player.Progression.performed += ctx => onToggleProgressionUI?.Invoke();


            playerActions.Player.Control.performed += ctx => IsControlPressed = true;
            playerActions.Player.Control.canceled += ctx => IsControlPressed = false;
            playerActions.Player.Shift.performed += ctx => IsShiftPressed = true;
            playerActions.Player.Shift.canceled += ctx => IsShiftPressed = false;

            playerActions.Player.Interact.performed += _ => onInteract?.Invoke();

            playerActions.Player.WorldMap.started += ctx => onMap?.Invoke();

            playerActions.RelicUI.Rotate.performed += _ => onRelicRotate?.Invoke();
        }

        public override void PopulateSaveData(SaveData saveData)
        {
            // InputManager는 저장할 데이터가 없음
        }

        public override void LoadFromSaveData(SaveData saveData)
        {
            // InputManager는 로드할 데이터가 없음
        }
        private void OnEnable()
        {
            playerActions?.Enable();
            playerActions.Player.Control.Enable();
            playerActions.Player.Shift.Enable();
        }
        private void OnDisable()
        {
            playerActions?.Disable();
            playerActions.Player.Control.Disable();
            playerActions.Player.Shift.Disable();
            playerActions.Player.Skill.performed -= ctx => onToggleSkillUI?.Invoke();
            playerActions.Player.Skill_Q.performed -= ctx => TriggerSkillInput(0);
            playerActions.Player.Skill_E.performed -= ctx => TriggerSkillInput(2);
            playerActions.Player.Skill_R.performed -= ctx => TriggerSkillInput(1);
            playerActions.Player.Progression.performed -= ctx => onToggleProgressionUI?.Invoke();
        }

        public void DisableMovement()
        {
            playerActions.Player.Move.Disable();
            playerActions.Player.Dash.Disable();
            playerActions.Player.Attack.Disable();
        }

        public void EnableMovement()
        {
            playerActions.Player.Move.Enable();
            playerActions.Player.Dash.Enable();
            playerActions.Player.Attack.Enable();
        }
        public void OnFireSkill(InputAction.CallbackContext context)
        {
            onSkillInput?.Invoke(0);
        }

        public Vector2 MousePosition
        {
            get
            {
                if (Mouse.current != null)
                {
                    return Mouse.current.position.ReadValue();
                }
                return Vector2.zero;
            }
        }
        private void TriggerSkillInput(int skillIndex)
        {
            onSkillInput?.Invoke(skillIndex);
        }
        public void EnablePlayerControls() => playerActions.Player.Enable();
        public void DisablePlayerControls() => playerActions.Player.Disable();

        private void HandleAttackPerformed(InputAction.CallbackContext context)
        {
            if (isAttackPressed || IsAttackBlockedByUI()) return;

            isAttackPressed = true;
            onAttackDown?.Invoke();
        }

        private void HandleAttackCanceled(InputAction.CallbackContext context)
        {
            // UI에서 시작한 클릭은 드래그 후 놓아도 공격 종료/차징 발사를 전달하지 않습니다.
            if (!isAttackPressed) return;

            isAttackPressed = false;
            onAttackUp?.Invoke();
        }

        private bool IsAttackBlockedByUI()
        {
            if (UIPanelBase.IsAnyPanelOpen) return true;

            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null || Mouse.current == null) return false;

            // 입력 콜백에서는 EventSystem의 이전 프레임 포인터 상태 대신 현재 위치를 검사합니다.
            if (attackPointerData == null || attackEventSystem != eventSystem)
            {
                attackEventSystem = eventSystem;
                attackPointerData = new PointerEventData(eventSystem);
            }
            attackPointerData.Reset();
            attackPointerData.position = MousePosition;
            attackUIRaycasts.Clear();
            eventSystem.RaycastAll(attackPointerData, attackUIRaycasts);

            bool isOverUI = false;
            foreach (RaycastResult result in attackUIRaycasts)
            {
                if (result.module is GraphicRaycaster)
                {
                    isOverUI = true;
                    break;
                }
            }
            attackUIRaycasts.Clear();
            return isOverUI;
        }

        private void Update()
        {
            if (Keyboard.current != null && relicUIToggleKey != Key.None &&
                Keyboard.current[relicUIToggleKey].wasPressedThisFrame)
            {
                onToggleRelicUI?.Invoke();
            }

            if (Keyboard.current != null && collectAllRelicsKey != Key.None && CanCollectAllRelics &&
                Keyboard.current[collectAllRelicsKey].wasPressedThisFrame)
            {
                onCollectAllRelics?.Invoke();
            }
        }
    }
}
