using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using Nytherion.UI.Inventory; // For BaseSlotUI, DragItemIcon
using Nytherion.Utils;       // For SlotTransferHelper

namespace Nytherion.UI.Inventory.Utils
{
    public static class DragDropUIHandler
    {
        public static void HandleBeginDragShared(BaseSlotUI slotBeingDragged)
        {
            if (slotBeingDragged == null || slotBeingDragged.IsEmpty) return;

            if (DragItemIcon.Instance != null && slotBeingDragged.Item != null && slotBeingDragged.Item.icon != null)
            {
                DragItemIcon.Instance.SetIcon(slotBeingDragged.Item.icon); // Use Item.icon directly
                DragItemIcon.Instance.Show();
            }
            else
            {
                if (DragItemIcon.Instance == null) Debug.LogError("[DragDropUIHandler] DragItemIcon.Instance is null.");
                if (slotBeingDragged.Item == null) Debug.LogWarning($"[DragDropUIHandler] Item in slot {slotBeingDragged.name} is null.");
                else if (slotBeingDragged.Item.icon == null) Debug.LogWarning($"[DragDropUIHandler] Icon for item {slotBeingDragged.Item.itemName} is null.");
            }
        }

        public static void HandleEndDragShared(BaseSlotUI sourceSlot, PointerEventData eventData, bool allowShiftSwap = true)
        {
            if (DragItemIcon.Instance != null)
                DragItemIcon.Instance.Hide();

            if (sourceSlot == null || sourceSlot.IsEmpty) return;

            List<RaycastResult> results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(eventData, results); // eventData already has position

            BaseSlotUI targetSlot = null;
            foreach (var result in results)
            {
                var foundSlot = result.gameObject.GetComponentInParent<BaseSlotUI>();
                if (foundSlot != null && foundSlot != sourceSlot)
                {
                    targetSlot = foundSlot;
                    break; // Found the first valid slot under the cursor
                }
            }

            if (targetSlot != null)
            {
                bool transferred = SlotTransferHelper.TryTransferItem(sourceSlot, targetSlot);
                if (!transferred && allowShiftSwap && (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)))
                {
                    SlotTransferHelper.TrySwapItems(sourceSlot, targetSlot);
                }
                // If neither transfer nor swap happened, the item visually snaps back
                // because its data in sourceSlot was never actually changed.
            }
            // else: No valid target slot found, item remains in sourceSlot. Visuals should be fine.
        }
    }
}
