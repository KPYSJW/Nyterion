using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using System;


namespace Nytherion.Core
{
    public class InputManager : MonoBehaviour
    {
        public static InputManager Instance;

        private PlayerAction inputActions;

        public Vector2 MoveInput { get; private set; }
        public bool Dash {  get; private set; }

        public event Action onAttackDown;
        public event Action onAttackUp;
        public event Action<int> onQuickSlotInput;

        private void Awake()
        {
            if (Instance != null)
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

            inputActions.Player.Dash.started += ctx => Dash=true;
            inputActions.Player.Dash.canceled += ctx => Dash = false;

            inputActions.Player.Skill_Q.started += ctx => Debug.Log("스킬Q 시작");
            inputActions.Player.Skill_Q.canceled += ctx => Debug.Log("스킬Q 종료");

            inputActions.Player.Skill_W.started += ctx => Debug.Log("스킬W 시작");
            inputActions.Player.Skill_W.canceled += ctx => Debug.Log("스킬W 종료");

            inputActions.Player.Skill_E.started += ctx => Debug.Log("스킬E 시작");
            inputActions.Player.Skill_E.canceled += ctx => Debug.Log("스킬E 종료");

            inputActions.Player.Skill_R.started += ctx => Debug.Log("스킬R 시작");
            inputActions.Player.Skill_R.canceled += ctx => Debug.Log("스킬R 종료");

            inputActions.Player.QuickSlot_0.started += ctx => onQuickSlotInput?.Invoke(0);
            inputActions.Player.QuickSlot_0.canceled += ctx => Debug.Log("퀵슬롯0 종료");

            inputActions.Player.QuickSlot_1.started += ctx => onQuickSlotInput?.Invoke(1);
            inputActions.Player.QuickSlot_1.canceled += ctx => Debug.Log("퀵슬롯1 종료");

            inputActions.Player.QuickSlot_2.started += ctx => onQuickSlotInput?.Invoke(2);
            inputActions.Player.QuickSlot_2.canceled += ctx => Debug.Log("퀵슬롯2 종료");

            inputActions.Player.QuickSlot_3.started += ctx => onQuickSlotInput?.Invoke(3);
            inputActions.Player.QuickSlot_3.canceled += ctx => Debug.Log("퀵슬롯3 종료");

            inputActions.Player.QuickSlot_4.started += ctx => onQuickSlotInput?.Invoke(4);
            inputActions.Player.QuickSlot_4.canceled += ctx => Debug.Log("퀵슬롯4 종료");

            inputActions.Player.QuickSlot_5.started += ctx => onQuickSlotInput?.Invoke(5);
            inputActions.Player.QuickSlot_5.canceled += ctx => Debug.Log("퀵슬롯5 종료");

            inputActions.Player.QuickSlot_6.started += ctx => onQuickSlotInput?.Invoke(6);
            inputActions.Player.QuickSlot_6.canceled += ctx => Debug.Log("퀵슬롯6 종료");

            inputActions.Player.QuickSlot_7.started += ctx => onQuickSlotInput?.Invoke(7);
            inputActions.Player.QuickSlot_7.canceled += ctx => Debug.Log("퀵슬롯7 종료");

            inputActions.Player.QuickSlot_8.started += ctx => onQuickSlotInput?.Invoke(8);
            inputActions.Player.QuickSlot_8.canceled += ctx => Debug.Log("퀵슬롯8 종료");

            inputActions.Player.QuickSlot_9.started += ctx => onQuickSlotInput?.Invoke(9);
            inputActions.Player.QuickSlot_9.canceled += ctx => Debug.Log("퀵슬롯9 종료");

        }

        private void Update()
        {
            /* if(Input.GetKeyDown(KeyCode.Space))//씬전환 테스트
             {
                 string currentScene = SceneManager.GetActiveScene().name;
                 string nextScene = currentScene == "PlayerTestScene" ? "test" : "PlayerTestScene";

                 Debug.Log($"씬 전환: {currentScene} → {nextScene}");
                 SceneManager.LoadScene(nextScene);
             }*/
        }


    }
}

