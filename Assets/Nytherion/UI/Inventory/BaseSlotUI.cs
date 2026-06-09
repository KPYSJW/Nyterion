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
        public event SlotEventDelegate OnPointerEnterEvent;
        public event SlotEventDelegate OnPointerExitEvent;
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
        public Image IconImage => iconImage;
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

            if (iconImage != null)
            {
                iconImage.enabled = false;
                iconImage.sprite = null;
            }
            if (countText != null)
            {
                countText.text = "";
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

            bool hasItem = item != null && count > 0;

            iconImage.enabled = hasItem;

            if (hasItem)
            {
                iconImage.sprite = item.icon;
                
                // 알파값을 항상 1로 복구하여 투명화 버그 방지
                Color color = iconImage.color;
                color.a = 1f;
                iconImage.color = color;

                if (countText != null)
                {
                    countText.text = item.isStackable && count > 1 ? count.ToString() : "";
                }
            }
            else
            {
                iconImage.sprite = null;
                if (countText != null)
                {
                    countText.text = "";
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
            if (eventData.button != PointerEventData.InputButton.Left) return;
            if (IsEmpty) return;
            OnBeginDragEvent?.Invoke(this, eventData);
        }

        public virtual void OnDrag(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            // 위치 업데이트는 DragItemIcon.Update에서 전담하므로 여기서는 아무것도 하지 않음
        }

        public virtual void OnEndDrag(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            OnEndDragEvent?.Invoke(this, eventData);
        }

        /// <summary>
        /// 드래그 도중 원본 슬롯의 아이콘을 투명하게 하거나 원래대로 복구합니다.
        /// </summary>
        public void SetDragVisibility(bool isVisible)
        {
            if (iconImage != null && currentItem != null)
            {
                // 완전히 끄는 대신 알파값만 조절하여 로직 충돌 방지
                Color color = iconImage.color;
                color.a = isVisible ? 1f : 0f;
                iconImage.color = color;
            }
        }

        public virtual void OnPointerEnter(PointerEventData eventData)
        {
            OnPointerEnterEvent?.Invoke(this, eventData);
            
            // 기본 툴팁 동작 (이벤트 핸들러가 없을 때의 폴백)
            if (OnPointerEnterEvent == null && currentItem != null && TooltipPanel.Instance != null)
                TooltipPanel.Instance.ShowTooltip(currentItem);
        }

        public virtual void OnPointerExit(PointerEventData eventData)
        {
            OnPointerExitEvent?.Invoke(this, eventData);
            
            // 기본 툴팁 숨김 동작 (이벤트 핸들러가 없을 때의 폴백)
            if (OnPointerExitEvent == null)
                TooltipPanel.Instance?.HideTooltip();
        }
    }
}