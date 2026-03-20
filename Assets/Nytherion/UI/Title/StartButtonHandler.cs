using UnityEngine;
using UnityEngine.UI;
using Nytherion.Core.Managers;
using VContainer;
using UnityEngine.SceneManagement;

namespace Nytherion.UI.Title
{
    public class StartButtonHandler : MonoBehaviour
    {
        [Header("Button Reference")]
        [SerializeField] private Button startButton;

        private SceneTransitionManager sceneTransitionManager;
        Scene scene;

        [Inject]
        public void Construct(SceneTransitionManager sceneTransitionManager)
        {
            this.sceneTransitionManager = sceneTransitionManager;
            scene = SceneManager.GetActiveScene();
            Debug.Log($"[StartButtonHandler] SceneTransitionManager injected: {sceneTransitionManager != null}");
        }

        private void Start()
        {
            // 버튼이 할당되지 않았으면 자동으로 찾기
            if (startButton == null)
            {
                startButton = GetComponent<Button>();
            }

            // 버튼 클릭 이벤트 연결
            if (startButton != null)
            {
                startButton.onClick.AddListener(OnStartButtonClicked);
            }
            else
            {
                Debug.LogError("[StartButtonHandler] Start button not found!");
            }
        }

        private void OnDestroy()
        {
            // 메모리 누수 방지
            if (startButton != null)
            {
                startButton.onClick.RemoveListener(OnStartButtonClicked);
            }
        }

        public void OnStartButtonClicked()
        {

            if (sceneTransitionManager != null)
            {
                if(scene.name == "Title")
                {
                    sceneTransitionManager.LoadScene("GameScene");
                }
                else if(scene.name == "TitleTest")
                {
                    sceneTransitionManager.LoadScene("GameSceneTest");
                }
            }
            else
            {
                Debug.LogError("[StartButtonHandler] SceneTransitionManager is null! Fallback to direct scene load.");
                // Fallback: 직접 씬 로드
                SceneManager.LoadScene("GameScene");
            }
        }
    }
}