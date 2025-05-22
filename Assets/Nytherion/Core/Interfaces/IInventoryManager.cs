using System;
using Nytherion.Data.ScriptableObjects.Items;
using Nytherion.UI.Inventory;

namespace Nytherion.Core.Interfaces
{
    public interface IInventoryManager
    {
        // 아이템 추가/제거
        bool AddItem(ItemData item);
        bool AddItem(ItemData item, int count);
        bool RemoveItem(ItemData item);
        bool RemoveItem(ItemData item, int count);
        void SwapItems(InventorySlotUI fromSlot, InventorySlotUI toSlot);
        
        // 인벤토리 상태 조회
        bool HasItem(ItemData item);
        bool HasItem(string itemId);
        int GetItemCount(ItemData item);
        bool IsFull { get; }
        
        // 인벤토리 관리
        void ClearInventory();
        
        // 이벤트
        event Action<ItemData, int> OnItemAdded;    // 아이템 추가 이벤트 (아이템, 추가된 수량)
        event Action<ItemData, int> OnItemRemoved;  // 아이템 제거 이벤트 (아이템, 제거된 수량)
        event Action OnInventoryUpdated;            // 인벤토리 갱신 이벤트
    }
}