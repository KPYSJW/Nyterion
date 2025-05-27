using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI; // LayoutRebuilder 사용을 위해 추가
using TMPro;
using Nytherion.Data.ScriptableObjects.Items;
using Nytherion.Core;
using System;
using EventSystem = UnityEngine.EventSystems.EventSystem; // 모호성 해결을 위한 별칭

namespace Nytherion.UI.Inventory
{
    public class QuickSlotUI : BaseSlotUI
    {
        public event Action<ItemData, int> OnItemUsed;
        [SerializeField] private TMPro.TextMeshProUGUI keyLabelText;
        private Action<ItemData, int> onItemUsed;

        protected override void Awake()
        {
            base.Awake();
            // 디버그 로그 추가
            if (keyLabelText == null)
            {
                Debug.LogError("keyLabelText is not assigned in the inspector!", this);
            }
            else
            {
                // 인스펙터에서 설정한 텍스트를 유지하기 위해 여기서는 활성화만 처리
                keyLabelText.gameObject.SetActive(true);
                Debug.Log($"QuickSlotUI Awake - KeyLabelText: '{keyLabelText.text}'", this);
            }

            // 드래그 이벤트 핸들러 등록
            // OnBeginDragEvent += HandleBeginDrag; // Remove old
            // OnEndDragEvent += HandleEndDrag;   // Remove old
            OnBeginDragEvent += (slot, eventData) => Nytherion.UI.Inventory.Utils.DragDropUIHandler.HandleBeginDragShared(slot);
            OnEndDragEvent += (slot, eventData) => Nytherion.UI.Inventory.Utils.DragDropUIHandler.HandleEndDragShared(slot, eventData); // allowShiftSwap defaults to true
        }

        // Removed HandleBeginDrag method

        public void SetKeyLabel(string label)
        {
            if (keyLabelText != null)
            {
                keyLabelText.text = label;
            }
        }

        public override void SetItem(ItemData item, int count, Action<ItemData, int> onUseCallback = null)
        {
            // isSettingItem is managed by BaseSlotUI.SetItem
            // if (isSettingItem) 
            // {
            //     Debug.Log($"[QuickSlotUI] Base class should handle re-entrancy. If this logs, review BaseSlotUI.");
            //     return;
            // }

            Debug.Log($"[QuickSlotUI] SetItem called - Item: {item?.itemName ?? "null"}, Count: {count}");

            // QuickSlot specific logic for callback
            if (onUseCallback != null)
            {
                this.onItemUsed = onUseCallback;
                // Debug.Log($"[QuickSlotUI] Callback set for {item?.itemName}");
            }
            else if (item == null) // If clearing the slot (item is null), clear the callback
            {
                this.onItemUsed = null;
                // Debug.Log($"[QuickSlotUI] Item is null, callback cleared.");
            }
            // If item is not null but onUseCallback is null, retain the existing onItemUsed.

            // Call base method to set data and update visuals
            // The base.SetItem will handle setting this.currentItem, this.currentCount,
            // calling UpdateVisuals, and invoking OnItemSet/OnItemCleared and OnSlotUpdated.
            base.SetItem(item, count, onUseCallback);

            // Duplicated UI update logic for iconImage and countText is now removed
            // as it's handled by BaseSlotUI.UpdateVisuals() called from base.SetItem().

            // QuickSlot specific UI updates (if any, beyond what BaseSlotUI.UpdateVisuals does)
            // e.g., keyLabelText update if it depended on item data, but SetKeyLabel is separate.

            // Layout rebuild if still necessary for QuickSlotUI
            // Ensure the GameObject is active in the hierarchy before forcing a layout rebuild.
            if (gameObject.activeInHierarchy && transform as RectTransform != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(transform as RectTransform);
            }

            // Debug log for confirmation
            if (this.currentItem != null) // Use this.currentItem as it's set by base.SetItem
            {
                Debug.Log($"[QuickSlotUI] SetItem completed: {this.currentItem.itemName} x{this.currentCount}");
            }
            else
            {
                Debug.Log("[QuickSlotUI] Cleared slot (item is null after base call)");
            }
        }

        public override void UseItem()
        {
            if (IsEmpty) return;

            // 인벤토리에서 아이템 제거 시도
            if (InventoryManager.Instance != null && InventoryManager.Instance.RemoveItem(currentItem, 1))
            {
                // 인벤토리에서 아이템 제거 성공 시에만 퀵슬롯에서도 감소
                currentCount--;

                if (currentCount <= 0)
                {
                    // 수량이 0 이하가 되면 슬롯 클리어
                    ClearSlot();
                }
                else
                {
                    // 수량만 업데이트
                    SetItem(currentItem, currentCount);
                }

                // 인벤토리 UI 갱신
                InventoryManager.Instance.RequestSlotsUpdate();

                // 아이템 사용 이벤트 호출
                OnItemUsed?.Invoke(currentItem, 1);
            }
            else
            {
                // 인벤토리에 아이템이 없는 경우 슬롯 클리어
                Debug.Log("인벤토리에 해당 아이템이 없어 퀵슬롯에서 제거합니다.");
                ClearSlot();
            }
        }

        // Removed HandleEndDrag method

        public override void ClearSlot()
        {
            // QuickSlot specific cleanup
            this.onItemUsed = null;
            // Debug.Log("[QuickSlotUI] onItemUsed callback cleared.");

            // Call base method. This will call SetItem(null, 0, null) in BaseSlotUI,
            // which in turn calls UpdateVisuals and handles relevant events.
            base.ClearSlot();

            // Debug.Log("[QuickSlotUI] Cleared slot via base.ClearSlot()");
        }
    }
}
