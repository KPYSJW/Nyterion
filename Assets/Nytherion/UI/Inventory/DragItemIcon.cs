using System.Collections;
using System.Collections.Generic;
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
            Debug.LogWarning("[DragItemIcon] Another instance already exists. Destroying this one.");
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
        }
        else
        {
            Debug.LogError("[DragItemIcon] iconImage is null. Cannot set icon.");
        }
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
