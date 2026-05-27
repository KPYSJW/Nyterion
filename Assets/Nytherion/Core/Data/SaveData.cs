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

        public DungeonMapSaveData dungeonMapData;

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
            dungeonMapData = new DungeonMapSaveData();
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

    [Serializable]
    public class DungeonMapSaveData
    {
        public bool hasMap;
        public bool hasCheckpoint;
        public int currentRoomId = -1;
        public int lastSafeRoomId = -1;
        public float lastSafeX;
        public float lastSafeY;
        public bool portalsUnlocked;
        public bool hasBossSpawned;
        public List<DungeonRoomSaveData> rooms = new();
        public List<Vector2IntSaveData> wallTiles = new();
        public List<Vector2IntSaveData> portalTiles = new();
        public List<PortalLinkSaveData> portalLinks = new();
        public List<RoomConnectionSaveData> roomConnections = new();
        public List<ObstacleSaveData> obstacles = new();
    }

    [Serializable]
    public class DungeonRoomSaveData
    {
        public int id;
        public int gridX;
        public int gridY;
        public int sizeX;
        public int sizeY;
        public float centerX;
        public float centerY;
        public string roomType;
        public bool hasBossSpawnPoint;
        public float bossSpawnX;
        public float bossSpawnY;
        public bool visited;
        public bool cleared;
        public List<Vector2IntSaveData> floorTiles = new();
    }

    [Serializable]
    public class Vector2IntSaveData
    {
        public int x;
        public int y;
    }

    [Serializable]
    public class PortalLinkSaveData
    {
        public int fromX;
        public int fromY;
        public int toX;
        public int toY;
    }

    [Serializable]
    public class RoomConnectionSaveData
    {
        public int fromRoomId;
        public int toRoomId;
    }

    [Serializable]
    public class ObstacleSaveData
    {
        public string prefabId;
        public float x;
        public float y;
    }
}
