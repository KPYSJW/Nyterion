using System;
using System.Collections.Generic;
using UnityEngine;
using Nytherion.Core.Enums;
using Nytherion.Data.ScriptableObjects.Items;
using Nytherion.Core.Data;
using Nytherion.Core.Systems;
using Nytherion.Core.Interfaces;
using VContainer;
using VContainer.Unity;

namespace Nytherion.Core.Managers
{
    public class EquipmentDataManager : BaseManager
    {

        private Dictionary<EquipmentSlotType, EquipmentData> equippedItems = new Dictionary<EquipmentSlotType, EquipmentData>();
        public IReadOnlyDictionary<EquipmentSlotType, EquipmentData> EquippedItems => equippedItems;
        public event Action<EquipmentSlotType, EquipmentData, EquipmentData> OnEquipmentChanged;

        // InventoryDataManager 참조 (인벤토리 연동용)
        private InventoryDataManager inventoryDataManager;


        [Inject]
        public void Construct(InventoryDataManager inventoryDataManager)
        {
            this.inventoryDataManager = inventoryDataManager;
        }

        protected override void OnInitializeInternal()
        {
            equippedItems.Clear();
        }

        public void SetEquipment(EquipmentSlotType slotType, EquipmentData equipment)
        {
            SetEquipment(slotType, equipment, true);
        }

        public void SetEquipment(EquipmentSlotType slotType, EquipmentData equipment, bool updateInventory)
        {
            equippedItems.TryGetValue(slotType, out var oldEquipment);

            // 기존 장착된 아이템이 있으면 인벤토리로 복귀 (인벤토리 업데이트가 활성화된 경우에만)
            if (oldEquipment != null && inventoryDataManager != null && updateInventory)
            {
                inventoryDataManager.AddItem(oldEquipment, 1);
            }

            if (equipment == null)
            {
                equippedItems.Remove(slotType);
            }
            else
            {
                // 새 아이템 장착 시 인벤토리에서 제거 (인벤토리 업데이트가 활성화된 경우에만)
                if (inventoryDataManager != null && updateInventory)
                {
                    inventoryDataManager.RemoveItem(equipment.ID, 1);
                }

                equippedItems[slotType] = equipment;
            }

            OnEquipmentChanged?.Invoke(slotType, equipment, oldEquipment);
        }

        public EquipmentData GetEquipment(EquipmentSlotType slotType)
        {
            return equippedItems.TryGetValue(slotType, out var equipment) ? equipment : null;
        }

        private List<EquippedItemEntry> GetEquipmentForSave()
        {
            var entries = new List<EquippedItemEntry>();
            foreach (var kvp in equippedItems)
            {
                if (kvp.Value != null)
                {
                    entries.Add(new EquippedItemEntry
                    {
                        slotType = kvp.Key,
                        itemId = kvp.Value.ID,
                        instanceId = kvp.Value.instanceId
                    });
                }
            }
            return entries;
        }

        private void LoadEquipmentFromSave(List<EquippedItemEntry> entries)
        {
            equippedItems.Clear();
            if (entries == null) return;

            foreach (var entry in entries)
            {
                ItemData itemAsset = ItemDatabase.GetItemByID(entry.itemId);
                if (itemAsset == null || !(itemAsset is EquipmentData))
                {
                    continue;
                }

                EquipmentData newEquipment = Instantiate(itemAsset) as EquipmentData;
                newEquipment.instanceId = entry.instanceId;

                // 로드 시에는 인벤토리를 업데이트하지 않음 (중복 방지)
                SetEquipment(entry.slotType, newEquipment, false);
            }
        }

        public void UnequipAll()
        {
            var slots = new List<EquipmentSlotType>(equippedItems.Keys);
            foreach (var slot in slots)
            {
                SetEquipment(slot, null);
            }
        }

        public override void PopulateSaveData(SaveData saveData)
        {
            var equipmentToSave = GetEquipmentForSave();
            saveData.equippedItemsData = equipmentToSave;
        }

        public override void LoadFromSaveData(SaveData saveData)
        {
            LoadEquipmentFromSave(saveData.equippedItemsData);
        }
    }
}