using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;
using Nytherion.Data.ScriptableObjects.Items;
using Nytherion.Core;
using System;

namespace Nytherion.UI.Inventory
{
    // 슬롯 이벤트 델리게이트
    public delegate void SlotEventDelegate(BaseSlotUI slot, PointerEventData eventData);
    public delegate void SlotItemEventDelegate(ItemData item, int count);
    public abstract class BaseSlotUI : MonoBehaviour,
        IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler,
        IPointerEnterHandler, IPointerExitHandler
    {
        // 이벤트
        public event SlotEventDelegate OnBeginDragEvent;
        public event SlotEventDelegate OnEndDragEvent;
        public event SlotEventDelegate OnPointerClickEvent;
        public event Action<BaseSlotUI> OnSlotUpdated;

        // 아이템 관련 이벤트
        public event SlotItemEventDelegate OnItemSet;
        public event Action OnItemCleared;
        [SerializeField] protected Image iconImage;
        [SerializeField] protected TextMeshProUGUI countText;

        protected ItemData currentItem;
        protected int currentCount;

        protected virtual void Awake()
        {
            // 컴포넌트 초기화
            if (iconImage == null) iconImage = GetComponentInChildren<Image>();
            if (countText == null) countText = GetComponentInChildren<TextMeshProUGUI>();
        }

        protected bool isSettingItem = false;

        protected virtual void HandleEndDrag(BaseSlotUI slot, PointerEventData eventData)
        {
            // 기본 구현: 아무 동작하지 않음 (하위 클래스에서 오버라이드하여 사용)
            if (DragItemIcon.Instance != null)
                DragItemIcon.Instance.Hide();
        }

        protected virtual void UpdateVisuals(ItemData item, int count)
        {
            if (iconImage == null || countText == null)
            {
                Debug.LogError("[BaseSlotUI] iconImage or countText is null. Cannot update visuals.");
                return;
            }

            if (item == null) // Corresponds to clearing the slot
            {
                iconImage.enabled = false;
                iconImage.sprite = null;
                countText.text = "";
            }
            else
            {
                iconImage.sprite = item.icon;
                iconImage.enabled = true;
                countText.text = item.isStackable && count > 1 ? count.ToString() : "";
            }
        }
        
        public virtual void SetItem(ItemData item, int count, Action<ItemData, int> onUseCallback) // onUseCallback might be removed if not used by BaseSlotUI directly
        {
            if (isSettingItem) return; // Keep re-entrancy guard if necessary
            isSettingItem = true;

            try
            {
                this.currentItem = item;
                this.currentCount = count;

                UpdateVisuals(this.currentItem, this.currentCount); // Call the new method

                if (this.currentItem == null)
                {
                    OnItemCleared?.Invoke();
                }
                else
                {
                    OnItemSet?.Invoke(this.currentItem, this.currentCount);
                }
                OnSlotUpdated?.Invoke(this); // This event seems general for any update
            }
            finally
            {
                isSettingItem = false;
            }
        }

        public virtual void SetItem(ItemData item, int count = 1)
        {
            SetItem(item, count, null); // Pass null for the callback if BaseSlotUI doesn't use it
        }

        public virtual void ClearSlot()
        {
            SetItem(null, 0, null); // This will handle data, visuals, and events
        }
        
        public virtual void UseItem()
        {
            // Override in derived classes
        }
        
        public virtual void UseItem(Action<ItemData, int> onUseCallback)
        {
            if (currentItem != null && onUseCallback != null)
                onUseCallback(currentItem, currentCount);
        }

        public virtual ItemData GetItem() => currentItem;
        public virtual ItemData Item => currentItem;
        public virtual int StackCount => currentCount;
        public virtual bool IsEmpty => currentItem == null;
        public virtual bool HasItem(ItemData item) => currentItem == item;

        public virtual void IncreaseCount(int amount = 1)
        {
            if (IsEmpty) return; // Cannot increase count if no item
            currentCount += amount;
            UpdateVisuals(currentItem, currentCount);
            OnSlotUpdated?.Invoke(this);
        }

        public virtual void DecreaseCount(int amount = 1)
        {
            if (IsEmpty) return; // Cannot decrease if no item
            currentCount -= amount;
            if (currentCount <= 0)
            {
                ClearSlot(); // ClearSlot will call SetItem(null,0) which calls UpdateVisuals
            }
            else
            {
                UpdateVisuals(currentItem, currentCount);
            }
            // OnSlotUpdated is called by ClearSlot (via SetItem) or directly if not cleared.
            // If not cleared by ClearSlot, invoke it here.
            if (currentCount > 0) 
            {
                OnSlotUpdated?.Invoke(this);
            }
        }

        // UI 이벤트 핸들러
        public virtual void OnPointerClick(PointerEventData eventData)
        {
            OnPointerClickEvent?.Invoke(this, eventData);
        }

        public virtual void OnBeginDrag(PointerEventData eventData)
        {
            Debug.Log($"[BaseSlotUI] OnBeginDrag: IsEmpty={IsEmpty}, Item={currentItem?.itemName ?? "null"}");
            // 모든 드래그 시도 허용 (하위 클래스에서 처리)
            OnBeginDragEvent?.Invoke(this, eventData);
        }

        public virtual void OnDrag(PointerEventData eventData)
        {
            if (IsEmpty || DragItemIcon.Instance == null) return; // Added null check for Instance
            DragItemIcon.Instance.transform.position = Input.mousePosition;
        }

        public virtual void OnEndDrag(PointerEventData eventData)
        {
            if (IsEmpty) return;
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