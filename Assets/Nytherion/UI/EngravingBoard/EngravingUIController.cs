using UnityEngine;
using Nytherion.Core;

namespace Nytherion.UI.EngravingBoard
{
    public class EngravingUIController : MonoBehaviour
    {
        public static EngravingUIController Instance { get; private set; }

        [SerializeField]
        private GameObject engravingUIPanel;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
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
            InputManager.Instance.DisablePlayerControls();

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        public void CloseEngravingUI()
        {
            if (!engravingUIPanel.activeSelf) return;

            engravingUIPanel.SetActive(false);
            InputManager.Instance.EnablePlayerControls();

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

    }
}