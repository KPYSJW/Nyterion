using UnityEngine;
using UnityEngine.EventSystems;
using Nytherion.Data.ScriptableObjects.Items;

namespace Nytherion.UI.Inventory.Utils
{
    public static class SlotTransferHelper
    {
        public static bool CanTransferItem(BaseSlotUI source, BaseSlotUI target)
        {
            if (source == null || target == null || source.IsEmpty)
                return false;
                
            // 타겟 슬롯이 아이템을 받을 수 있는지 확인
            if (!target.CanReceiveItem(source.CurrentItem))
                return false;
                
            // 소스 슬롯이 장비 슬롯이고 타겟이 장비 슬롯인 경우
            if (source is EquipmentSlotUI sourceEquipment && target is EquipmentSlotUI targetEquipment)
            {
                // 같은 타입의 장비 슬롯 간에만 이동 가능
                return sourceEquipment.SlotType == targetEquipment.SlotType;
            }
            
            // 소스가 장비 슬롯이고 타겟이 인벤토리 슬롯인 경우
            if (source is EquipmentSlotUI && target is InventorySlotUI)
            {
                // 항상 이동 가능
                return true;
            }
            
            // 소스가 인벤토리 슬롯이고 타겟이 장비 슬롯인 경우
            if (source is InventorySlotUI && target is EquipmentSlotUI targetEquipSlot)
            {
                // 아이템 타입이 장비 슬롯 타입과 일치해야 함
                return source.CurrentItem.itemType == targetEquipSlot.SlotType;
            }
            
            // 퀵슬롯 관련 검사 제거 - 모든 아이템을 퀵슬롯에 등록 가능
            
            // 그 외의 경우 (인벤토리 간 이동 등)
            return true;
        }
        
        public static void TransferItem(BaseSlotUI source, BaseSlotUI target)
        {
            if (!CanTransferItem(source, target) || source.IsEmpty)
                return;

            // 타겟 슬롯이 비어있는 경우
            if (target.IsEmpty)
            {
                // 타겟 슬롯에 아이템 설정
                target.SetItem(source.CurrentItem, source.CurrentCount);
                source.ClearSlot();
            }
            // 타겟 슬롯에 아이템이 있는 경우 (스왑)
            else
            {
                // 아이템이 같은 경우는 처리하지 않음
                if (source.CurrentItem == target.CurrentItem)
                    return;
                    
                // 아이템 스왑
                var tempItem = target.CurrentItem;
                var tempCount = target.CurrentCount;
                
                target.SetItem(source.CurrentItem, source.CurrentCount);
                source.SetItem(tempItem, tempCount);
            }
        }
        
        public static void HandleDropOnEmptySpace(BaseSlotUI source, PointerEventData eventData)
        {
            if (source == null || source.IsEmpty)
                return;
                
            // Shift + 드롭: 아이템 버리기
            if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
            {
                // TODO: 아이템 버리기 로직 구현 (필요시)
                Debug.Log($"[SlotTransfer] 아이템 버림: {source.CurrentItem.itemName}");
                source.ClearSlot();
            }
        }
    }
}
