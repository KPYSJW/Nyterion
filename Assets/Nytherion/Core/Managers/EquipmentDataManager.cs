using System;
using System.Collections.Generic;
using UnityEngine;
using Nytherion.Core.Enums;
using Nytherion.Data.ScriptableObjects.Items;
using Nytherion.Core.Data;
using Nytherion.Core.Systems;
using Nytherion.Core.Interfaces;
using Zenject;

namespace Nytherion.Core.Managers
{
    public class EquipmentDataManager : MonoBehaviour, ISaveable, IInitializable
    {

        private Dictionary<EquipmentSlotType, EquipmentData> equippedItems = new Dictionary<EquipmentSlotType, EquipmentData>();
        public IReadOnlyDictionary<EquipmentSlotType, EquipmentData> EquippedItems => equippedItems;
        public event Action<EquipmentSlotType, EquipmentData, EquipmentData> OnEquipmentChanged;


        public void Initialize()
        {
            equippedItems.Clear();
        }

        public void SetEquipment(EquipmentSlotType slotType, EquipmentData equipment)
        {
            equippedItems.TryGetValue(slotType, out var oldEquipment);

            if (equippedItems.ContainsKey(slotType))
            {
                equippedItems[slotType] = equipment;
            }
            else
            {
                equippedItems.Add(slotType, equipment);
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
                    Debug.LogWarning($"[EquipmentDataManager] Equipment with ID {entry.itemId} not found.");
                    continue;
                }

                EquipmentData newEquipment = Instantiate(itemAsset) as EquipmentData;
                newEquipment.instanceId = entry.instanceId;
                SetEquipment(entry.slotType, newEquipment);
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

        public void PopulateSaveData(SaveData saveData)
        {
            saveData.equippedItemsData = GetEquipmentForSave();
        }
        public void LoadFromSaveData(SaveData saveData)
        {
            LoadEquipmentFromSave(saveData.equippedItemsData);
        }
    }
}