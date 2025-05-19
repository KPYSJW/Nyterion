using UnityEngine;
using UnityEngine.EventSystems;
using Nytherion.Data.ScriptableObjects.Items;
using Nytherion.Core;
using System;

namespace Nytherion.UI.Inventory
{
    public class InventorySlotUI : BaseSlotUI
    {
        public int SlotIndex { get; private set; }

        protected override void Awake()
        {
            base.Awake();
            // 드래그 앤 드롭 이벤트 설정
            OnBeginDragEvent += HandleBeginDrag;
            OnEndDragEvent += HandleEndDrag;
            OnPointerClickEvent += HandlePointerClick;
        }

        public void Initialize(int index)
        {
            SlotIndex = index;
            ClearSlot();
        }

        private void HandleBeginDrag(BaseSlotUI slot, PointerEventData eventData)
        {
            if (IsEmpty) return;
            // 드래그 시작 시 아이콘 표시
            if (DragItemIcon.Instance != null)
            {
                DragItemIcon.Instance.SetIcon(iconImage.sprite);
                DragItemIcon.Instance.Show();
            }
        }

        protected override void HandleEndDrag(BaseSlotUI slot, PointerEventData eventData)
        {
            if (DragItemIcon.Instance != null)
                DragItemIcon.Instance.Hide();

            if (IsEmpty) return;

            // 드래그가 끝난 위치에서 Raycast 수행
            var pointerData = new PointerEventData(UnityEngine.EventSystems.EventSystem.current)
            {
                position = eventData.position
            };

            var results = new System.Collections.Generic.List<RaycastResult>();
            UnityEngine.EventSystems.EventSystem.current.RaycastAll(pointerData, results);

            bool wasDroppedOnSlot = false;

            foreach (var result in results)
            {
                // 인벤토리 슬롯 또는 퀵슬롯 찾기
                var targetSlot = result.gameObject.GetComponentInParent<BaseSlotUI>();
                if (targetSlot == null || targetSlot == this) continue;

                // SlotTransferHelper를 사용하여 아이템 이동/교환 시도
                if (targetSlot is InventorySlotUI || targetSlot is QuickSlotUI)
                {
                    if (Nytherion.Utils.SlotTransferHelper.TryTransferItem(this, targetSlot))
                    {
                        Debug.Log($"[InventorySlotUI] Successfully transferred item to {targetSlot.GetType().Name}");
                        wasDroppedOnSlot = true;
                        break;
                    }
                    else if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
                    {
                        // Shift 키를 누른 경우 아이템 교환 시도
                        Nytherion.Utils.SlotTransferHelper.TrySwapItems(this, targetSlot);
                        wasDroppedOnSlot = true;
                        break;
                    }
                }
            }

            // 아무 슬롯에도 드롭되지 않았거나 이동/교환이 실패한 경우
            if (!wasDroppedOnSlot)
            {
                // 아이템을 원래 슬롯에 다시 설정 (시각적 피드백)
                SetItem(currentItem, currentCount);
            }
        }

        private void HandlePointerClick(BaseSlotUI slot, PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Right && !IsEmpty)
            {
                // 아이템 사용 또는 컨텍스트 메뉴 표시
                Debug.Log($"[Inventory] Used item: {currentItem.itemName}");
                // 아이템 사용 로직 (필요시 구현)
            }
        }


        public override void ClearSlot()
        {
            base.ClearSlot();
            // 슬롯이 비워질 때 추가 정리 작업이 필요하면 여기에 구현
        }
    }
}