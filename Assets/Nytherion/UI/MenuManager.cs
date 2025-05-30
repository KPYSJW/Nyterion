using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.InputSystem;

namespace Nytherion.UI
{
    public class MenuManager : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private GameObject menuUI;
        [SerializeField] private GameObject mainPanel;
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private GameObject settingsPanel;
        [SerializeField] private Button controlButton;
        [SerializeField] private GameObject controlsPanel;
        [SerializeField] private Button mainMenuButton;


        [Header("Input")]
        [SerializeField] private InputActionReference toggleMenuAction;

        private bool isPaused = false;

        private void OnEnable()
        {
            // 버튼 이벤트 연결
            resumeButton.onClick.AddListener(ResumeGame);
            settingsButton.onClick.AddListener(OpenSettings);
            controlButton.onClick.AddListener(OpenControls);
            mainMenuButton.onClick.AddListener(ReturnToMainMenu);

            // 입력 액션 설정
            if (toggleMenuAction != null)
            {
                toggleMenuAction.action.Enable();
                toggleMenuAction.action.performed += OnToggleMenu;
            }

            // 초기 상태 설정
            menuUI.SetActive(false);
        }


        private void OnDisable()
        {
            // 입력 액션 정리
            if (toggleMenuAction != null && toggleMenuAction.action != null)
            {
                toggleMenuAction.action.performed -= OnToggleMenu;
            }
        }

        private void OnToggleMenu(InputAction.CallbackContext context)
        {
            if (isPaused) ResumeGame();
            else PauseGame();
        }

        private void PauseGame()
        {
            Time.timeScale = 0f;
            menuUI.SetActive(true);
            mainPanel.SetActive(true);
            settingsPanel.SetActive(false);
            controlsPanel.SetActive(false);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            isPaused = true;
        }

        public void ResumeGame()
        {
            Time.timeScale = 1f;
            menuUI.SetActive(false);
            mainPanel.SetActive(false);
            settingsPanel.SetActive(false);
            controlsPanel.SetActive(false);
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            isPaused = false;
        }

        private void OpenSettings()
        {
            settingsPanel.SetActive(true);
            mainPanel.SetActive(false);
        }
        public void CloseSettings()
        {
            settingsPanel.SetActive(false);
            mainPanel.SetActive(true);
        }
        private void OpenControls()
        {
            controlsPanel.SetActive(true);
            mainPanel.SetActive(false);
        }

        public void CloseControls()
        {
            controlsPanel.SetActive(false);
            mainPanel.SetActive(true);
        }

        private void ReturnToMainMenu()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene("MainMenu");
        }
    }
}
