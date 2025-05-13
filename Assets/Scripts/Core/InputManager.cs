using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;

namespace Game.Core
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

            inputActions.Player.Attack.performed += ctx => Debug.Log("���� ����");
            inputActions.Player.Attack.canceled += ctx => Debug.Log("���� ����");

            inputActions.Player.Dash.started += ctx => Dash=true;
            inputActions.Player.Dash.canceled += ctx => Dash = false;

            inputActions.Player.Skill_Q.started += ctx => Debug.Log("��ųQ ����");
            inputActions.Player.Skill_Q.canceled += ctx => Debug.Log("��ųQ ����");

            inputActions.Player.Skill_W.started += ctx => Debug.Log("��ųW ����");
            inputActions.Player.Skill_W.canceled += ctx => Debug.Log("��ųW ����");

            inputActions.Player.Skill_E.started += ctx => Debug.Log("��ųE ����");
            inputActions.Player.Skill_E.canceled += ctx => Debug.Log("��ųE ����");

            inputActions.Player.Skill_R.started += ctx => Debug.Log("��ųR ����");
            inputActions.Player.Skill_R.canceled += ctx => Debug.Log("��ųR ����");

            inputActions.Player.QuickSlot_0.started += ctx => Debug.Log("������0 ����");
            inputActions.Player.QuickSlot_0.canceled += ctx => Debug.Log("������0 ����");

            inputActions.Player.QuickSlot_1.started += ctx => Debug.Log("������1 ����");
            inputActions.Player.QuickSlot_1.canceled += ctx => Debug.Log("������1 ����");

            inputActions.Player.QuickSlot_2.started += ctx => Debug.Log("������2 ����");
            inputActions.Player.QuickSlot_2.canceled += ctx => Debug.Log("������2 ����");

            inputActions.Player.QuickSlot_3.started += ctx => Debug.Log("������3 ����");
            inputActions.Player.QuickSlot_3.canceled += ctx => Debug.Log("������3 ����");

            inputActions.Player.QuickSlot_4.started += ctx => Debug.Log("������4 ����");
            inputActions.Player.QuickSlot_4.canceled += ctx => Debug.Log("������4 ����");

            inputActions.Player.QuickSlot_5.started += ctx => Debug.Log("������5 ����");
            inputActions.Player.QuickSlot_5.canceled += ctx => Debug.Log("������5 ����");

            inputActions.Player.QuickSlot_6.started += ctx => Debug.Log("������6 ����");
            inputActions.Player.QuickSlot_6.canceled += ctx => Debug.Log("������6 ����");

            inputActions.Player.QuickSlot_7.started += ctx => Debug.Log("������7 ����");
            inputActions.Player.QuickSlot_7.canceled += ctx => Debug.Log("������7 ����");

            inputActions.Player.QuickSlot_8.started += ctx => Debug.Log("������8 ����");
            inputActions.Player.QuickSlot_8.canceled += ctx => Debug.Log("������8 ����");

            inputActions.Player.QuickSlot_9.started += ctx => Debug.Log("������9 ����");
            inputActions.Player.QuickSlot_9.canceled += ctx => Debug.Log("������9 ����");

        }

        private void Update()
        {
            /* if(Input.GetKeyDown(KeyCode.Space))//����ȯ �׽�Ʈ
             {
                 string currentScene = SceneManager.GetActiveScene().name;
                 string nextScene = currentScene == "PlayerTestScene" ? "test" : "PlayerTestScene";

                 Debug.Log($"�� ��ȯ: {currentScene} �� {nextScene}");
                 SceneManager.LoadScene(nextScene);
             }*/
        }


    }
}

