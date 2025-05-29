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
        
        // 현재 아이템과 개수에 대한 프로퍼티
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
            // 컴포넌트 초기화
            if (iconImage == null) 
            {
                iconImage = GetComponentInChildren<Image>();
                if (iconImage == null)
                {
                    Debug.LogError($"[{name}] Could not find Image component in children");
                }
            }
            
            // countText는 옵션이므로 찾지 못해도 경고만 표시
            if (countText == null) 
            {
                countText = GetComponentInChildren<TextMeshProUGUI>();
            }
        }

        protected bool isSettingItem = false;

        protected virtual void HandleEndDrag(BaseSlotUI slot, PointerEventData eventData)
        {
            if (DragItemIcon.Instance != null)
                DragItemIcon.Instance.Hide();
        }

        protected virtual void UpdateVisuals(ItemData item, int count)
        {
            if (iconImage == null)
            {
                Debug.LogError($"[{name}] iconImage is null. Cannot update visuals.");
                return;
            }

            if (item == null) 
            {
                iconImage.enabled = false;
                iconImage.sprite = null;
                if (countText != null) countText.text = "";
            }
            else
            {
                iconImage.sprite = item.icon;
                iconImage.enabled = true;
                
                // countText가 있는 경우에만 텍스트 업데이트
                if (countText != null)
                {
                    countText.text = item.isStackable && count > 1 ? count.ToString() : "";
                }

                RectTransform iconRect = iconImage.rectTransform;
                if (iconRect != null)
                {
                    float scale = 0.7f; 
                    iconRect.localScale = new Vector3(scale, scale, 1f);
                }
            }
        }

        public virtual void SetItem(ItemData item, int count, Action<ItemData, int> onUseCallback) 
        {
            if (isSettingItem) return; 
            isSettingItem = true;

            try
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
            SetItem(null, 0, null); 
        }

        public virtual void UseItem()
        {
        }

        public virtual void UseItem(Action<ItemData, int> onUseCallback)
        {
            if (currentItem != null && onUseCallback != null)
                onUseCallback(currentItem, currentCount);
        }

        public virtual ItemData GetItem() => currentItem;
        public virtual ItemData Item => currentItem;
        public virtual int StackCount => currentCount;
        public virtual bool HasItem(ItemData item) => currentItem == item;

        public virtual void IncreaseCount(int amount = 1)
        {
            if (IsEmpty) return; 
            currentCount += amount;
            UpdateVisuals(currentItem, currentCount);
            OnSlotUpdated?.Invoke(this);
        }

        public virtual void DecreaseCount(int amount = 1)
        {
            if (IsEmpty) return; 
            currentCount -= amount;
            if (currentCount <= 0)
            {
                ClearSlot(); 
            }
            else
            {
                UpdateVisuals(currentItem, currentCount);
            }
            if (currentCount > 0)
            {
                OnSlotUpdated?.Invoke(this);
            }
        }

        public virtual void OnPointerClick(PointerEventData eventData)
        {
            OnPointerClickEvent?.Invoke(this, eventData);
        }

        public virtual void OnBeginDrag(PointerEventData eventData)
        {
            Debug.Log($"[BaseSlotUI] OnBeginDrag: IsEmpty={IsEmpty}, Item={currentItem?.itemName ?? "null"}");
            OnBeginDragEvent?.Invoke(this, eventData);
        }

        public virtual void OnDrag(PointerEventData eventData)
        {
            if (IsEmpty || DragItemIcon.Instance == null) return; 
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