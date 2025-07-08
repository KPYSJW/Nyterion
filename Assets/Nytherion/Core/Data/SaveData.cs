using System;
using System.Collections.Generic;
using Nytherion.Core.Managers;
using Nytherion.Core.Enums;

namespace Nytherion.Core.Data
{
    [Serializable]
    public class ShopStockState
    {
        public string shopItemId;
        public int remainingStock;
    }

    [Serializable]
    public class SaveData
    {
        public List<CurrencyType> currencyTypes = new List<CurrencyType>();
        public List<int> currencyAmounts = new List<int>();

        public List<ItemEntry> inventoryData = new List<ItemEntry>();

        public EngravingGridState engravingData;
        public List<ShopStockState> shopStockData = new List<ShopStockState>();

        public List<string> quickSlotItemIDs = new List<string>();
        public List<int> quickSlotItemCounts = new List<int>();
        public List<EquippedItemEntry> equippedItemsData = new List<EquippedItemEntry>();

        public SaveData()
        {
            currencyTypes = new List<CurrencyType>();
            currencyAmounts = new List<int>();
            inventoryData = new List<ItemEntry>();
            engravingData = new EngravingGridState();
            shopStockData = new List<ShopStockState>();
            quickSlotItemIDs = new List<string>();
            quickSlotItemCounts = new List<int>();
        }
    }
    [Serializable]
    public class EquippedItemEntry
    {
        public EquipmentSlotType slotType;
        public string instanceId;
    }
    [Serializable]
    public class ItemEntry
    {
        public string ItemId;
        public int Count;
        public string InstanceId;
    }

}