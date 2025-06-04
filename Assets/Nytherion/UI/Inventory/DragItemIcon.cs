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
            // Optional: If this object should persist across scene loads
            // DontDestroyOnLoad(gameObject); 
        }
        else if (Instance != this)
        {
            Debug.LogWarning("[DragItemIcon] Another instance already exists. Destroying this one.");
            Destroy(gameObject);
            return; // Return to prevent further initialization
        }

        // Original Awake logic for Canvas setup (if still needed)
        // It's generally better if the Canvas is part of the prefab setup,
        // but this dynamic addition is kept as per original logic if required.
        Canvas currentCanvas = GetComponent<Canvas>();
        if (currentCanvas == null)
        {
            currentCanvas = gameObject.AddComponent<Canvas>();
            currentCanvas.renderMode = RenderMode.ScreenSpaceCamera; // Or ScreenSpaceOverlay depending on requirements
            // Find the main camera if ScreenSpaceCamera is used and it's not automatically assigned
            // if (currentCanvas.worldCamera == null && currentCanvas.renderMode == RenderMode.ScreenSpaceCamera)
            // {
            //    currentCanvas.worldCamera = Camera.main; 
            // }
            currentCanvas.sortingOrder = 1000; // Ensure it's on top
        }
        
        // Ensure iconImage is initially hidden or in a known state
        if (iconImage == null)
        {
            iconImage = GetComponentInChildren<Image>(true); // true to include inactive children
            if (iconImage == null)
            {
                Debug.LogError("[DragItemIcon] Image component for iconImage is not assigned and could not be found in children. Please assign it in the Inspector.");
                // Optionally, disable the component if iconImage is crucial and not found
                // enabled = false; 
                // return;
            }
            else
            {
                Debug.Log("[DragItemIcon] iconImage was not set in inspector, but found in children.");
            }
        }
        Hide(); // Start hidden
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
        else
        {
            Debug.LogError("[DragItemIcon] iconImage is null. Cannot show icon.");
        }
    }

    public void Hide()
    {
        if (iconImage != null)
        {
            iconImage.enabled = false;
        }
        else
        {
            // Don't log an error on Hide if it's null, as it might be called during destruction
            // or before full initialization in some edge cases. Or, make it consistent.
            // For now, let's be consistent with logging as per instructions.
            Debug.LogWarning("[DragItemIcon] iconImage is null. Cannot hide icon (already effectively hidden).");
        }
    }
}
