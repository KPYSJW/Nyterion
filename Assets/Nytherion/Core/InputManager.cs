using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using System;
using Nytherion.UI;


namespace Nytherion.Core
{
    public class InputManager : MonoBehaviour
    {
        public static InputManager Instance { get; private set; }
        private PlayerAction playerActions;

        public Vector2 MoveInput { get; private set; }

        public bool Dash { get; private set; }

        public event Action onAttackDown;

        public event Action onAttackUp;

        public event Action<int> onQuickSlotInput;

        public event Action<int> onSkillInput;

        public event Action OnPausePressed;

        public event Action onInteract;

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
            playerActions.EngravingUI.Enable();
            playerActions.GachaUI.Enable();
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

            playerActions.Player.QuickSlot_0.started += ctx => onQuickSlotInput?.Invoke(0);
            playerActions.Player.QuickSlot_1.started += ctx => onQuickSlotInput?.Invoke(1);
            playerActions.Player.QuickSlot_2.started += ctx => onQuickSlotInput?.Invoke(2);
            playerActions.Player.QuickSlot_3.started += ctx => onQuickSlotInput?.Invoke(3);
            playerActions.Player.QuickSlot_4.started += ctx => onQuickSlotInput?.Invoke(4);
            playerActions.Player.QuickSlot_5.started += ctx => onQuickSlotInput?.Invoke(5);
            playerActions.Player.QuickSlot_6.started += ctx => onQuickSlotInput?.Invoke(6);
            playerActions.Player.QuickSlot_7.started += ctx => onQuickSlotInput?.Invoke(7);
            playerActions.Player.QuickSlot_8.started += ctx => onQuickSlotInput?.Invoke(8);
            playerActions.Player.QuickSlot_9.started += ctx => onQuickSlotInput?.Invoke(9);

            playerActions.Player.Interact.performed += _ => onInteract?.Invoke();
        }
        private void OnEnable()
        {
            if (playerActions != null)
            {
                playerActions.Enable();
            }
        }

        private void OnDisable()
        {
            if (playerActions != null)
            {
                playerActions.Disable();
            }
        }

    }
}

