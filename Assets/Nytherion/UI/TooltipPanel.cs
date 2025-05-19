using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class TooltipPanel : MonoBehaviour
{
    public static TooltipPanel Instance;
    public RectTransform panel;
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI descriptionText;
    private bool isVisible = false;
    private void Awake()
    {
        Instance = this;
        HideTooltip();
    }

    private void LateUpdate()
    {
        if (!isVisible) return;

        Vector2 localPoint;
        RectTransform canvasRect = panel.GetComponentInParent<Canvas>().GetComponent<RectTransform>();

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect,
            Input.mousePosition,
            null,
            out localPoint
        );

        Vector2 offset = new Vector2(4, 4); // 살짝 마우스에서 띄움
        panel.anchoredPosition = localPoint + offset;
    }

    public void ShowTooltip(ItemData item)
    {
        if (item == null) return;

        titleText.text = item.itemName;
        descriptionText.text = item.description;

        isVisible = true;
        panel.gameObject.SetActive(true);
    }

    public void HideTooltip()
    {
        isVisible = false;
        panel.gameObject.SetActive(false);
    }
}
