using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using Nytherion.UI.Inventory;
using Nytherion.Data.ScriptableObjects.Items;

namespace Nytherion.UI.Inventory.Utils
{
    public static class DragDropUIHandler
    {
        public static void HandleBeginDragShared(BaseSlotUI slotBeingDragged)
        {
            if (slotBeingDragged == null || slotBeingDragged.IsEmpty) return;

            if (DragItemIcon.Instance != null && slotBeingDragged.CurrentItem != null && slotBeingDragged.CurrentItem.icon != null)
            {
                DragItemIcon.Instance.SetIcon(slotBeingDragged.CurrentItem.icon);
                DragItemIcon.Instance.Show();
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
            if (DragItemIcon.Instance != null)
                DragItemIcon.Instance.Hide();

            if (sourceSlot == null || sourceSlot.IsEmpty) return;

            // UI 요소들에 대한 레이캐스트 수행
            var results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(eventData, results);

            // 타겟 슬롯 찾기
            BaseSlotUI targetSlot = null;
            foreach (var result in results)
            {
                var slot = result.gameObject.GetComponentInParent<BaseSlotUI>();
                if (slot != null && slot != sourceSlot)
                {
                    targetSlot = slot;
                    break;
                }
            }

            if (targetSlot != null)
            {
                // 유효한 타겟 슬롯이 있는 경우 아이템 이동/교체 시도
                SlotTransferHelper.TransferItem(sourceSlot, targetSlot);
            }
            else
            {
                // UI 외부에 드롭된 경우 (아이템 버리기 등)
                SlotTransferHelper.HandleDropOnEmptySpace(sourceSlot, eventData);
            }
        }
    }
}
