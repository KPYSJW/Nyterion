using UnityEngine;
using System;

namespace Nytherion.Core.Managers
{
    public class InputManager : MonoBehaviour
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

        public event Action OnPausePressed;

        public event Action onInteract;

        public event Action onMap;

        public event Action onEngravingRotate;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            playerActions = new PlayerAction();
        }
        public void Initialize()
        {
            playerActions.Player.Enable();
            playerActions.UI.Enable();
            playerActions.UI.Pause.performed += ctx =>
            {
                Debug.Log("Pause pressed");
                OnPausePressed?.Invoke();
            };
            playerActions.Player.Move.performed += ctx => MoveInput = ctx.ReadValue<Vector2>();
            playerActions.Player.Move.canceled += ctx => MoveInput = Vector2.zero;

            playerActions.Player.Attack.performed += ctx => onAttackDown?.Invoke();
            playerActions.Player.Attack.canceled += ctx => onAttackUp?.Invoke();

            playerActions.Player.Dash.started += ctx => Dash = true;
            playerActions.Player.Dash.canceled += ctx => Dash = false;

            playerActions.Player.Skill_Q.started += ctx => onSkillInput?.Invoke(0);
            playerActions.Player.Skill_W.started += ctx => onSkillInput?.Invoke(1);
            playerActions.Player.Skill_E.started += ctx => onSkillInput?.Invoke(2);
            playerActions.Player.Skill_R.started += ctx => onSkillInput?.Invoke(3);

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

        public void EnablePlayerControls() => playerActions.Player.Enable();
        public void DisablePlayerControls() => playerActions.Player.Disable();
    }
}