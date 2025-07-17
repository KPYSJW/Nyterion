using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Nytherion.Data.ScriptableObjects.Items;
using Nytherion.Core.Systems;
using Nytherion.Core.Data;

namespace Nytherion.Core.Managers
{
    public class InventoryManager : MonoBehaviour
    {
        public static InventoryManager Instance { get; private set; }

        [Header("Inventory Settings")]
        [SerializeField] private int maxSlotCount = 24;

        public int MaxSlotCount => maxSlotCount;
        public InventoryModel InventoryModel { get; private set; }

        public event Action OnInitialized;
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
            InventoryModel.OnInventoryUpdated += () => OnInventoryUpdated?.Invoke();
            OnInitialized?.Invoke();
        }

        public void TriggerInventoryUpdate()
        {
            OnInventoryUpdated?.Invoke();
        }

        // --- Save & Load ---
        public List<ItemEntry> GetInventoryForSave()
        {
            var itemEntries = new List<ItemEntry>();
            if (InventoryModel == null)
            {
                Debug.LogWarning("InventoryModel is null when trying to save inventory data");
                return itemEntries;
            }
            
            for (int i = 0; i < InventoryModel.MaxSlots; i++)
            {
                var (item, count) = InventoryModel.GetItemAt(i);
                if (item != null && count > 0)
                {
                    itemEntries.Add(new ItemEntry
                    {
                        slotIndex = i,
                        itemId = item.ID,
                        count = count,
                        instanceId = item.isStackable ? null : item.instanceId
                    });
                }
            }
            return itemEntries;
        }

        public void LoadDataFromSave(List<ItemEntry> itemEntries)
        {
            if (InventoryModel == null)
            {
                Debug.LogWarning("InventoryModel is null when trying to load inventory data");
                return;
            }
            
            InventoryModel.Clear();
            if (itemEntries == null)
            {
                OnInventoryUpdated?.Invoke();
                return;
            }

            foreach (var entry in itemEntries)
            {
                ItemData itemAsset = ItemDatabase.GetItemByID(entry.itemId);
                if (itemAsset == null) continue;

                ItemData itemToPlace = itemAsset;
                if (!itemAsset.isStackable)
                {
                    itemToPlace = Instantiate(itemAsset);
                    itemToPlace.instanceId = !string.IsNullOrEmpty(entry.instanceId) ? entry.instanceId : Guid.NewGuid().ToString();
                }
                InventoryModel.AddItemToSlot(itemToPlace, entry.count, entry.slotIndex);
            }
        }

        public bool AddItem(ItemData item, int count = 1)
        {
            return InventoryModel.AddItem(item, count);
        }

        public bool RemoveItem(ItemData item, int count = 1)
        {
            return InventoryModel.RemoveItem(item, count);
        }

        public bool RemoveItemFromSlot(int slotIndex, int count = 1)
        {
            var (item, currentCount) = InventoryModel.GetItemAt(slotIndex);
            if (item == null || currentCount < count) return false;

            InventoryModel.RemoveItemFromSlot(slotIndex, count);
            return true;
        }

        public bool RemoveItemByInstanceId(string instanceId)
        {
            for (int i = 0; i < InventoryModel.MaxSlots; i++)
            {
                var (item, _) = InventoryModel.GetItemAt(i);
                if (item != null && !item.isStackable && item.instanceId == instanceId)
                {
                    InventoryModel.RemoveItemFromSlot(i, 1);
                    return true;
                }
            }
            return false;
        }
        public (ItemData item, int count) RemoveItemFromSlotWithoutNotify(int slotIndex, int count = 1)
        {
            var (item, currentCount) = InventoryModel.GetItemAt(slotIndex);
            if (item == null || currentCount < count) return (null, 0);

            return InventoryModel.RemoveItemFromSlotWithoutNotify(slotIndex, count);
        }
        public void SwapItems(int fromIndex, int toIndex)
        {
            InventoryModel.SwapItems(fromIndex, toIndex);
        }

        public void ClearInventory() => InventoryModel.Clear();

        // --- Item Query ---
        public bool IsFull => InventoryModel.IsFull;

        public (ItemData item, int count) GetItemAt(int index)
        {
            return InventoryModel.GetItemAt(index);
        }

        public int GetItemCount(ItemData item)
        {
            if (item == null) return 0;
            int totalCount = 0;
            for (int i = 0; i < InventoryModel.MaxSlots; i++)
            {
                var (slotItem, slotCount) = InventoryModel.GetItemAt(i);
                if (slotItem != null && slotItem.ID == item.ID)
                {
                    totalCount += slotCount;
                }
            }
            return totalCount;
        }

        public bool HasItem(ItemData item)
        {
            return GetItemCount(item) > 0;
        }

        public bool HasItemByInstanceId(string instanceId)
        {
            for (int i = 0; i < InventoryModel.MaxSlots; i++)
            {
                var (item, _) = InventoryModel.GetItemAt(i);
                if (item != null && !item.isStackable && item.instanceId == instanceId)
                {
                    return true;
                }
            }
            return false;
        }

        public Dictionary<ItemData, int> GetAllItems()
        {
            var allItems = new Dictionary<ItemData, int>();
            for (int i = 0; i < InventoryModel.MaxSlots; i++)
            {
                var (item, count) = InventoryModel.GetItemAt(i);
                if (item != null)
                {
                    if (item.isStackable && allItems.ContainsKey(item))
                    {
                        allItems[item] += count;
                    }
                    else if (!allItems.ContainsKey(item))
                    {
                        allItems[item] = count;
                    }
                }
            }
            return allItems;
        }
    }
}