using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using System;


namespace Nytherion.Core
{
    /// <summary>
    /// 플레이어의 모든 입력을 처리하고 관련 이벤트를 발생시키는 관리자 클래스입니다.
    /// 싱글톤 패턴으로 구현되어 어디서든 접근이 가능합니다.
    /// </summary>
    public class InputManager : MonoBehaviour
    {
        /// <summary>
        /// InputManager의 싱글톤 인스턴스에 접근합니다.
        /// </summary>
        public static InputManager Instance { get; private set; }
        public GameObject inventoryPanel;
        private PlayerAction inputActions;

        /// <summary>
        /// 현재 이동 입력 벡터를 가져옵니다. (정규화되지 않음)
        /// </summary>
        public Vector2 MoveInput { get; private set; }

        /// <summary>
        /// 대시 입력 상태를 가져옵니다.
        /// </summary>
        public bool Dash { get; private set; }

        /// <summary>
        /// 공격 버튼을 누를 때 발생하는 이벤트입니다.
        /// </summary>
        public event Action onAttackDown;

        /// <summary>
        /// 공격 버튼을 뗄 때 발생하는 이벤트입니다.
        /// </summary>
        public event Action onAttackUp;

        /// <summary>
        /// 퀵슬롯 입력 시 발생하는 이벤트입니다. (0-9)
        /// </summary>
        public event Action<int> onQuickSlotInput;

        public event Action<int> onSkillInput;
        /// <summary>
        /// 컴포넌트가 활성화될 때 호출됩니다.
        /// 싱글톤 인스턴스를 초기화하고 입력 액션을 설정합니다.
        /// </summary>
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            inputActions = new PlayerAction();
            inputActions.Player.Enable();

            inputActions.Player.Move.performed += ctx => MoveInput = ctx.ReadValue<Vector2>();
            inputActions.Player.Move.canceled += ctx => MoveInput = Vector2.zero;

            inputActions.Player.Attack.performed += ctx => onAttackDown?.Invoke();//null 이 아니면 구독되어있는 함수 실행 
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

        }

        /// <summary>
        /// 매 프레임마다 호출됩니다.
        /// (현재는 사용되지 않음, 디버그용 코드가 주석 처리되어 있음)
        /// </summary>
        private void Update()
        {
            /* if(Input.GetKeyDown(KeyCode.Space))//씬전환 테스트
             {
                 string currentScene = SceneManager.GetActiveScene().name;
                 string nextScene = currentScene == "PlayerTestScene" ? "test" : "PlayerTestScene";

                 Debug.Log($"씬 전환: {currentScene} → {nextScene}");
                 SceneManager.LoadScene(nextScene);
             }*/
            if (Input.GetKeyDown(KeyCode.I))
            {
                inventoryPanel.SetActive(!inventoryPanel.activeSelf);
            }
        }


    }
}

