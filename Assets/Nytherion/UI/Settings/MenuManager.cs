using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using VContainer;
using VContainer.Unity;

namespace Nytherion.UI.Controllers
{
    public class MenuManager : UIPanelBase, IInitializable
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

        // [SerializeField] private InputActionReference toggleMenuAction; // InputManager를 사용하므로 주석 처리

        [Inject]
        public void Construct(GameSceneUIRefs gameSceneuiRefs)
        {
            this.mainPanel = gameSceneuiRefs.MenuMainPanel;

            // --- CanvasGroup을 VContainer 주입 대신 직접 찾도록 수정 ---
            if (this.mainPanel != null)
            {
                this.controlledCanvasGroup = this.mainPanel.GetComponent<CanvasGroup>();
                if (this.controlledCanvasGroup == null)
                {
                    this.controlledCanvasGroup = this.mainPanel.GetComponentInParent<CanvasGroup>();
                }
            }
            // --------------------------------------------------------

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

        public void Initialize()
        {
            // 초기 상태는 모두 비활성화
            if (mainPanel != null) mainPanel.SetActive(false);
            if (settingsPanel != null) settingsPanel.SetActive(false);
            if (controlsPanel != null) controlsPanel.SetActive(false);

            // 이벤트 구독 (Start에서 한 번만 수행)
            if (Nytherion.Core.Managers.InputManager.Instance != null)
            {
                Nytherion.Core.Managers.InputManager.Instance.OnPausePressed += OnToggleMenu;
            }
            else
            {
                Debug.LogWarning("[MenuManager] InputManager.Instance가 null입니다. ESC 키가 작동하지 않을 수 있습니다.");
            }
        }

        private void OnEnable()
        {
            resumeButton.onClick.AddListener(Close);
            settingsButton.onClick.AddListener(OpenSettings);
            controlButton.onClick.AddListener(OpenControls);
            mainMenuButton.onClick.AddListener(ReturnToMainMenu);

            /* 기존 방식 주석 처리
            if (toggleMenuAction != null)
            {
                toggleMenuAction.action.Enable();
                toggleMenuAction.action.performed += OnToggleMenu;
            }
            */
        }

        private void OnDisable()
        {
            resumeButton.onClick.RemoveListener(Close);
            settingsButton.onClick.RemoveListener(OpenSettings);
            controlButton.onClick.RemoveListener(OpenControls);
            mainMenuButton.onClick.RemoveListener(ReturnToMainMenu);

            /* 기존 방식 주석 처리
            if (toggleMenuAction != null && toggleMenuAction.action != null)
            {
                toggleMenuAction.action.performed -= OnToggleMenu;
            }
            */
        }

        private void OnDestroy()
        {
            // 메모리 누수 방지를 위해 이벤트 구독 해제
            if (Nytherion.Core.Managers.InputManager.Instance != null)
            {
                Nytherion.Core.Managers.InputManager.Instance.OnPausePressed -= OnToggleMenu;
            }
        }

        private void OnToggleMenu() // InputAction.CallbackContext 파라미터 제거
        {
            if (globalUIManager != null && globalUIManager.IsAnyPanelOpen())
            {
                // 어떤 UI든(메뉴 자신 포함) 열려 있다면 닫습니다.
                globalUIManager.CloseCurrentPanel();
            }
            else
            {
                // 아무 창도 안 열려 있다면 메뉴를 엽니다.
                Open();
            }
        }
        
        // 기존 OnToggleMenu(InputAction.CallbackContext) 호환성을 위해 남겨둠 (주석 처리)
        /*
        private void OnToggleMenu(InputAction.CallbackContext context)
        {
            OnToggleMenu();
        }
        */
        
        public override void Open(bool closeOthers = true)
        {
            base.Open(closeOthers);
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