using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class ScrollbarControl : MonoBehaviour
{
    public Scrollbar scrollbar;
    public ScrollRect scrollRect;
    [Range(0.01f, 1f)] public float handleSize = 0.2f;

    private bool userDragging = false;

    void Start()
    {
        scrollbar.size = handleSize;

        scrollbar.onValueChanged.AddListener(OnScrollbarValueChanged);

        StartCoroutine(InitializeScrollToTop());
    }

    void OnScrollbarValueChanged(float value)
    {
        if (!userDragging) return;

        scrollRect.verticalNormalizedPosition = value;
    }

    public void OnBeginDrag()
    {
        userDragging = true;
    }

    public void OnEndDrag()
    {
        userDragging = false;
    }

    private IEnumerator InitializeScrollToTop()
    {
        yield return new WaitForEndOfFrame();
        scrollRect.verticalNormalizedPosition = 1f;
        scrollbar.value = 1f;
    }
}

