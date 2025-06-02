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
            // OnBeginDragEvent += HandleBeginDrag; // Remove old
            // OnEndDragEvent += HandleEndDrag;   // Remove old
            OnBeginDragEvent += (s, e) => Nytherion.UI.Inventory.Utils.DragDropUIHandler.HandleBeginDragShared(s);
            OnEndDragEvent += (s, e) => Nytherion.UI.Inventory.Utils.DragDropUIHandler.HandleEndDragShared(s, e);
            OnPointerClickEvent += HandlePointerClick;
        }

        public void Initialize(int index)
        {
            SlotIndex = index;
            ClearSlot();
        }

        // Removed HandleBeginDrag method
        // Removed HandleEndDrag method

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