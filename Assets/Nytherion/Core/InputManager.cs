using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;

namespace Nytherion.Core
{
    public class InputManager : MonoBehaviour
    {
        public static InputManager Instance;

        private PlayerAction inputActions;

        public Vector2 MoveInput { get; private set; }
        public bool Dash {  get; private set; }

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

            inputActions.Player.Attack.performed += ctx => Debug.Log("공격 시작");
            inputActions.Player.Attack.canceled += ctx => Debug.Log("공격 종료");

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

            inputActions.Player.QuickSlot_0.started += ctx => Debug.Log("퀵슬롯0 시작");
            inputActions.Player.QuickSlot_0.canceled += ctx => Debug.Log("퀵슬롯0 종료");

            inputActions.Player.QuickSlot_1.started += ctx => Debug.Log("퀵슬롯1 시작");
            inputActions.Player.QuickSlot_1.canceled += ctx => Debug.Log("퀵슬롯1 종료");

            inputActions.Player.QuickSlot_2.started += ctx => Debug.Log("퀵슬롯2 시작");
            inputActions.Player.QuickSlot_2.canceled += ctx => Debug.Log("퀵슬롯2 종료");

            inputActions.Player.QuickSlot_3.started += ctx => Debug.Log("퀵슬롯3 시작");
            inputActions.Player.QuickSlot_3.canceled += ctx => Debug.Log("퀵슬롯3 종료");

            inputActions.Player.QuickSlot_4.started += ctx => Debug.Log("퀵슬롯4 시작");
            inputActions.Player.QuickSlot_4.canceled += ctx => Debug.Log("퀵슬롯4 종료");

            inputActions.Player.QuickSlot_5.started += ctx => Debug.Log("퀵슬롯5 시작");
            inputActions.Player.QuickSlot_5.canceled += ctx => Debug.Log("퀵슬롯5 종료");

            inputActions.Player.QuickSlot_6.started += ctx => Debug.Log("퀵슬롯6 시작");
            inputActions.Player.QuickSlot_6.canceled += ctx => Debug.Log("퀵슬롯6 종료");

            inputActions.Player.QuickSlot_7.started += ctx => Debug.Log("퀵슬롯7 시작");
            inputActions.Player.QuickSlot_7.canceled += ctx => Debug.Log("퀵슬롯7 종료");

            inputActions.Player.QuickSlot_8.started += ctx => Debug.Log("퀵슬롯8 시작");
            inputActions.Player.QuickSlot_8.canceled += ctx => Debug.Log("퀵슬롯8 종료");

            inputActions.Player.QuickSlot_9.started += ctx => Debug.Log("퀵슬롯9 시작");
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

