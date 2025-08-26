using UnityEngine;
using UnityEngine.EventSystems;
using Nytherion.UI.Inventory;
using Nytherion.UI.Components;
using Nytherion.Data.ScriptableObjects.Items;
using System;

namespace Nytherion.Core.Utils
{
    /// <summary>
    /// UI 슬롯 컴포넌트들의 공통 기능을 표준화하고 팩토리 메서드를 제공합니다.
    /// 드래그&드롭, 툴팁, 이벤트 핸들링 등의 중복 코드를 제거합니다.
    /// </summary>
    public static class UISlotFactory
    {
        /// <summary>
        /// 슬롯의 기본 드래그&드롭 기능을 설정합니다.
        /// </summary>
        /// <param name="slot">설정할 슬롯</param>
        /// <param name="enableDrag">드래그 기능 활성화 여부</param>
        /// <param name="enableDrop">드롭 기능 활성화 여부</param>
        public static void SetupDragDrop(BaseSlotUI slot, bool enableDrag = true, bool enableDrop = true)
        {
            if (slot == null)
            {
                Debug.LogWarning("[UISlotFactory] Slot is null. Cannot setup drag and drop.");
                return;
            }

            if (enableDrag)
            {
                slot.OnBeginDragEvent += CommonDragHandler.HandleBeginDrag;
                slot.OnEndDragEvent += CommonDragHandler.HandleEndDrag;
            }

            // 드롭 기능은 개별 슬롯 타입에서 IDropHandler 인터페이스로 구현되므로 여기서는 설정하지 않음
        }

        /// <summary>
        /// 슬롯의 툴팁 기능을 설정합니다.
        /// </summary>
        /// <param name="slot">설정할 슬롯</param>
        /// <param name="enableTooltip">툴팁 기능 활성화 여부</param>
        public static void SetupTooltip(BaseSlotUI slot, bool enableTooltip = true)
        {
            if (slot == null)
            {
                Debug.LogWarning("[UISlotFactory] Slot is null. Cannot setup tooltip.");
                return;
            }

            if (enableTooltip)
            {
                slot.OnPointerEnterEvent += CommonTooltipHandler.HandlePointerEnter;
                slot.OnPointerExitEvent += CommonTooltipHandler.HandlePointerExit;
            }
        }

        /// <summary>
        /// 슬롯의 기본 이벤트 핸들링을 설정합니다.
        /// </summary>
        /// <param name="slot">설정할 슬롯</param>
        /// <param name="onLeftClick">좌클릭 이벤트 핸들러</param>
        /// <param name="onRightClick">우클릭 이벤트 핸들러</param>
        public static void SetupClickEvents(BaseSlotUI slot, 
            Action<BaseSlotUI, PointerEventData> onLeftClick = null,
            Action<BaseSlotUI, PointerEventData> onRightClick = null)
        {
            if (slot == null)
            {
                Debug.LogWarning("[UISlotFactory] Slot is null. Cannot setup click events.");
                return;
            }

            slot.OnPointerClickEvent += (slotUI, eventData) =>
            {
                switch (eventData.button)
                {
                    case PointerEventData.InputButton.Left:
                        onLeftClick?.Invoke(slotUI, eventData);
                        break;
                    case PointerEventData.InputButton.Right:
                        onRightClick?.Invoke(slotUI, eventData);
                        break;
                }
            };
        }

        /// <summary>
        /// 슬롯에 완전한 기본 설정을 적용합니다.
        /// </summary>
        /// <param name="slot">설정할 슬롯</param>
        /// <param name="config">슬롯 설정 옵션</param>
        public static void SetupSlot(BaseSlotUI slot, SlotConfig config = null)
        {
            if (slot == null)
            {
                Debug.LogWarning("[UISlotFactory] Slot is null. Cannot setup slot.");
                return;
            }

            config ??= SlotConfig.Default;

            SetupDragDrop(slot, config.EnableDrag, config.EnableDrop);
            SetupTooltip(slot, config.EnableTooltip);
            SetupClickEvents(slot, config.OnLeftClick, config.OnRightClick);

            Debug.Log($"[UISlotFactory] Successfully configured slot: {slot.name}");
        }

