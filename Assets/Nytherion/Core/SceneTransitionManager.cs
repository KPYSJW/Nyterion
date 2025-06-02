using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Events;
using System.Collections;

public class SceneTransitionManager : MonoBehaviour
{
    public static SceneTransitionManager Instance { get; private set; }

    [Header("Scene Settings")]
    [SerializeField] private string defaultSceneToLoad = "GameScene";
    [SerializeField] private float fadeDuration = 0.5f;

    [Header("Events")]
    public UnityEvent OnSceneStartLoading = new UnityEvent();
    public UnityEvent OnSceneLoaded = new UnityEvent();

    private CanvasGroup fadeCanvasGroup;
    private bool isTransitioning = false;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeFadeCanvas();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        if (fadeCanvasGroup != null)
        {
            // Start with black screen
            fadeCanvasGroup.alpha = 1f;
            // Fade in from black
            StartCoroutine(Fade(1f, 0f));
        }
    }


    private void InitializeFadeCanvas()
    {
        GameObject canvasObj = new GameObject("FadeCanvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 999; // Ensure it's on top

        fadeCanvasGroup = canvasObj.AddComponent<CanvasGroup>();
        fadeCanvasGroup.alpha = 0f;
        fadeCanvasGroup.blocksRaycasts = false;

        // Add a full-screen image for fading
        var image = canvasObj.AddComponent<UnityEngine.UI.Image>();
        image.color = Color.black;
        var rectTransform = image.GetComponent<RectTransform>();
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.sizeDelta = Vector2.zero;

        DontDestroyOnLoad(canvasObj);
    }

    public void LoadTargetScene()
    {
        if (string.IsNullOrEmpty(defaultSceneToLoad))
        {
            Debug.LogWarning($"[{nameof(SceneTransitionManager)}] defaultSceneToLoad is not set.");
            return;
        }

        LoadScene(defaultSceneToLoad);
    }


    public void LoadScene(string sceneName)
    {
        if (isTransitioning) return;
        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogWarning($"[{nameof(SceneTransitionManager)}] Scene name is null or empty.");
            return;
        }

        StartCoroutine(TransitionRoutine(sceneName));
    }

    private IEnumerator TransitionRoutine(string sceneName)
    {
        isTransitioning = true;
        OnSceneStartLoading?.Invoke();

        // Fade out
        yield return StartCoroutine(Fade(0f, 1f));

        // Load the new scene
        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneName);
        asyncLoad.allowSceneActivation = false;

        // Wait until the asynchronous scene fully loads
        while (!asyncLoad.isDone)
        {
            if (asyncLoad.progress >= 0.9f)
            {
                asyncLoad.allowSceneActivation = true;
            }
            yield return null;
        }

        // Fade in
        yield return StartCoroutine(Fade(1f, 0f));

        OnSceneLoaded?.Invoke();
        isTransitioning = false;
    }

    private IEnumerator Fade(float startAlpha, float targetAlpha)
    {
        if (fadeCanvasGroup == null) yield break;
        
        float elapsedTime = 0f;
        fadeCanvasGroup.alpha = startAlpha;
        fadeCanvasGroup.blocksRaycasts = true;

        while (elapsedTime < fadeDuration)
        {
            if (fadeCanvasGroup != null)
            {
                fadeCanvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, elapsedTime / fadeDuration);
            }
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        if (fadeCanvasGroup != null)
        {
            fadeCanvasGroup.alpha = targetAlpha;
            fadeCanvasGroup.blocksRaycasts = targetAlpha > 0;
        }
    }
}
