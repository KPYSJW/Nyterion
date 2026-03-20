using UnityEngine;
using UnityEngine.UI;
using Nytherion.Core.Managers;
using VContainer;
using VContainer.Unity;
using UnityEngine.SceneManagement;

namespace Nytherion.UI.Title
{
   
    public class TitleMenuManager : MonoBehaviour
    {
        [Header("UI Buttons")]
        [SerializeField] private Button startButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button quitButton;

        Scene scene;
        private SceneTransitionManager sceneTransitionManager;

        [Inject]
        public void Construct(SceneTransitionManager sceneTransitionManager)
        {
            this.sceneTransitionManager = sceneTransitionManager;
            scene = SceneManager.GetActiveScene();
        }

        private void Awake()
        {
            InitializeButtons();
        }

        private void Start()
        {
            if (sceneTransitionManager == null)
            {
                TryManualInjection();
            }
        }

        private void TryManualInjection()
        {
            try
            {
                // TitleLifetimeScope에서 찾기
                var titleScope = FindObjectOfType<TitleLifetimeScope>();
                if (titleScope != null && titleScope.Container != null)
                {
                    sceneTransitionManager = titleScope.Container.Resolve<SceneTransitionManager>();
                    return;
                }

                // RootLifetimeScope에서 직접 찾기
                if (RootLifetimeScope.Instance != null && RootLifetimeScope.Instance.Container != null)
                {
                    sceneTransitionManager = RootLifetimeScope.Instance.Container.Resolve<SceneTransitionManager>();
                    return;
                }

            }
            catch (System.Exception e)
            {
                Debug.LogError($"[TitleMenuManager] 수동 주입 실패: {e.Message}");
            }
        }

        private void InitializeButtons()
        {
            // Start Button 설정
            if (startButton != null)
            {
                startButton.onClick.AddListener(OnStartGame);
            }
            else
            {
                Debug.LogError("[TitleMenuManager] Start 버튼이 할당되지 않았습니다!");
            }

            // Settings Button 설정
            if (settingsButton != null)
            {
                settingsButton.onClick.AddListener(OnSettings);
            }

            // Quit Button 설정
            if (quitButton != null)
            {
                quitButton.onClick.AddListener(OnQuit);
            }
        }

        private void OnDestroy()
        {
            // 메모리 누수 방지
            if (startButton != null) startButton.onClick.RemoveListener(OnStartGame);
            if (settingsButton != null) settingsButton.onClick.RemoveListener(OnSettings);
            if (quitButton != null) quitButton.onClick.RemoveListener(OnQuit);
        }

        
        public void OnStartGame()
        {
            if (sceneTransitionManager == null)
            {
                Debug.LogError("[TitleMenuManager] SceneTransitionManager is null!");
                return;
            }

            if(scene.name == "Title")
            {
                sceneTransitionManager.LoadScene("GameScene");
            }
            else if(scene.name == "TitleTest")
            {
                sceneTransitionManager.LoadScene("GameSceneTest");
            }
        }

        public void OnSettings()
        {
        }

        public void OnQuit()
        {
            #if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
            #else
                Application.Quit();
            #endif
        }

        [ContextMenu("Test Start Game")]
        public void TestStartGame() => OnStartGame();
    }
}