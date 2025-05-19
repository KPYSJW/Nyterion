using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class TooltipPanel : MonoBehaviour
{
    public static TooltipPanel Instance;
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI descriptionText;
    public GameObject panel;

    private void Awake()
    {
        Instance = this;
        HideTooltip();
    }

    public void ShowTooltip(ItemData item)
    {
        titleText.text = item.itemName;
        descriptionText.text = item.description;
        panel.SetActive(true);
    }

    public void HideTooltip()
    {
        panel.SetActive(false);
    }
}