        /// <summary>
        /// 여러 슬롯에 동일한 설정을 일괄 적용합니다.
        /// </summary>
        /// <param name="slots">설정할 슬롯 배열</param>
        /// <param name="config">슬롯 설정 옵션</param>
        public static void SetupSlots(BaseSlotUI[] slots, SlotConfig config = null)
        {
            if (slots == null || slots.Length == 0)
            {
                Debug.LogWarning("[UISlotFactory] Slots array is null or empty.");
                return;
            }

            foreach (var slot in slots)
            {
                SetupSlot(slot, config);
            }

            Debug.Log($"[UISlotFactory] Successfully configured {slots.Length} slots.");
        }
    }

    /// <summary>
    /// 공통 드래그 핸들러 클래스
    /// </summary>
    public static class CommonDragHandler
    {
        public static void HandleBeginDrag(BaseSlotUI slot, PointerEventData eventData)
        {
            if (slot == null || slot.IsEmpty) return;

            if (DragItemIcon.Instance != null)
            {
                DragItemIcon.Instance.SetIcon(slot.CurrentItem.icon);
                DragItemIcon.Instance.Show();
                DragItemIcon.Instance.transform.position = Input.mousePosition;
            }
        }

        public static void HandleEndDrag(BaseSlotUI slot, PointerEventData eventData)
        {
            if (DragItemIcon.Instance != null)
            {
                DragItemIcon.Instance.Hide();
            }
        }
    }

    /// <summary>
    /// 공통 툴팁 핸들러 클래스
    /// </summary>
    public static class CommonTooltipHandler
    {
        public static void HandlePointerEnter(BaseSlotUI slot, PointerEventData eventData)
        {
            if (slot?.CurrentItem != null && TooltipPanel.Instance != null)
            {
                TooltipPanel.Instance.ShowTooltip(slot.CurrentItem);
            }
        }

        public static void HandlePointerExit(BaseSlotUI slot, PointerEventData eventData)
        {
            TooltipPanel.Instance?.HideTooltip();
        }
    }

    /// <summary>
    /// 슬롯 설정 옵션 클래스
    /// </summary>
    public class SlotConfig
    {
        public bool EnableDrag { get; set; } = true;
        public bool EnableDrop { get; set; } = true;
        public bool EnableTooltip { get; set; } = true;
        public Action<BaseSlotUI, PointerEventData> OnLeftClick { get; set; }
        public Action<BaseSlotUI, PointerEventData> OnRightClick { get; set; }

        /// <summary>
        /// 기본 설정
        /// </summary>
        public static SlotConfig Default => new SlotConfig();

        /// <summary>
        /// 읽기 전용 슬롯 설정 (드래그&드롭 비활성화)
        /// </summary>
        public static SlotConfig ReadOnly => new SlotConfig
        {
            EnableDrag = false,
            EnableDrop = false,
            EnableTooltip = true
        };

        /// <summary>
        /// 상호작용 없는 디스플레이 전용 슬롯 설정
        /// </summary>
        public static SlotConfig DisplayOnly => new SlotConfig
        {
            EnableDrag = false,
            EnableDrop = false,
            EnableTooltip = false
        };

        /// <summary>
        /// 인벤토리 슬롯 기본 설정
        /// </summary>
        public static SlotConfig Inventory => new SlotConfig
        {
            EnableDrag = true,
            EnableDrop = true,
            EnableTooltip = true,
            OnRightClick = (slot, eventData) =>
            {
                // 기본 우클릭 동작: 아이템 사용
                if (!slot.IsEmpty && slot.CurrentItem is ConsumableData)
                {
                    Debug.Log($"Using item: {slot.CurrentItem.itemName}");
                }
            }
        };

        /// <summary>
        /// 상점 슬롯 기본 설정
        /// </summary>
        public static SlotConfig Shop => new SlotConfig
        {
            EnableDrag = false,
            EnableDrop = false,
            EnableTooltip = true,
            OnLeftClick = (slot, eventData) =>
            {
                // 기본 좌클릭 동작: 구매
                Debug.Log($"Attempting to buy: {slot.CurrentItem?.itemName}");
            }
        };
    }
}