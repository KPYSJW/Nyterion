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
        private PlayerAction inputActions;

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

            inputActions = new PlayerAction();
            
            // Player 액션 맵 활성화
            inputActions.Player.Enable();
            // UI 액션 맵 활성화
            inputActions.UI.Enable();
            
            // ESC 키가 눌렸을 때 메뉴가 열리도록 설정
            inputActions.UI.Pause.performed += ctx => {
                Debug.Log("Pause pressed");
                OnPausePressed?.Invoke();
            };

            inputActions.Player.Move.performed += ctx => MoveInput = ctx.ReadValue<Vector2>();
            inputActions.Player.Move.canceled += ctx => MoveInput = Vector2.zero;

            inputActions.Player.Attack.performed += ctx => onAttackDown?.Invoke();
            inputActions.Player.Attack.canceled += ctx => onAttackUp?.Invoke();

            inputActions.Player.Dash.started += ctx => Dash = true;
            inputActions.Player.Dash.canceled += ctx => Dash = false;

            inputActions.Player.Skill_Q.started += ctx => onSkillInput?.Invoke(0);
            inputActions.Player.Skill_W.started += ctx => onSkillInput?.Invoke(1);
            inputActions.Player.Skill_E.started += ctx => onSkillInput?.Invoke(2);
            inputActions.Player.Skill_R.started += ctx => onSkillInput?.Invoke(3);
            
            inputActions.Player.QuickSlot_0.started += ctx => onQuickSlotInput?.Invoke(0);
            inputActions.Player.QuickSlot_1.started += ctx => onQuickSlotInput?.Invoke(1);
            inputActions.Player.QuickSlot_2.started += ctx => onQuickSlotInput?.Invoke(2);
            inputActions.Player.QuickSlot_3.started += ctx => onQuickSlotInput?.Invoke(3);
            inputActions.Player.QuickSlot_4.started += ctx => onQuickSlotInput?.Invoke(4);
            inputActions.Player.QuickSlot_5.started += ctx => onQuickSlotInput?.Invoke(5);
            inputActions.Player.QuickSlot_6.started += ctx => onQuickSlotInput?.Invoke(6);
            inputActions.Player.QuickSlot_7.started += ctx => onQuickSlotInput?.Invoke(7);
            inputActions.Player.QuickSlot_8.started += ctx => onQuickSlotInput?.Invoke(8);
            inputActions.Player.QuickSlot_9.started += ctx => onQuickSlotInput?.Invoke(9);

            inputActions.Player.Interact.performed += _ => onInteract?.Invoke();
        }

        private void OnEnable()
        {
            inputActions.Player.Enable();
            inputActions.UI.Enable();
        }

        private void OnDisable()
        {
            inputActions.Player.Disable();
            inputActions.UI.Disable();
        }

    }
}

