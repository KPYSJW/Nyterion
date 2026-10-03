using UnityEngine;
using UnityEngine.UI;

public class DragItemIcon : MonoBehaviour
{
    public static DragItemIcon Instance { get; private set; }
    public Image iconImage;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;

        }
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Canvas currentCanvas = GetComponent<Canvas>();
        if (currentCanvas == null)
        {
            currentCanvas = gameObject.AddComponent<Canvas>();
            currentCanvas.renderMode = RenderMode.ScreenSpaceCamera;
            currentCanvas.sortingOrder = 1000;
        }

        if (iconImage == null)
        {
            iconImage = GetComponentInChildren<Image>(true);
            if (iconImage == null)
            {
                Debug.LogError("[DragItemIcon] Image component for iconImage is not assigned and could not be found in children. Please assign it in the Inspector.");
            }
            else
            {
                Debug.Log("[DragItemIcon] iconImage was not set in inspector, but found in children.");
            }
        }
        Hide();
    }
    private void Update()
    {
        if (iconImage != null && iconImage.enabled)
        {
            Vector2 mousePos = Input.mousePosition;
            
            // 새로운 Input System 호환
            if (Nytherion.Core.Managers.InputManager.Instance != null)
            {
                mousePos = Nytherion.Core.Managers.InputManager.Instance.MousePosition;
            }

            Canvas canvas = GetComponentInParent<Canvas>();
            Camera cam = null;

            if (canvas != null && (canvas.renderMode == RenderMode.ScreenSpaceCamera || canvas.renderMode == RenderMode.WorldSpace))
            {
                cam = canvas.worldCamera;
                if (cam == null) cam = Camera.main;
            }

            RectTransform rt = transform as RectTransform;
            if (rt != null && canvas != null)
            {
                RectTransformUtility.ScreenPointToWorldPointInRectangle(canvas.transform as RectTransform, mousePos, cam, out Vector3 worldPoint);
                rt.position = worldPoint;
            }
            else
            {
                transform.position = mousePos;
            }
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void SetIcon(Sprite icon)
    {
        if (iconImage != null)
        {
            iconImage.sprite = icon;
            // 다른 경로에서 사용해도 이전 슬롯의 회전과 배율이 남지 않도록 초기화합니다.
            iconImage.rectTransform.localRotation = Quaternion.identity;
            iconImage.rectTransform.localScale = Vector3.one;
            iconImage.preserveAspect = true;
            iconImage.raycastTarget = false;
        }
        else
        {
            Debug.LogError("[DragItemIcon] iconImage is null. Cannot set icon.");
        }
    }

    public void SetSlotIcon(Image sourceImage)
    {
        if (sourceImage == null || iconImage == null) return;
        SetIcon(sourceImage.sprite);

        RectTransform targetRect = iconImage.rectTransform;
        RectTransform sourceRect = sourceImage.rectTransform;
        // 앵커를 고정해야 슬롯에서 복사한 크기가 드래그 캔버스 크기의 영향을 받지 않습니다.
        targetRect.anchorMin = targetRect.anchorMax = new Vector2(0.5f, 0.5f);
        targetRect.pivot = sourceRect.pivot;
        targetRect.anchoredPosition = Vector2.zero;
        targetRect.sizeDelta = sourceRect.rect.size;
        targetRect.localRotation = sourceRect.localRotation;
        targetRect.localScale = sourceRect.localScale;
        iconImage.preserveAspect = sourceImage.preserveAspect;
    }

    public void Show()
    {
        if (iconImage != null)
        {
            iconImage.enabled = true;
        }

        CanvasGroup group = GetComponent<CanvasGroup>();
        if (group == null)
        {
            group = gameObject.AddComponent<CanvasGroup>();
        }

        group.alpha = 1f;
        group.interactable = false;
        group.blocksRaycasts = false;
    }

    public void Hide()
    {
        if (iconImage != null)
        {
            iconImage.enabled = false;
        }

        CanvasGroup group = GetComponent<CanvasGroup>();
        if (group == null)
        {
            group = gameObject.AddComponent<CanvasGroup>();
        }

        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;
    }
}
