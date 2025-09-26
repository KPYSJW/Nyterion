using System;
using System.Collections.Generic;
using UnityEngine;
using Nytherion.Data.ScriptableObjects.Items;
using Nytherion.Core.Systems;
using Nytherion.Core.Data;
using Nytherion.Core.Interfaces;
using Nytherion.Core.Utils;
using VContainer;

namespace Nytherion.Core.Managers
{
    /// <summary>
    /// 순수 인벤토리 데이터 관리 매니저
    /// UI 관련 기능 없이 데이터 로직만 담당
    /// </summary>
    public class InventoryDataManager : BaseManager, IDataManager, IInventoryDataNotifier
    {
        [Header("Inventory Settings")]
        [SerializeField] private int maxSlotCount = 24;

        public int MaxSlotCount => maxSlotCount;
        public InventoryModel InventoryModel { get; private set; }

        // 데이터 변경 알림 이벤트
        public event Action<InventoryChangeData> OnDataChanged;

        // SaveLoadManager 참조 (자동 저장용)
        private SaveLoadManager saveLoadManager;

        protected override void Awake()
        {
            base.Awake();
        }

        [Inject]
        public void Construct(SaveLoadManager saveLoadManager)
        {
            this.saveLoadManager = saveLoadManager;
        }

        protected override void OnInitializeInternal()
        {
            if (InventoryModel == null)
            {
                InventoryModel = new InventoryModel(maxSlotCount);
            }

            // InventoryModel의 이벤트를 데이터 변경 알림으로 변환
            InventoryModel.OnInventoryUpdated += HandleInventoryModelUpdate;
        }

        private void HandleInventoryModelUpdate()
        {
            // 일반적인 인벤토리 업데이트를 알림
            NotifyDataChanged(new InventoryChangeData
            {
                slotIndex = -1, // 전체 인벤토리 업데이트
                itemId = "",
                newCount = 0,
                changeType = InventoryChangeType.InventoryLoaded
            });
        }

        // 순수 데이터 조작 메서드들
        public bool AddItem(ItemData itemData, int count = 1)
        {
            if (!IsInitialized || itemData == null)
            {
                return false;
            }

            bool result = InventoryModel.AddItem(itemData, count);

            if (result)
            {
                NotifyDataChanged(new InventoryChangeData
                {
                    slotIndex = -1, // TODO: Get actual slot index if needed
                    itemId = itemData.ID,
                    newCount = count,
                    changeType = InventoryChangeType.ItemAdded
                });

                // 자동 저장 실행
                if (saveLoadManager != null)
                {
                    saveLoadManager.SaveGame();
                }
            }
            return result;
        }

        public bool RemoveItem(string itemId, int count = 1)
        {
            if (!IsInitialized) return false;

            // Find item by ID first
            ItemData itemToRemove = null;
            for (int i = 0; i < InventoryModel.MaxSlots; i++)
            {
                var slotData = InventoryModel.GetItemAt(i);
                if (slotData.item != null && slotData.item.ID == itemId)
                {
                    itemToRemove = slotData.item;
                    break;
                }
            }

            if (itemToRemove == null) return false;

            bool result = InventoryModel.RemoveItem(itemToRemove, count);

            if (result)
            {
                NotifyDataChanged(new InventoryChangeData
                {
                    slotIndex = -1, // TODO: Get actual slot index if needed
                    itemId = itemId,
                    newCount = 0, // TODO: Calculate actual remaining count
                    changeType = InventoryChangeType.ItemRemoved
                });

                // 자동 저장 실행
                if (saveLoadManager != null)
                {
                    saveLoadManager.SaveGame();
                }
            }

            return result;
        }

        public bool RemoveItemFromSlot(int slotIndex, int count = 1)
        {
            if (!IsInitialized) return false;

            var slot = InventoryModel.GetItemAt(slotIndex);
            if (slot.item == null) return false;

            string itemId = slot.item.ID;
            int originalCount = slot.count;

            InventoryModel.RemoveItemFromSlot(slotIndex, count);

            // Check the slot again to see the new count
            var updatedSlot = InventoryModel.GetItemAt(slotIndex);
            int newCount = updatedSlot.item != null ? updatedSlot.count : 0;

            NotifyDataChanged(new InventoryChangeData
            {
                slotIndex = slotIndex,
                itemId = itemId,
                newCount = newCount,
                changeType = newCount > 0 ? InventoryChangeType.ItemCountChanged : InventoryChangeType.SlotCleared
            });

            return true;
        }

        public bool MoveItem(int fromSlotIndex, int toSlotIndex)
        {
            if (!IsInitialized) return false;

            InventoryModel.SwapItems(fromSlotIndex, toSlotIndex);

            // SwapItems is void, so we assume success if no exception thrown
            // 이동은 두 슬롯 모두 변경되므로 전체 업데이트로 처리
            NotifyDataChanged(new InventoryChangeData
            {
                slotIndex = -1,
                itemId = "",
                newCount = 0,
                changeType = InventoryChangeType.InventoryLoaded
            });

            return true;
        }

