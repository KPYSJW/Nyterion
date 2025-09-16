using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Events;
using Nytherion.Core.Data;
using System.Collections;
using VContainer;
using VContainer.Unity;

namespace Nytherion.Core.Managers
{
    public class SceneTransitionManager : BaseManager
    {

        [Header("Scene Settings")]
        [SerializeField] private float fadeOutDuration = 1.0f;
        [SerializeField] private float fadeInDuration = 1.5f;
        [SerializeField] private float holdDuration = 0.5f;
        [SerializeField] private float gameSceneHoldDuration = 2.0f; // GameScene 전용 긴 대기 시간

        [Header("Events")]
        public UnityEvent OnSceneStartLoading = new UnityEvent();
        public UnityEvent OnSceneLoaded = new UnityEvent();

        private CanvasGroup fadeCanvasGroup;
        private bool isTransitioning = false;

        protected override void Awake()
        {
            base.Awake();
            InitializeFadeCanvas();
        }


        private void InitializeFadeCanvas()
        {
            if (fadeCanvasGroup != null)
            {
                return;
            }

            GameObject existingCanvas = GameObject.Find("FadeCanvas");
            if (existingCanvas != null)
            {
                fadeCanvasGroup = existingCanvas.GetComponent<CanvasGroup>();
                if (fadeCanvasGroup == null)
                {
                    Destroy(existingCanvas);
                }
                else
                {
                    return;
                }
            }

            try
            {
                GameObject canvasObj = new GameObject("FadeCanvas");
                Canvas canvas = canvasObj.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 9999; // 다른 UI 위에 표시되도록 높은 값 사용

                fadeCanvasGroup = canvasObj.AddComponent<CanvasGroup>();

                // Boot 씬이나 전환 중일 때는 검은 화면으로 시작
                string currentScene = SceneManager.GetActiveScene().name;
                if (currentScene == "Boot" || isTransitioning)
                {
                    fadeCanvasGroup.alpha = 1f; // Boot 씬이나 전환 중은 검은 화면 유지
                }
                else
                {
                    fadeCanvasGroup.alpha = 0f; // 다른 씬은 투명하게 시작
                }

                fadeCanvasGroup.blocksRaycasts = fadeCanvasGroup.alpha > 0.1f;

                var image = canvasObj.AddComponent<UnityEngine.UI.Image>();
                image.color = Color.black;
                var rectTransform = image.GetComponent<RectTransform>();
                rectTransform.anchorMin = Vector2.zero;
                rectTransform.anchorMax = Vector2.one;
                rectTransform.sizeDelta = Vector2.zero;

                DontDestroyOnLoad(canvasObj);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[SceneTransitionManager] FadeCanvas 생성 중 오류 발생: {e.Message}");
            }
        }

        public void LoadTargetScene(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName))
            {
                return;
            }

