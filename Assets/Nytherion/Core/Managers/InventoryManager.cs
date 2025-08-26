using System;
using System.Collections.Generic;
using UnityEngine;
using Nytherion.Data.ScriptableObjects.Items;
using Nytherion.Core.Systems;
using Nytherion.Core.Data;
using Nytherion.Core.Interfaces;
using Nytherion.Core.Utils;

namespace Nytherion.Core.Managers
{
    public class InventoryManager : BaseManager
    {

        [Header("Inventory Settings")]
        [SerializeField] private int maxSlotCount = 24;

        public int MaxSlotCount => maxSlotCount;
        public InventoryModel InventoryModel { get; private set; }

        public event Action OnInventoryUpdated;

        protected override void Awake()
        {
            base.Awake();
            InventoryModel = new InventoryModel(maxSlotCount);
        }

        protected override void OnInitializeInternal()
        {
            if (InventoryModel == null)
            {
                InventoryModel = new InventoryModel(maxSlotCount);
            }
            
            InventoryModel.OnInventoryUpdated += () => OnInventoryUpdated?.Invoke();
        }

        public void TriggerInventoryUpdate()
        {
            OnInventoryUpdated?.Invoke();
        }

        public override void PopulateSaveData(SaveData saveData)
        {
            SaveLoadHelper.SafePopulateCollection(
                saveData,
                GetItemEntriesForSave(),
                (data, entries) => data.inventoryData = entries,
                nameof(InventoryManager)
            );
        }

        private IEnumerable<ItemEntry> GetItemEntriesForSave()
        {
            if (InventoryModel == null)
            {
                Debug.LogWarning("[InventoryManager] InventoryModel is null when trying to get save data");
                yield break;
            }
            
            for (int i = 0; i < InventoryModel.MaxSlots; i++)
            {
                var (item, count) = InventoryModel.GetItemAt(i);
                if (item != null && count > 0)
                {
                    yield return new ItemEntry
                    {
                        slotIndex = i,
                        itemId = item.ID,
                        count = count,
                        instanceId = item.isStackable ? null : item.instanceId
                    };
                }
            }
        }

        public override void LoadFromSaveData(SaveData saveData)
        {
            SaveLoadHelper.SafeLoadCollection(
                saveData,
                data => data?.inventoryData,
                itemEntries =>
                {
                    if (InventoryModel == null)
                    {
                        Debug.LogWarning("[InventoryManager] InventoryModel is null when trying to load inventory data");
                        return;
                    }

                    InventoryModel.Clear();

                    if (itemEntries == null || itemEntries.Count == 0)
                    {
                        TriggerInventoryUpdate();
                        return;
                    }

                    foreach (var entry in itemEntries)
                    {
                        try
                        {
                            ItemData itemAsset = ItemDatabase.GetItemByID(entry.itemId);
                            if (itemAsset == null)
                            {
                                Debug.LogWarning($"[InventoryManager] Item with ID '{entry.itemId}' not found in database");
                                continue;
                            }

                            ItemData itemToPlace = itemAsset;
                            if (!itemAsset.isStackable)
                            {
                                itemToPlace = Instantiate(itemAsset);
                                itemToPlace.instanceId = !string.IsNullOrEmpty(entry.instanceId) 
                                    ? entry.instanceId 
                                    : Guid.NewGuid().ToString();
                            }

                            if (entry.slotIndex >= 0 && entry.slotIndex < InventoryModel.MaxSlots)
                            {
                                InventoryModel.AddItemToSlot(itemToPlace, entry.count, entry.slotIndex);
                            }
                            else
                            {
                                Debug.LogWarning($"[InventoryManager] Invalid slot index: {entry.slotIndex}");
                            }
                        }
                        catch (Exception e)
                        {
                            Debug.LogError($"[InventoryManager] Error loading item entry: {e.Message}");
                        }
                    }

                    TriggerInventoryUpdate();
                },
                nameof(InventoryManager)
            );
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

        /// <summary>
        /// 인벤토리의 빈 슬롯 수를 반환합니다.
        /// </summary>
        public int GetEmptySlotCount()
        {
            if (InventoryModel == null) return 0;
            
            int emptyCount = 0;
            for (int i = 0; i < InventoryModel.MaxSlots; i++)
            {
                var (item, _) = InventoryModel.GetItemAt(i);
                if (item == null)
                {
                    emptyCount++;
                }
            }
            return emptyCount;
        }

        /// <summary>
        /// 인벤토리 상태 정보를 반환합니다.
        /// </summary>
        public override string GetStatusInfo()
        {
            if (InventoryModel == null)
            {
                return $"{base.GetStatusInfo()}, InventoryModel: null";
            }

            int usedSlots = MaxSlotCount - GetEmptySlotCount();
            return $"{base.GetStatusInfo()}, Slots: {usedSlots}/{MaxSlotCount}";
        }

        /// <summary>
        /// 메모리 정리
        /// </summary>
        protected override void OnDestroy()
        {
            base.OnDestroy();
            OnInventoryUpdated = null;
            
            if (InventoryModel != null)
            {
                InventoryModel.OnInventoryUpdated -= () => OnInventoryUpdated?.Invoke();
            }
        }
    }
}