        // 데이터 조회 메서드들
        public (ItemData item, int count) GetSlot(int slotIndex)
        {
            return IsInitialized ? InventoryModel.GetItemAt(slotIndex) : (null, 0);
        }

        public int GetItemCount(string itemId)
        {
            if (!IsInitialized) return 0;

            int totalCount = 0;
            for (int i = 0; i < InventoryModel.MaxSlots; i++)
            {
                var slot = InventoryModel.GetItemAt(i);
                if (slot.item != null && slot.item.ID == itemId)
                {
                    totalCount += slot.count;
                }
            }
            return totalCount;
        }

        public bool HasItem(string itemId, int requiredCount = 1)
        {
            return GetItemCount(itemId) >= requiredCount;
        }

        public List<(ItemData item, int count)> GetAllItems()
        {
            if (!IsInitialized) return new List<(ItemData, int)>();

            var items = new List<(ItemData, int)>();
            for (int i = 0; i < InventoryModel.MaxSlots; i++)
            {
                var slot = InventoryModel.GetItemAt(i);
                if (slot.item != null)
                {
                    items.Add(slot);
                }
            }
            return items;
        }

        public int GetEmptySlotCount()
        {
            if (!IsInitialized) return 0;

            int emptyCount = 0;
            for (int i = 0; i < InventoryModel.MaxSlots; i++)
            {
                var slot = InventoryModel.GetItemAt(i);
                if (slot.item == null)
                {
                    emptyCount++;
                }
            }
            return emptyCount;
        }

        public bool IsFull()
        {
            return GetEmptySlotCount() == 0;
        }

        public bool SwapItems(int fromSlotIndex, int toSlotIndex)
        {
            if (!IsInitialized) return false;
            if (fromSlotIndex < 0 || fromSlotIndex >= InventoryModel.MaxSlots) return false;
            if (toSlotIndex < 0 || toSlotIndex >= InventoryModel.MaxSlots) return false;

            InventoryModel.SwapItems(fromSlotIndex, toSlotIndex);

            // 아이템 교환은 전체 업데이트로 처리
            NotifyDataChanged(new InventoryChangeData
            {
                slotIndex = -1,
                itemId = "",
                newCount = 0,
                changeType = InventoryChangeType.InventoryLoaded
            });

            return true;
        }

        public bool AddItemToSlot(ItemData itemData, int count, int slotIndex, bool forceOverwrite = false)
        {
            if (!IsInitialized || itemData == null) return false;

            bool result = InventoryModel.AddItemToSlot(itemData, count, slotIndex, forceOverwrite);

            if (result)
            {
                NotifyDataChanged(new InventoryChangeData
                {
                    slotIndex = slotIndex,
                    itemId = itemData.ID,
                    newCount = count,
                    changeType = InventoryChangeType.ItemAdded
                });
            }

            return result;
        }

        // Save/Load 기능
        public override void PopulateSaveData(SaveData saveData)
        {
            if (!IsInitialized) return;

            SaveLoadHelper.SafePopulateCollection(
                saveData,
                GetItemEntriesForSave(),
                (data, entries) => data.inventoryData = entries,
                nameof(InventoryDataManager)
            );
        }

        public override void LoadFromSaveData(SaveData saveData)
        {
            if (saveData?.inventoryData == null)
            {
                return;
            }

            if (!IsInitialized)
            {
                return;
            }

            // 기존 인벤토리 클리어
            InventoryModel = new InventoryModel(maxSlotCount);

            int loadedCount = 0;
            foreach (var entry in saveData.inventoryData)
            {
                var itemData = ItemDatabase.GetItemByID(entry.itemId);
                if (itemData != null && entry.slotIndex >= 0 && entry.slotIndex < maxSlotCount)
                {
                    bool success = InventoryModel.AddItemToSlot(itemData, entry.count, entry.slotIndex, true);
                    if (success)
                    {
                        loadedCount++;
                    }
                }
                else
                {
                    Debug.LogWarning($"[InventoryDataManager] 아이템 데이터 없음 또는 잘못된 슬롯: {entry.itemId}, 슬롯 {entry.slotIndex}");
                }
            }

            NotifyDataChanged(new InventoryChangeData
            {
                slotIndex = -1,
                itemId = "",
                newCount = 0,
                changeType = InventoryChangeType.InventoryLoaded
            });

        }

        private List<ItemEntry> GetItemEntriesForSave()
        {
            var entries = new List<ItemEntry>();

            for (int i = 0; i < maxSlotCount; i++)
            {
                var slot = InventoryModel.GetItemAt(i);
                if (slot.item != null)
                {
                    entries.Add(new ItemEntry
                    {
                        slotIndex = i,
                        itemId = slot.item.ID,
                        count = slot.count,
                        instanceId = "" // TODO: Add instanceId support if needed
                    });
                }
            }

            return entries;
        }

        private void NotifyDataChanged(InventoryChangeData changeData)
        {
            OnDataChanged?.Invoke(changeData);
        }

        protected override void OnDestroy()
        {
            if (InventoryModel != null)
            {
                InventoryModel.OnInventoryUpdated -= HandleInventoryModelUpdate;
            }
            base.OnDestroy();
        }
    }
}