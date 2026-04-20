using UnityEngine;
using System;
using Nytherion.Core.Data;
using UnityEngine.InputSystem;

namespace Nytherion.Core.Managers
{
    public class InputManager : BaseManager
    {
        public static InputManager Instance { get; private set; }
        private PlayerAction playerActions;

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

        public event Action OnPausePressed;

        public event Action onInteract;

        public event Action onMap;

        public event Action onEngravingRotate;

     
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

            playerActions.Player.Attack.performed += ctx => 
            {
                onAttackDown?.Invoke();};
            playerActions.Player.Attack.canceled += ctx => onAttackUp?.Invoke();

            playerActions.Player.Dash.started += ctx => Dash = true;
            playerActions.Player.Dash.canceled += ctx => Dash = false;

            playerActions.Player.Skill.performed += ctx => onToggleSkillUI?.Invoke();

            playerActions.Player.Skill_Q.performed += ctx => TriggerSkillInput(0);
            playerActions.Player.Skill_E.performed += ctx => TriggerSkillInput(1);
            playerActions.Player.Skill_R.performed += ctx => TriggerSkillInput(2);

            playerActions.Player.Progression.performed += ctx => onToggleProgressionUI?.Invoke();

            playerActions.Player.QuickSlot_1.started += ctx => onQuickSlotInput?.Invoke(1);
            playerActions.Player.QuickSlot_2.started += ctx => onQuickSlotInput?.Invoke(2);
            playerActions.Player.QuickSlot_3.started += ctx => onQuickSlotInput?.Invoke(3);

            playerActions.Player.Control.performed += ctx => IsControlPressed = true;
            playerActions.Player.Control.canceled += ctx => IsControlPressed = false;
            playerActions.Player.Shift.performed += ctx => IsShiftPressed = true;
            playerActions.Player.Shift.canceled += ctx => IsShiftPressed = false;

            playerActions.Player.Interact.performed += _ => onInteract?.Invoke();

            playerActions.Player.WorldMap.started += ctx => onMap?.Invoke();

            playerActions.EngravingUI.Rotate.performed += _ => onEngravingRotate?.Invoke();
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
            playerActions.Player.Skill_E.performed -= ctx => TriggerSkillInput(1);
            playerActions.Player.Skill_R.performed -= ctx => TriggerSkillInput(2);
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
    }
}