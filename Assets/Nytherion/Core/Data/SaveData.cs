using System;
using System.Collections.Generic;
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
        public RelicGridState relicData;
        public List<ShopStockState> shopStockData = new List<ShopStockState>();
        public List<QuickSlotEntry> quickSlotData = new List<QuickSlotEntry>();
        public List<EquippedItemEntry> equippedItemsData = new List<EquippedItemEntry>();
        public List<SkillEntry> ownedSkills = new List<SkillEntry>();
        public List<string> equippedSkillIds = new List<string>();

        public ProgressionState progressionState;

        public SaveData()
        {
            currencyTypes = new List<CurrencyType>();
            currencyAmounts = new List<int>();
            inventoryData = new List<ItemEntry>();
            relicData = new RelicGridState();
            shopStockData = new List<ShopStockState>();
            quickSlotData = new List<QuickSlotEntry>();
            equippedItemsData = new List<EquippedItemEntry>();
            ownedSkills = new List<SkillEntry>();
            equippedSkillIds = new List<string>();
            progressionState = new ProgressionState();
            // puzzleAttempts = new Dictionary<string, PuzzleAttemptData>(); // 나중에 사용 예정
        }
    }

    [Serializable]
    public class QuickSlotEntry
    {
        public int slotIndex;
        public string itemId;
        public int count;
        public string instanceId;
    }

    [Serializable]
    public class EquippedItemEntry
    {
        public EquipmentSlotType slotType;
        public string itemId;
        public string instanceId;
    }

    [Serializable]
    public class ItemEntry
    {
        public int slotIndex;
        public string itemId;
        public int count;
        public string instanceId;
    }

    [Serializable]
    public class SkillEntry
    {
        public string skillId;
        public int level;
        public int exp;
    }
}