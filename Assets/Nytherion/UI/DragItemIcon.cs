using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class DragItemIcon : MonoBehaviour
{
    public static DragItemIcon Instance;
    public Image iconImage;

    private void Awake()
    {
        Instance = this;
        Hide();
    }

    public void SetIcon(Sprite icon)
    {
        iconImage.sprite = icon;
    }

    public void Show()
    {
        iconImage.enabled = true;
    }

    public void Hide()
    {
        iconImage.enabled = false;
    }
}
