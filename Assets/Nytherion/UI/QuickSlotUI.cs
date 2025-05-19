using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;
public class QuickSlotUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public Image iconImage;
    public TextMeshProUGUI countText;
    public TextMeshProUGUI keyLabelText;
    public ItemData item;
    public int stackCount;

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

        // 슬롯 배경 이미지 초기화
        Image background = GetComponent<Image>();
        if (background != null)
            background.color = new Color(1, 1, 1, 1); // 완전한 흰색

        // Outline 제거
        Outline outline = GetComponent<Outline>();
        if (outline != null)
            outline.enabled = false;

        // Highlight 오브젝트 비활성화
        Transform highlight = transform.Find("Highlight");
        if (highlight != null)
            highlight.gameObject.SetActive(false);
    }

    public void Use()
    {
        if (item == null) return;

        Debug.Log($"Used quickslot item: {item.itemName}");

        stackCount--;
        if (stackCount <= 0)
            ClearSlot();
        else
            countText.text = stackCount.ToString();
    }

    public void SetKeyLabel(string label)
    {
        if (keyLabelText != null)
            keyLabelText.text = label;
    }
    public void OnBeginDrag(PointerEventData eventData)
    {
        if (item == null) return;
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
            if (targetSlot != null)
            {
                targetSlot.SetItem(item, stackCount);
                ClearSlot();
            }
        }
    }
}
