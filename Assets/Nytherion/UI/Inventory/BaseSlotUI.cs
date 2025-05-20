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

        public virtual void SetItem(ItemData item, int count, Action<ItemData, int> onUseCallback)
        {
            if (isSettingItem) return;
            isSettingItem = true;

            try
            {
                currentItem = item;
                currentCount = count;

                if (item == null)
                {
                    iconImage.enabled = false;
                    iconImage.sprite = null;
                    countText.text = "";
                    OnItemCleared?.Invoke();
                    return;
                }

                iconImage.sprite = item.icon;
                iconImage.enabled = true;
                countText.text = item.isStackable && count > 1 ? count.ToString() : "";
                
                OnItemSet?.Invoke(item, count);
                OnSlotUpdated?.Invoke(this);
            }
            finally
            {
                isSettingItem = false;
            }
        }

        public virtual void SetItem(ItemData item, int count = 1)
        {
            SetItem(item, count, null);
        }

        public virtual void ClearSlot()
        {
            SetItem(null);
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
            currentCount += amount;
            UpdateCountText();
        }

        public virtual void DecreaseCount(int amount = 1)
        {
            currentCount -= amount;
            if (currentCount <= 0)
                ClearSlot();
            else
                UpdateCountText();
        }

        protected virtual void UpdateCountText()
        {
            if (currentItem == null)
                countText.text = "";
            else
                countText.text = currentItem.isStackable && currentCount > 1 ? currentCount.ToString() : "";
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
            if (IsEmpty) return;
            if (DragItemIcon.Instance != null)
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