using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Nytherion.UI.Components
{
    public class FadeTransition : MonoBehaviour
    {
        [Header("Fade Settings")]
        [SerializeField] private Image fadeImage;
        [SerializeField] private float fadeSpeed = 2f;

        private static FadeTransition instance;
        public static FadeTransition Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindObjectOfType<FadeTransition>();
                }
                return instance;
            }
        }

        private void Awake()
        {
            if (instance == null)
            {
                instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
                return;
            }

            if (fadeImage == null)
            {
                fadeImage = GetComponent<Image>();
            }

            // 시작 시 투명하게 설정
            SetAlpha(0f);
        }

        /// <summary>
        /// 페이드 인/아웃 효과를 실행합니다.
        /// </summary>
        /// <param name="targetAlpha">목표 알파 값 (0: 투명, 1: 불투명)</param>
        /// <param name="duration">페이드 지속 시간</param>
        public IEnumerator Fade(float targetAlpha, float duration = 0.3f)
        {
            if (fadeImage == null)
            {
                Debug.LogError("[FadeTransition] fadeImage가 null입니다!");
                yield break;
            }

            float startAlpha = fadeImage.color.a;
            float elapsedTime = 0f;

            while (elapsedTime < duration)
            {
                elapsedTime += Time.deltaTime;
                float currentAlpha = Mathf.Lerp(startAlpha, targetAlpha, elapsedTime / duration);
                SetAlpha(currentAlpha);
                yield return null;
            }

            SetAlpha(targetAlpha);
        }

        /// <summary>
        /// 페이드 아웃 → 액션 실행 → 페이드 인을 순서대로 실행합니다.
        /// </summary>
        /// <param name="action">페이드 아웃 중에 실행할 액션</param>
        /// <param name="fadeOutDuration">페이드 아웃 지속 시간</param>
        /// <param name="fadeInDuration">페이드 인 지속 시간</param>
        public IEnumerator FadeTransitionWithAction(System.Action action, float fadeOutDuration = 0.3f, float fadeInDuration = 0.3f)
        {
            // 페이드 아웃 (화면을 어둡게)
            yield return StartCoroutine(Fade(1f, fadeOutDuration));

            // 액션 실행 (플레이어 이동, 카메라 이동 등)
            action?.Invoke();

            // 한 프레임 대기 (위치 변경이 완전히 적용되도록)
            yield return new WaitForEndOfFrame();

            // 페이드 인 (화면을 밝게)
            yield return StartCoroutine(Fade(0f, fadeInDuration));
        }

        private void SetAlpha(float alpha)
        {
            if (fadeImage != null)
            {
                Color color = fadeImage.color;
                color.a = alpha;
                fadeImage.color = color;
            }
        }

        /// <summary>
        /// 즉시 페이드 아웃 (디버깅용)
        /// </summary>
        public void SetFadeOut()
        {
            SetAlpha(1f);
        }

        /// <summary>
        /// 즉시 페이드 인 (디버깅용)
        /// </summary>
        public void SetFadeIn()
        {
            SetAlpha(0f);
        }
    }
}