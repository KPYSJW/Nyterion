using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Nytherion.Data.ScriptableObjects.Items;
using Nytherion.Core.Interfaces;
using Nytherion.UI.Inventory;
using Nytherion.Core.Systems;
using Nytherion.Core.Data;

namespace Nytherion.Core.Managers
{
    public class InventoryManager : MonoBehaviour, IInventoryManager
    {
        public static InventoryManager Instance { get; private set; }

        [Header("Inventory Settings")]
        [SerializeField] private int maxSlotCount = 24;

        public int MaxSlotCount => maxSlotCount;
        public InventoryModel InventoryModel { get; private set; }

        public event Action OnInitialized;
        public event Action<ItemData, int> OnItemAdded;
        public event Action<ItemData, int> OnItemRemoved;
        public event Action OnInventoryUpdated;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
            }

            InventoryModel = new InventoryModel(maxSlotCount);
        }

        public void Initialize()
        {
            InventoryModel.OnItemAdded += (item, count) => OnItemAdded?.Invoke(item, count);
            InventoryModel.OnItemRemoved += (item, count) => OnItemRemoved?.Invoke(item, count);
            InventoryModel.OnInventoryUpdated += () => OnInventoryUpdated?.Invoke();
            OnInitialized?.Invoke();
        }
        public void TriggerInventoryUpdate()
        {
            OnInventoryUpdated?.Invoke();
        }
        public List<ItemEntry> GetInventoryForSave()
        {
            var itemEntries = new List<ItemEntry>();
            var groupedItems = InventoryModel.Items
                .GroupBy(item => item.isStackable ? item.ID : item.instanceId);

            foreach (var group in groupedItems)
            {
                var representativeItem = group.First();
                if (representativeItem.isStackable)
                {
                    itemEntries.Add(new ItemEntry
                    {
                        ItemId = representativeItem.ID,
                        Count = group.Count()
                    });
                }
                else
                {
                    foreach (var item in group)
                    {
                        Debug.Log($"[InventoryManager] Saving item {item.ID} with instanceId {item.instanceId}");
                        itemEntries.Add(new ItemEntry
                        {
                            ItemId = item.ID,
                            Count = 1,
                            InstanceId = item.instanceId
                        });
                    }
                }
            }
            return itemEntries;
        }

        public void LoadDataFromSave(List<ItemEntry> itemEntries)
        {
            InventoryModel.Clear();
            if (itemEntries == null)
            {
                OnInventoryUpdated?.Invoke();
                return;
            }

            foreach (var entry in itemEntries)
            {
                ItemData itemAsset = ItemDatabase.GetItemByID(entry.ItemId);
                if (itemAsset == null)
                {
                    Debug.LogWarning($"[InventoryManager] Item with ID {entry.ItemId} not found in ItemDatabase.");
                    continue;
                }

                if (itemAsset.isStackable)
                {
                    InventoryModel.AddItem(itemAsset, entry.Count);
                }
                else
                {
                    ItemData newItemInstance = Instantiate(itemAsset);
                    newItemInstance.instanceId = !string.IsNullOrEmpty(entry.InstanceId)
                        ? entry.InstanceId
                        : Guid.NewGuid().ToString();
                    Debug.Log($"[InventoryManager] Loaded item {entry.ItemId} with instanceId {newItemInstance.instanceId}");
                    InventoryModel.AddItem(newItemInstance, 1);
                }
            }
            OnInventoryUpdated?.Invoke();
        }

        public bool AddItem(ItemData item) => AddItem(item, 1);

        public bool AddItem(ItemData item, int count)
        {
            if (item == null || count <= 0) return false;

            Debug.Log($"[InventoryManager] Attempting to add {item.itemName} (Type: {item.GetType().Name}) x{count}");

            if (item.isStackable)
            {
                if (InventoryModel.IsFull) return false;
                return InventoryModel.AddItem(item, count);
            }
            else
            {
                for (int i = 0; i < count; i++)
                {
                    if (InventoryModel.IsFull)
                    {
                        Debug.LogWarning($"인벤토리가 가득 차 {item.itemName}을(를) 추가할 수 없습니다.");
                        if (i > 0) OnInventoryUpdated?.Invoke();
                        return i > 0;
                    }
                    ItemData newItemInstance = Instantiate(item);
                    newItemInstance.instanceId = Guid.NewGuid().ToString();
                    InventoryModel.AddItem(newItemInstance, 1);
                }
            }
            return true;
        }
        public bool AddItemWithoutNotify(ItemData item, int count = 1)
        {
            return InventoryModel.AddItemSilently(item, count);
        }
        public bool RemoveItem(ItemData item) => RemoveItem(item, 1);

        public bool RemoveItem(ItemData item, int count = 1)
        {
            return InventoryModel.RemoveItem(item, count);
        }

        public void ClearInventory() => InventoryModel.Clear();

        public Dictionary<ItemData, int> GetAllItems()
        {
            return InventoryModel.Items
                .GroupBy(item => item.isStackable ? item.ID : item.instanceId)
                .ToDictionary(
                    group => group.First(),
                    group => group.Count()
                );
        }

        public int GetItemCount(ItemData item) => InventoryModel.GetItemCount(item);
        public bool IsFull => InventoryModel.IsFull;
        public bool HasItem(ItemData item) => InventoryModel.HasItem(item);
        public bool HasItem(string itemId) => InventoryModel.Items.Any(i => i.ID == itemId);
        public void SwapItems(InventorySlotUI fromSlot, InventorySlotUI toSlot) { }
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