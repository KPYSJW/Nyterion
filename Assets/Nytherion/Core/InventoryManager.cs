using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Nytherion.Data.ScriptableObjects.Items;
using Nytherion.Data.ScriptableObjects.Weapons;
using Nytherion.Core.Interfaces;
using Nytherion.UI.Inventory;
using Nytherion.Services;

namespace Nytherion.Core
{
    public class InventoryManager : MonoBehaviour, IInventoryManager
    {
        public static InventoryManager Instance { get; private set; }

        [Header("Inventory Settings")]
        [SerializeField] private int maxSlotCount = 24;

        public int MaxSlotCount => maxSlotCount;
        public InventoryModel InventoryModel { get; private set; }

        private IInventorySaveService saveService;
        private bool isScheduledForSave = false;
        private const float SAVE_DELAY = 2f;

        public event Action OnInitialized;
        public event Action<ItemData, int> OnItemAdded;
        public event Action<ItemData, int> OnItemRemoved;
        public event Action OnInventoryUpdated;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
            transform.SetParent(null);
        }

        public void Initialize()
        {
            ItemDatabase.Initialize();
            InventoryModel = new InventoryModel(maxSlotCount);
            saveService = new PlayerPrefsInventorySaveService();
            LoadInventory();

            InventoryModel.OnItemAdded += (item, count) =>
            {
                OnItemAdded?.Invoke(item, count);
                ScheduleSave();
            };
            InventoryModel.OnItemRemoved += (item, count) =>
            {
                OnItemRemoved?.Invoke(item, count);
                ScheduleSave();
            };
            InventoryModel.OnInventoryUpdated += () => OnInventoryUpdated?.Invoke();

            OnInitialized?.Invoke();
        }

        private void OnDestroy()
        {
            if (Instance == this) SaveInventory();
        }

        public bool AddItem(ItemData item) => AddItem(item, 1);

        public bool AddItem(ItemData item, int count)
        {
            if (item == null || count <= 0) return false;
            bool isEquipment = item is WeaponData || item.itemType == ItemType.Armor;
            if (isEquipment)
            {
                for (int i = 0; i < count; i++)
                {
                    if (InventoryModel.IsFull)
                    {
                        Debug.LogWarning($"[Inventory] 인벤토리가 가득 찼습니다. {item.itemName}을 추가할 수 없습니다.");
                        return i > 0;
                    }
                    ItemData clonedItem = Instantiate(item);
                    clonedItem.name = item.name;
                    InventoryModel.AddItem(clonedItem, 1);
                }
                return true;
            }
            return InventoryModel.AddItem(item, count);
        }

        public bool RemoveItem(ItemData item) => InventoryModel.RemoveItem(item, 1);
        public bool RemoveItem(ItemData item, int count = 1) => InventoryModel.RemoveItem(item, count);
        public void ClearInventory() => InventoryModel.Clear();
        public Dictionary<ItemData, int> GetAllItems() => new Dictionary<ItemData, int>(InventoryModel.Items);
        public int GetItemCount(ItemData item) => InventoryModel.GetItemCount(item);
        public bool IsFull => InventoryModel.IsFull;
        public bool HasItem(ItemData item) => InventoryModel.HasItem(item);
        public bool HasItem(string itemId) => InventoryModel.Items.Keys.Any(i => i.ID == itemId);
        public void SwapItems(InventorySlotUI fromSlot, InventorySlotUI toSlot)
        {
            if (fromSlot == null || toSlot == null || fromSlot.IsEmpty) return;
            InventoryModel.SwapItems(fromSlot.Item, toSlot.Item);
        }

        public void SaveInventory()
        {
            var state = new InventoryState(new Dictionary<ItemData, int>(InventoryModel.Items));
            saveService?.SaveInventory(state);
        }

        public void LoadInventory()
        {
            var state = saveService.LoadInventory();
            if (state == null) return;
            InventoryModel.Clear();
            foreach (var entry in state.Items)
            {
                if (string.IsNullOrEmpty(entry.ItemId) || entry.Count <= 0) continue;
                ItemData item = ItemDatabase.GetItemByID(entry.ItemId);
                if (item != null) InventoryModel.AddItem(item, entry.Count);
            }
        }

        private void ScheduleSave()
        {
            if (isScheduledForSave) return;
            isScheduledForSave = true;
            Invoke(nameof(PerformDelayedSave), SAVE_DELAY);
        }

        private void PerformDelayedSave()
        {
            if (isScheduledForSave)
            {
                SaveInventory();
                isScheduledForSave = false;
            }
        }

        public bool MoveToEquipment(ItemData item, int count = 1)
        {
            if (item == null || count <= 0) return false;
            if (InventoryModel.GetItemCount(item) < count) return false;
            return RemoveItem(item, count);
        }

        public bool MoveToInventory(ItemData item, int count = 1)
        {
            if (item == null || count <= 0) return false;
            if (InventoryModel.IsFull && !InventoryModel.HasItem(item)) return false;
            return AddItem(item, count);
        }

        public void RegisterQuickSlot(QuickSlotUI quickSlot, ItemData item, int count, Action<ItemData, int> onUseCallback = null)
        {
            if (quickSlot == null || item == null || count <= 0) return;
            Action<ItemData, int> onUsed = onUseCallback ?? ((usedItem, usedCount) => RemoveItem(usedItem, usedCount));
            quickSlot.SetItem(item, count, onUsed);
        }
    }
}