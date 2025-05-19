using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using Nytherion.Data.ScriptableObjects.Items;
using Nytherion.Core;

namespace Nytherion.UI.Inventory
{
    public class InventorySlotUI : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private Image iconImage;
        [SerializeField] private TextMeshProUGUI countText;
        public ItemData Item { get; private set; }
        public int StackCount { get; private set; }

        private void Start()
        {
            ClearSlot();
        }

        public void SetItem(ItemData newItem, int count = 1)
        {
            Item = newItem;
            StackCount = count;
            iconImage.sprite = newItem.icon;
            iconImage.enabled = true;
            countText.text = newItem.isStackable && count > 1 ? count.ToString() : "";

            Debug.Log($"[InventorySlotUI] SetItem: {newItem.itemName} x{count}");
        }

        public void ClearSlot()
        {
            Item = null;
            StackCount = 0;
            iconImage.sprite = null;
            iconImage.enabled = false;
            countText.text = "";
        }

        public bool HasItem(ItemData target) => Item == target;

        public bool IsEmpty => Item == null;

        public void IncreaseCount(int amount = 1)
        {
            StackCount += amount;
            countText.text = Item.isStackable && StackCount > 1 ? StackCount.ToString() : "";
        }

        public void DecreaseCount(int amount = 1)
        {
            StackCount -= amount;
            if (StackCount <= 0) ClearSlot();
            else countText.text = Item.isStackable && StackCount > 1 ? StackCount.ToString() : "";
        }

        public void OnPointerClick(PointerEventData eventData) { }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (Item == null || iconImage.sprite == null || DragItemIcon.Instance == null)
                return;

            DragItemIcon.Instance.SetIcon(iconImage.sprite);
            DragItemIcon.Instance.transform.position = Input.mousePosition;
            DragItemIcon.Instance.Show();
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (Item == null || DragItemIcon.Instance == null) return;
            DragItemIcon.Instance.transform.position = Input.mousePosition;
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (Item == null || DragItemIcon.Instance == null) return;
            DragItemIcon.Instance.Hide();

            if (eventData.pointerEnter != null)
            {
                var targetSlot = eventData.pointerEnter.GetComponentInParent<InventorySlotUI>();
                if (targetSlot != null && targetSlot != this)
                {
                    InventoryManager.Instance.SwapItems(this, targetSlot);
                    return;
                }

                var quickSlot = eventData.pointerEnter.GetComponentInParent<QuickSlotUI>();
                if (quickSlot != null)
                {
                    quickSlot.SetItem(Item, StackCount);
                    InventoryManager.Instance.RemoveItem(Item);
                    ClearSlot();
                    return;
                }
            }
        }


        public void OnPointerEnter(PointerEventData eventData)
        {
            if (Item != null && TooltipPanel.Instance != null)
                TooltipPanel.Instance.ShowTooltip(Item);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            TooltipPanel.Instance?.HideTooltip();
        }
    }
}