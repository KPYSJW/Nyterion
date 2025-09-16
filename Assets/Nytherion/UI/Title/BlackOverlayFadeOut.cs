using UnityEngine;
using Nytherion.Core.Managers;
using VContainer;

namespace Nytherion.UI.Title
{
    public class BlackOverlayFadeOut : MonoBehaviour
    {
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private float fadeDuration = 2.0f;
        private Coroutine fadeCoroutine;
        private SceneTransitionManager sceneTransitionManager;

        [Inject]
        public void Construct(SceneTransitionManager transitionManager)
        {
            this.sceneTransitionManager = transitionManager;
        }

        private void Start()
        {
            if (canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>();
            }
            
            if (sceneTransitionManager != null)
            {
                sceneTransitionManager.OnSceneStartLoading.AddListener(StopFade);
            }

            canvasGroup.alpha = 1f; // 검은 화면으로 시작
            fadeCoroutine = StartCoroutine(FadeOut());
        }

        private void OnDestroy()
        {
            sceneTransitionManager?.OnSceneStartLoading.RemoveListener(StopFade);
        }

        private System.Collections.IEnumerator FadeOut()
        {
            yield return new WaitForSecondsRealtime(0.1f);
            float elapsed = 0f;

            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / fadeDuration);

                float eased = t * t;

                canvasGroup.alpha = 1f - eased;
                yield return null;
            }

            canvasGroup.gameObject.SetActive(false); // 완전히 사라지면 비활성화
        }

        private void StopFade()
        {
            if (fadeCoroutine != null)
            {
                StopCoroutine(fadeCoroutine);
                fadeCoroutine = null;
                // 씬 전환이 시작되면, 이 CanvasGroup의 제어권을 즉시 포기해야 합니다.
                if (canvasGroup != null)
                {
                    canvasGroup.alpha = 0f;
                }
            }
        }
    }
}
