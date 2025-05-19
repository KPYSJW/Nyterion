// QuickSlotUI.cs
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Nytherion.Data.ScriptableObjects.Items;
using UnityEngine.EventSystems;
using Nytherion.Core;

namespace Nytherion.UI.Inventory
{
    public class QuickSlotUI : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private Image iconImage;
        [SerializeField] private TextMeshProUGUI countText;
        [SerializeField] private TextMeshProUGUI keyLabelText;

        private ItemData currentItem;
        private int currentCount;

        public void SetItem(ItemData item, int count = 1)
        {
            currentItem = item;
            currentCount = count;

            if (item == null)
            {
                iconImage.enabled = false;
                countText.text = "";
                return;
            }

            iconImage.sprite = item.icon;
            iconImage.enabled = true;
            countText.text = item.isStackable && count > 1 ? count.ToString() : "";
        }

        public void ClearSlot()
        {
            currentItem = null;
            currentCount = 0;
            iconImage.enabled = false;
            iconImage.sprite = null;
            countText.text = "";
        }

        public void SetKeyLabel(string label)
        {
            if (keyLabelText != null)
            {
                keyLabelText.text = label;
            }
        }

        public void Use()
        {
            if (currentItem == null)
            {
                Debug.Log("[QuickSlot] No item assigned to this slot.");
                return;
            }

            Debug.Log($"[QuickSlot] Used item: {currentItem.itemName}");
            currentCount--;
            if (currentCount <= 0)
                ClearSlot();
            else
                countText.text = currentItem.isStackable && currentCount > 1 ? currentCount.ToString() : "";
        }

        public bool IsEmpty => currentItem == null;

        public ItemData GetItem() => currentItem;

        public void OnPointerClick(PointerEventData eventData) { }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (currentItem == null || iconImage.sprite == null || DragItemIcon.Instance == null)
                return;

            DragItemIcon.Instance.SetIcon(iconImage.sprite);
            DragItemIcon.Instance.transform.position = Input.mousePosition;
            DragItemIcon.Instance.Show();
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (currentItem == null || DragItemIcon.Instance == null) return;
            DragItemIcon.Instance.transform.position = Input.mousePosition;
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (currentItem == null || DragItemIcon.Instance == null) return;
            DragItemIcon.Instance.Hide();

            if (eventData.pointerEnter != null)
            {
                InventorySlotUI inventorySlot = eventData.pointerEnter.GetComponentInParent<InventorySlotUI>();
                if (inventorySlot != null)
                {
                    inventorySlot.SetItem(currentItem, currentCount);
                    ClearSlot();
                    return;
                }

                QuickSlotUI quickSlot = eventData.pointerEnter.GetComponentInParent<QuickSlotUI>();
                if (quickSlot != null && quickSlot != this)
                {
                    quickSlot.SetItem(currentItem, currentCount);
                    ClearSlot();
                    return;
                }
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (currentItem != null && TooltipPanel.Instance != null)
                TooltipPanel.Instance.ShowTooltip(currentItem);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            TooltipPanel.Instance?.HideTooltip();
        }
    }
}