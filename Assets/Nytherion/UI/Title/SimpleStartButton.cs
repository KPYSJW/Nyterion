using UnityEngine;
using Nytherion.Core.Managers;
using VContainer;
using UnityEngine.SceneManagement;
using VContainer.Unity;

namespace Nytherion.UI.Title
{
   
    public class SimpleStartButton : MonoBehaviour
    {
        [Header("Scene Settings")]
        [SerializeField] private string targetSceneName = "Boot";

        private SceneTransitionManager sceneTransitionManager;

        [Inject]
        public void Construct(SceneTransitionManager sceneTransitionManagerPrefab)
        {
            this.sceneTransitionManager = sceneTransitionManagerPrefab;
            Debug.Log($"<color=cyan>[SimpleStartButton] SceneTransitionManager 주입됨 (ID: {sceneTransitionManager?.GetInstanceID()})</color>");
        }

        public void StartGame()
        {
            if (string.IsNullOrEmpty(targetSceneName))
            {
                return;
            }

            if (sceneTransitionManager != null)
            {
                Debug.Log("[SimpleStartButton] 'Start Game'");
                // sceneTransitionManager.LoadScene(targetSceneName);
            }
            else
            {
                Debug.LogError("[SimpleStartButton] SceneTransitionManager가 null입니다! 씬을 전환할 수 없습니다.");
                // SceneManager.LoadScene(targetSceneName);
            }
        }

       
        public void QuitGame()
        {
            #if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
            #else
                Application.Quit();
            #endif
        }

        public void OpenSettings()
        {
            Debug.Log("[SimpleStartButton] OpenSettings() called");
            // TODO: 설정 UI 구현
        }

    }
}