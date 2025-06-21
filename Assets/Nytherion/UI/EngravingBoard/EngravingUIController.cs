using UnityEngine;
using Nytherion.Core;

namespace Nytherion.UI.EngravingBoard
{
    public class EngravingUIController : MonoBehaviour
    {
        public static EngravingUIController Instance { get; private set; }

        [SerializeField]
        private GameObject engravingUIPanel; // 각인 UI의 최상위 패널 오브젝트

        private PlayerAction playerAction;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                playerAction = new PlayerAction();
            }
            else
            {
                Destroy(gameObject);
            }

            if (engravingUIPanel != null)
            {
                engravingUIPanel.SetActive(false);
            }
        }

        private void OnEnable()
        {
            playerAction.EngravingUI.Enable();
            playerAction.EngravingUI.Close.performed += _ => CloseEngravingUI();

            playerAction.Player.Interact.performed += _ => CloseIfOpen();
        }

        private void OnDisable()
        {
            playerAction.EngravingUI.Disable();
            playerAction.EngravingUI.Close.performed -= _ => CloseEngravingUI();
            playerAction.Player.Interact.performed -= _ => CloseIfOpen();
        }

        public void ToggleEngravingUI()
        {
            bool isOpen = !engravingUIPanel.activeSelf;
            if (isOpen)
            {
                OpenEngravingUI();
            }
            else
            {
                CloseEngravingUI();
            }
        }
        private void OpenEngravingUI()
        {
            engravingUIPanel.SetActive(true);
            UpdateActionMaps(true);

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        public void CloseEngravingUI()
        {
            if (!engravingUIPanel.activeSelf) return;

            engravingUIPanel.SetActive(false);
            UpdateActionMaps(false);

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void CloseIfOpen()
        {
            if (engravingUIPanel.activeSelf)
            {
                CloseEngravingUI();
            }
        }

        private void UpdateActionMaps(bool isEngravingUIOpen)
        {
            if (isEngravingUIOpen)
            {
                playerAction.Player.Disable();
            }
            else
            {
                playerAction.Player.Enable();
            }
        }
    }
}