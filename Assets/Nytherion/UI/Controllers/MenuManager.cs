using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using VContainer;

namespace Nytherion.UI.Controllers
{
    public class MenuManager : UIPanelBase
    {
        [Header("UI References")]
        private GameObject mainPanel;
        private Button resumeButton;
        private Button settingsButton;
        private GameObject settingsPanel;
        private Button controlButton;
        private GameObject controlsPanel;
        private Button mainMenuButton;
        [SerializeField] private GameSceneUIRefs gameSceneuiRefs;

        [Header("Input")]
        [SerializeField] private InputActionReference toggleMenuAction;

        [Inject]
        public void Construct(
             CanvasGroup controlledCanvasGroup,
             GameSceneUIRefs gameSceneuiRefs)
        {
            this.controlledCanvasGroup = controlledCanvasGroup;
            this.mainPanel = gameSceneuiRefs.MenuMainPanel;
            this.resumeButton = gameSceneuiRefs.MenuResumeButton;
            this.settingsButton = gameSceneuiRefs.MenuSettingsButton;
            this.settingsPanel = gameSceneuiRefs.MenuSettingsPanel;
            this.controlButton = gameSceneuiRefs.MenuControlButton;
            this.controlsPanel = gameSceneuiRefs.MenuControlsPanel;
            this.mainMenuButton = gameSceneuiRefs.MenuMainMenuButton;
        }

        protected override void Awake()
        {
            base.Awake();
        }

        private void OnEnable()
        {
            resumeButton.onClick.AddListener(Close);
            settingsButton.onClick.AddListener(OpenSettings);
            controlButton.onClick.AddListener(OpenControls);
            mainMenuButton.onClick.AddListener(ReturnToMainMenu);

            if (toggleMenuAction != null)
            {
                toggleMenuAction.action.Enable();
                toggleMenuAction.action.performed += OnToggleMenu;
            }
        }

        private void OnDisable()
        {
            if (toggleMenuAction != null && toggleMenuAction.action != null)
            {
                toggleMenuAction.action.performed -= OnToggleMenu;
            }
        }

        private void OnToggleMenu(InputAction.CallbackContext context)
        {
            Toggle();
        }
        
        public override void Open()
        {
            base.Open();
            mainPanel.SetActive(true);
            settingsPanel.SetActive(false);
            controlsPanel.SetActive(false);
        }
        
        protected override void OnPanelStateChanged(bool isOpen)
        {
            if (isOpen)
            {
                Time.timeScale = 0f;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else
            {
                Time.timeScale = 1f;
            }
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