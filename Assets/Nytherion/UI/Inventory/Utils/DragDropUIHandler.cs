using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;

namespace Nytherion.UI.Inventory.Utils
{
    public static class DragDropUIHandler
    {
        public static bool dropHandled = false;
        public static void HandleBeginDragShared(BaseSlotUI slotBeingDragged)
        {
            dropHandled = false;
            if (slotBeingDragged == null || slotBeingDragged.IsEmpty) return;

            if (DragItemIcon.Instance != null && slotBeingDragged.CurrentItem != null && slotBeingDragged.CurrentItem.icon != null)
            {
                DragItemIcon.Instance.SetIcon(slotBeingDragged.CurrentItem.icon);

                // 드래그 아이콘의 크기를 원본 슬롯 내부의 실제 아이콘(Image) 크기에 정확히 맞춤
                if (slotBeingDragged.IconImage != null)
                {
                    RectTransform iconRect = slotBeingDragged.IconImage.rectTransform;
                    DragItemIcon.Instance.iconImage.rectTransform.sizeDelta = iconRect.rect.size;
                }
                else
                {
                    RectTransform sourceRect = slotBeingDragged.GetComponent<RectTransform>();
                    if (sourceRect != null)
                    {
                        DragItemIcon.Instance.iconImage.rectTransform.sizeDelta = sourceRect.rect.size;
                    }
                }

                DragItemIcon.Instance.Show();

                // 원본 슬롯 아이콘을 투명하게 처리 (알파값 0)
                slotBeingDragged.SetDragVisibility(false);
            }
            else
            {
                if (DragItemIcon.Instance == null) Debug.LogError("[DragDropUIHandler] DragItemIcon.Instance is null.");
                if (slotBeingDragged.CurrentItem == null) Debug.LogWarning($"[DragDropUIHandler] Item in slot {slotBeingDragged.name} is null.");
                else if (slotBeingDragged.CurrentItem.icon == null) Debug.LogWarning($"[DragDropUIHandler] Icon for item {slotBeingDragged.CurrentItem.itemName} is null.");
            }
        }

        public static void HandleEndDragShared(BaseSlotUI sourceSlot, PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;

            if (DragItemIcon.Instance != null)
                DragItemIcon.Instance.Hide();

            if (sourceSlot != null)
            {
                // 드래그가 끝나면 무조건 원본 아이콘 복구 (알파값 1)
                sourceSlot.SetDragVisibility(true);
            }

            if (dropHandled) return;

            if (sourceSlot == null || sourceSlot.IsEmpty) return;

            var results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(eventData, results);

            BaseSlotUI targetSlot = null;
            foreach (var result in results)
            {
                var slot = result.gameObject.GetComponentInParent<BaseSlotUI>();
                if (slot != null)
                {
                    targetSlot = slot;
                    break;
                }
            }

            if (targetSlot != null)
            {
                SlotTransferHelper.TransferItem(sourceSlot, targetSlot);
            }
            else
            {
                sourceSlot.SetItem(sourceSlot.CurrentItem, sourceSlot.CurrentCount);
                SlotTransferHelper.HandleDropOnEmptySpace(sourceSlot, eventData);
            }
        }
    }
}
