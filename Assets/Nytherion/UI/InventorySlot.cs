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
        if (item == null || iconImage.sprite == null || DragItemIcon.Instance == null)
        {
            return; // 드래그 취소
        }
        DragItemIcon.Instance.SetIcon(iconImage.sprite);
        DragItemIcon.Instance.transform.position = Input.mousePosition;
        DragItemIcon.Instance.Show();
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (item == null || DragItemIcon.Instance == null) return;
        DragItemIcon.Instance.transform.position = Input.mousePosition;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (item == null || DragItemIcon.Instance == null) return;
        DragItemIcon.Instance.Hide();

        if (eventData.pointerEnter != null)
        {
            InventorySlot targetSlot = eventData.pointerEnter.GetComponentInParent<InventorySlot>();
            if (targetSlot != null && targetSlot != this)
            {
                InventoryManager.Instance.SwapItems(this, targetSlot);
            }
        }

        if (eventData.pointerEnter != null)
        {
            // 퀵슬롯으로 드래그 등록 처리
            QuickSlotUI quickSlot = eventData.pointerEnter.GetComponentInParent<QuickSlotUI>();
            if (quickSlot != null)
            {
                quickSlot.SetItem(item, stackCount); // 복사 등록
                InventoryManager.Instance.RemoveItem(item, stackCount);
                return;
            }

            // 기본 인벤토리 슬롯간 교환 처리
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