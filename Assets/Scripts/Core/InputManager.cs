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

            inputActions.Player.Attack.performed += ctx => Debug.Log("°ø°Ý ½ÃÀÛ");
            inputActions.Player.Attack.canceled += ctx => Debug.Log("°ø°Ý Á¾·á");

            inputActions.Player.Dash.started += ctx => Dash=true;
            inputActions.Player.Dash.canceled += ctx => Dash = false;

            inputActions.Player.Skill_Q.started += ctx => Debug.Log("½ºÅ³Q ½ÃÀÛ");
            inputActions.Player.Skill_Q.canceled += ctx => Debug.Log("½ºÅ³Q Á¾·á");

            inputActions.Player.Skill_W.started += ctx => Debug.Log("½ºÅ³W ½ÃÀÛ");
            inputActions.Player.Skill_W.canceled += ctx => Debug.Log("½ºÅ³W Á¾·á");

            inputActions.Player.Skill_E.started += ctx => Debug.Log("½ºÅ³E ½ÃÀÛ");
            inputActions.Player.Skill_E.canceled += ctx => Debug.Log("½ºÅ³E Á¾·á");

            inputActions.Player.Skill_R.started += ctx => Debug.Log("½ºÅ³R ½ÃÀÛ");
            inputActions.Player.Skill_R.canceled += ctx => Debug.Log("½ºÅ³R Á¾·á");

            inputActions.Player.QuickSlot_0.started += ctx => Debug.Log("Äü½½·Ô0 ½ÃÀÛ");
            inputActions.Player.QuickSlot_0.canceled += ctx => Debug.Log("Äü½½·Ô0 Á¾·á");

            inputActions.Player.QuickSlot_1.started += ctx => Debug.Log("Äü½½·Ô1 ½ÃÀÛ");
            inputActions.Player.QuickSlot_1.canceled += ctx => Debug.Log("Äü½½·Ô1 Á¾·á");

            inputActions.Player.QuickSlot_2.started += ctx => Debug.Log("Äü½½·Ô2 ½ÃÀÛ");
            inputActions.Player.QuickSlot_2.canceled += ctx => Debug.Log("Äü½½·Ô2 Á¾·á");

            inputActions.Player.QuickSlot_3.started += ctx => Debug.Log("Äü½½·Ô3 ½ÃÀÛ");
            inputActions.Player.QuickSlot_3.canceled += ctx => Debug.Log("Äü½½·Ô3 Á¾·á");

            inputActions.Player.QuickSlot_4.started += ctx => Debug.Log("Äü½½·Ô4 ½ÃÀÛ");
            inputActions.Player.QuickSlot_4.canceled += ctx => Debug.Log("Äü½½·Ô4 Á¾·á");

            inputActions.Player.QuickSlot_5.started += ctx => Debug.Log("Äü½½·Ô5 ½ÃÀÛ");
            inputActions.Player.QuickSlot_5.canceled += ctx => Debug.Log("Äü½½·Ô5 Á¾·á");

            inputActions.Player.QuickSlot_6.started += ctx => Debug.Log("Äü½½·Ô6 ½ÃÀÛ");
            inputActions.Player.QuickSlot_6.canceled += ctx => Debug.Log("Äü½½·Ô6 Á¾·á");

            inputActions.Player.QuickSlot_7.started += ctx => Debug.Log("Äü½½·Ô7 ½ÃÀÛ");
            inputActions.Player.QuickSlot_7.canceled += ctx => Debug.Log("Äü½½·Ô7 Á¾·á");

            inputActions.Player.QuickSlot_8.started += ctx => Debug.Log("Äü½½·Ô8 ½ÃÀÛ");
            inputActions.Player.QuickSlot_8.canceled += ctx => Debug.Log("Äü½½·Ô8 Á¾·á");

            inputActions.Player.QuickSlot_9.started += ctx => Debug.Log("Äü½½·Ô9 ½ÃÀÛ");
            inputActions.Player.QuickSlot_9.canceled += ctx => Debug.Log("Äü½½·Ô9 Á¾·á");

        }

        private void Update()
        {
            /* if(Input.GetKeyDown(KeyCode.Space))//¾ÀÀüÈ¯ Å×½ºÆ®
             {
                 string currentScene = SceneManager.GetActiveScene().name;
                 string nextScene = currentScene == "PlayerTestScene" ? "test" : "PlayerTestScene";

                 Debug.Log($"¾À ÀüÈ¯: {currentScene} ¡æ {nextScene}");
                 SceneManager.LoadScene(nextScene);
             }*/
        }


    }
}

