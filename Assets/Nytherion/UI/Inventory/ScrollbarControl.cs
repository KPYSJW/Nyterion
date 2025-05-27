using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class ScrollbarControl : MonoBehaviour
{
    public Scrollbar scrollbar;            // UI에 붙어있는 Scrollbar
    public ScrollRect scrollRect;          // 스크롤되는 영역
    [Range(0.01f, 1f)] public float handleSize = 0.2f;

    public float scrollValue = 0f;

    private bool userDragging = false;

    void Start()
    {
        scrollbar.size = handleSize;

        scrollbar.onValueChanged.AddListener(OnScrollbarValueChanged);

        // 스크롤 위치 초기화 코루틴 시작
        StartCoroutine(InitializeScrollToTop());
    }

    void Update()
    {
        if (!userDragging)
        {
            scrollbar.value = scrollRect.verticalNormalizedPosition;
        }
    }

    void OnScrollbarValueChanged(float value)
    {
        userDragging = true;
        scrollRect.verticalNormalizedPosition = value;
        scrollValue = value;
        userDragging = false;
    }

    // 코루틴으로 UI 초기화 후 스크롤 맨 위로 이동
    private IEnumerator InitializeScrollToTop()
    {
        yield return new WaitForEndOfFrame(); // UI가 완전히 그려진 다음 프레임에 실행
        scrollRect.verticalNormalizedPosition = 1f;
        scrollbar.value = 1f;
        scrollValue = 1f;
    }
}
