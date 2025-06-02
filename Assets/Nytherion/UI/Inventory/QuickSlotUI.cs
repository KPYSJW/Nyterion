using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using Nytherion.Data.ScriptableObjects.Items;
using Nytherion.Core;
using System;
using EventSystem = UnityEngine.EventSystems.EventSystem;
using Nytherion.UI.Inventory.Utils;

namespace Nytherion.UI.Inventory
{
    public class QuickSlotUI : BaseSlotUI
    {
        public event Action<ItemData, int> OnItemUsed;
        [SerializeField] private TMPro.TextMeshProUGUI keyLabelText;
        private Action<ItemData, int> onItemUsed;
        private IUsableItem usableItem; // 사용 가능한 아이템 참조

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
            // 기존 아이템 정리
            if (this.usableItem != null && this.usableItem is IDisposable disposable)
            {
                disposable.Dispose();
            }

            // 새 아이템 설정
            this.usableItem = item as IUsableItem;
            this.onItemUsed = onUseCallback;

            // 기본 슬롯 업데이트
            base.SetItem(item, count, (usedItem, usedCount) => 
            {
                // 아이템 사용 시 호출될 콜백
                if (this.usableItem != null)
                {
                    this.usableItem.Use();
                }
                onItemUsed?.Invoke(usedItem, usedCount);
            });

            // 레이아웃 갱신
            if (gameObject.activeInHierarchy && transform is RectTransform rectTransform)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(rectTransform);
            }

            Debug.Log($"[QuickSlot] {(item != null ? $"아이템 설정 완료: {item.itemName} x{count}" : "슬롯 비움")}");
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
