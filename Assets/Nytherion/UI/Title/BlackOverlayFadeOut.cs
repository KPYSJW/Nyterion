using UnityEngine;

namespace Nytherion.UI.Title
{
    public class BlackOverlayFadeOut : MonoBehaviour
    {
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private float fadeDuration = 2.0f;

        private void Start()
        {
            if (canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>();
            }

            canvasGroup.alpha = 1f; // 검은 화면으로 시작
            StartCoroutine(FadeOut());
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
    }
}