            // Boot → Title: 검은 화면 유지 후 페이드 인
            if (sceneName == "Title")
            {
                StartCoroutine(BootToTitleTransition());
            }
            else
            {
                LoadScene(sceneName);
            }
        }

        public void LoadScene(string sceneName)
        {

            if (isTransitioning) 
            {
                return;
            }
            if (string.IsNullOrEmpty(sceneName))
            {
                return;
            }
            StartCoroutine(TransitionRoutine(sceneName));
        }

        public override void PopulateSaveData(SaveData saveData)
        {
            if (saveData == null) return;
        }

        public override void LoadFromSaveData(SaveData saveData)
        {
            if (saveData == null) return;
        }

        // Boot → Title 전환 (검은 화면 유지 후 페이드 인)
        private IEnumerator BootToTitleTransition()
        {
            isTransitioning = true;
            OnSceneStartLoading?.Invoke();

            if (fadeCanvasGroup != null)
            {
                fadeCanvasGroup.alpha = 1f;
                fadeCanvasGroup.blocksRaycasts = true;
            }

            // 씬 로딩 (검은 화면 유지)
            AsyncOperation asyncLoad = SceneManager.LoadSceneAsync("Title");
            asyncLoad.allowSceneActivation = false;

            while (asyncLoad.progress < 0.9f)
            {
                yield return null;
            }

            asyncLoad.allowSceneActivation = true;

            while (!asyncLoad.isDone)
            {
                yield return null;
            }

            // Title 씬 로딩 완료 후 FadeCanvas 재초기화 및 검은 화면 강제 설정
            yield return new WaitForEndOfFrame(); // 한 프레임 대기하여 씬 완전히 로딩

            InitializeFadeCanvas(); // FadeCanvas 재초기화
            if (fadeCanvasGroup != null)
            {
                fadeCanvasGroup.alpha = 1f; // 강제로 검은 화면 설정
                fadeCanvasGroup.blocksRaycasts = true;
            }

            // Title 씬 로딩 완료 후 추가 대기 (검은 화면 유지)
            yield return new WaitForSecondsRealtime(holdDuration * 2f); // 더 오래 검은 화면 유지

            // 천천히 페이드 인 (검은 화면 → 투명)
            yield return StartCoroutine(FadeWithDuration(0f, fadeInDuration));

            OnSceneLoaded?.Invoke();
            isTransitioning = false;
        }

        // 일반적인 씬 전환 (페이드 아웃 → 로딩 → 페이드 인)
        private IEnumerator TransitionRoutine(string sceneName)
        {
            isTransitioning = true;
            OnSceneStartLoading?.Invoke();

            // 페이드 아웃 (화면을 어둡게)
            yield return StartCoroutine(FadeWithDuration(1f, fadeOutDuration));

            // 씬 로딩
            AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneName);
            asyncLoad.allowSceneActivation = false;

            while (asyncLoad.progress < 0.9f)
            {
                yield return null;
            }

            asyncLoad.allowSceneActivation = true;

            while (!asyncLoad.isDone)
            {
                yield return null;
            }

            // 새 씬 로딩 완료 후 잠시 대기 (GameScene은 더 오래 대기)
            float waitTime = sceneName == "GameScene" ? gameSceneHoldDuration : holdDuration;
            yield return new WaitForSecondsRealtime(waitTime);

            // 페이드 인 (화면을 밝게)
            yield return StartCoroutine(FadeWithDuration(0f, fadeInDuration));

            OnSceneLoaded?.Invoke();
            isTransitioning = false;
        }

        // 지정된 시간으로 페이드
        private IEnumerator FadeWithDuration(float targetAlpha, float duration)
        {
            if (fadeCanvasGroup == null)
            {
                InitializeFadeCanvas();
                if (fadeCanvasGroup == null)
                {
                    yield break;
                }
            }

            float startAlpha = fadeCanvasGroup.alpha;
            float elapsedTime = 0f;

            // 페이드 시작 시 레이캐스트 설정
            fadeCanvasGroup.blocksRaycasts = true;

            while (elapsedTime < duration)
            {
                if (fadeCanvasGroup == null)
                {
                    yield break;
                }

                float progress = Mathf.Clamp01(elapsedTime / duration);
                // Smooth curve for more natural fade
                float smoothProgress = Mathf.SmoothStep(0f, 1f, progress);
                fadeCanvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, smoothProgress);

                elapsedTime += Time.unscaledDeltaTime;
                yield return null;
            }

            if (fadeCanvasGroup != null)
            {
                fadeCanvasGroup.alpha = targetAlpha;
                // 투명할 때만 레이캐스트 비활성화
                fadeCanvasGroup.blocksRaycasts = targetAlpha > 0.1f;
            }
        }

        // 기본 fadeDuration 사용하는 레거시 메서드
        private IEnumerator Fade(float targetAlpha)
        {
            return FadeWithDuration(targetAlpha, fadeOutDuration);
        }
    }
}