using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;
using Nytherion.Data.ScriptableObjects.Items;
using System;
using Nytherion.UI.Components;

namespace Nytherion.UI.Inventory
{
    public delegate void SlotEventDelegate(BaseSlotUI slot, PointerEventData eventData);
    public delegate void SlotItemEventDelegate(ItemData item, int count);

    public abstract class BaseSlotUI : MonoBehaviour,
        IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler,
        IPointerEnterHandler, IPointerExitHandler
    {
        public event SlotEventDelegate OnBeginDragEvent;
        public event SlotEventDelegate OnEndDragEvent;
        public event SlotEventDelegate OnPointerClickEvent;
        public event Action<BaseSlotUI> OnSlotUpdated;
        public event SlotItemEventDelegate OnItemSet;
        public event Action OnItemCleared;

        public ItemData CurrentItem => currentItem;
        public int CurrentCount => currentCount;
        public bool IsEmpty => currentItem == null || currentCount <= 0;

        public virtual bool CanReceiveItem(ItemData item)
        {
            return true;
        }

        [SerializeField] protected Image iconImage;
        [SerializeField] protected TextMeshProUGUI countText;
        protected ItemData currentItem;
        protected int currentCount;

        protected virtual void Awake()
        {
            if (iconImage == null)
            {
                iconImage = GetComponentInChildren<Image>();
            }

            if (countText == null)
            {
                countText = GetComponentInChildren<TextMeshProUGUI>();
            }
        }

        protected virtual void HandleEndDrag(BaseSlotUI slot, PointerEventData eventData)
        {
            if (DragItemIcon.Instance != null)
                DragItemIcon.Instance.Hide();
        }

        protected virtual void UpdateVisuals(ItemData item, int count)
        {
            if (iconImage == null) return;

            if (item == null || count <= 0)
            {
                iconImage.enabled = false;
                iconImage.sprite = null;
                if (countText != null) countText.text = "";
            }
            else
            {
                iconImage.sprite = item.icon;
                iconImage.enabled = true;
                if (countText != null)
                {
                    countText.text = item.isStackable && count > 1 ? count.ToString() : "";
                }
            }
        }

        public virtual void SetItem(ItemData item, int count)
        {
            SetItem(item, count, null);
        }

        public virtual void SetItem(ItemData item, int count, Action<ItemData, int> onUseCallback)
        {
            this.currentItem = item;
            this.currentCount = count;
            UpdateVisuals(this.currentItem, this.currentCount);

            if (this.currentItem == null)
            {
                OnItemCleared?.Invoke();
            }
            else
            {
                OnItemSet?.Invoke(this.currentItem, this.currentCount);
            }
            OnSlotUpdated?.Invoke(this);
        }

        public virtual void ClearSlot()
        {
            SetItem(null, 0, null);
        }

        public (ItemData item, int count) GetItemInfo()
        {
            return (currentItem, currentCount);
        }

        public virtual void UseItem()
        {
        }

        public virtual void OnPointerClick(PointerEventData eventData)
        {
            OnPointerClickEvent?.Invoke(this, eventData);
        }

        public virtual void OnBeginDrag(PointerEventData eventData)
        {
            if (IsEmpty) return;
            OnBeginDragEvent?.Invoke(this, eventData);
        }

        public virtual void OnDrag(PointerEventData eventData)
        {
            if (IsEmpty || DragItemIcon.Instance == null) return;
            DragItemIcon.Instance.transform.position = Input.mousePosition;
        }

        public virtual void OnEndDrag(PointerEventData eventData)
        {
            OnEndDragEvent?.Invoke(this, eventData);
        }

        public virtual void OnPointerEnter(PointerEventData eventData)
        {
            if (currentItem != null && TooltipPanel.Instance != null)
                TooltipPanel.Instance.ShowTooltip(currentItem);
        }

        public virtual void OnPointerExit(PointerEventData eventData)
        {
            TooltipPanel.Instance?.HideTooltip();
        }
    }
}