using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class InventorySlot : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerEnterHandler, IPointerExitHandler
{
    public Image iconImage;
    public TextMeshProUGUI countText;
    public ItemData item;
    public int stackCount = 0;

    private Transform originalParent;
    private Canvas parentCanvas;

    private void Start()
    {
        parentCanvas = GetComponentInParent<Canvas>();
    }

    public void SetItem(ItemData newItem, int count = 1)
    {
        item = newItem;
        stackCount = count;
        iconImage.sprite = newItem.icon;
        iconImage.enabled = true;
        countText.text = newItem.isStackable && count > 1 ? count.ToString() : "";
    }

    public void ClearSlot()
    {
        item = null;
        stackCount = 0;
        iconImage.sprite = null;
        iconImage.enabled = false;
        countText.text = "";
    }

    public void OnPointerClick(PointerEventData eventData) { }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (item == null) return;
        originalParent = transform.parent;
        DragItemIcon.Instance.SetIcon(iconImage.sprite);
        DragItemIcon.Instance.transform.position = Input.mousePosition;
        DragItemIcon.Instance.Show();
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (item == null) return;
        DragItemIcon.Instance.transform.position = Input.mousePosition;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        DragItemIcon.Instance.Hide();

        if (eventData.pointerEnter != null)
        {
            InventorySlot targetSlot = eventData.pointerEnter.GetComponentInParent<InventorySlot>();
            if (targetSlot != null && targetSlot != this)
            {
                InventoryManager.Instance.SwapItems(this, targetSlot);
            }
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (item != null) TooltipPanel.Instance.ShowTooltip(item);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        TooltipPanel.Instance.HideTooltip();
    }
}