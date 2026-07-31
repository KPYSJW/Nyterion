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
    // <summary>
    /// 캐릭터의 장비 데이터를 관리
    /// 아이템의 장착/해제 상태 추적, 인벤토리 및 저장 시스템과 연동
    /// </summary>
    public class EquipmentDataManager : BaseManager
    {

        /// <summary>
        /// 현재 장착된 아이템 목록을 슬롯 타입별로 관리하는 딕셔너리
        /// </summary>
        private Dictionary<EquipmentSlotType, EquipmentData> equippedItems = new Dictionary<EquipmentSlotType, EquipmentData>();

        /// <summary>
        /// 일기 전용으로 접근 가능한 장착 아이템 목록
        /// </summary>
        public IReadOnlyDictionary<EquipmentSlotType, EquipmentData> EquippedItems => equippedItems;

        /// <summary>
        /// 장비 변경하면 발생
        /// (슬롯 타입, 새 장비, 이전 장비)
        /// </summary>
        public event Action<EquipmentSlotType, EquipmentData, EquipmentData> OnEquipmentChanged;
        
        private InventoryDataManager inventoryDataManager;

        private ProgressionManager progressionManager;

        [Inject]
        public void Construct(InventoryDataManager inventoryDataManager, ProgressionManager progressionManager)
        {
            this.inventoryDataManager = inventoryDataManager;
            this.progressionManager = progressionManager;
        }

        protected override void OnInitializeInternal()
        {
            equippedItems.Clear();
        }

        /// <summary>
        /// 특정 슬롯에 장비를 장착하거나 해제
        /// 기본적으로 인베토리 업데이트 수행
        /// </summary>
        /// <param name="slotType">장착할 장비 슬롯 타입</param>
        /// <param name="equipment">장착할 장비 데이터</param>
        public void SetEquipment(EquipmentSlotType slotType, EquipmentData equipment)
        {
            SetEquipment(slotType, equipment, true);
        }
        /// <summary>
        /// 특정 슬롯에 장비를 장착하거나 해제, 인벤토리 연동 여부를 결정
        /// </summary>
        /// <param name="slotType">장착할 장비 슬롯 타입</param>
        /// <param name="equipment">장착할 장비 데이터(null이면 해제)</param>
        /// <param name="updateInventory">true일 경우 기존 장비는 인벤토리, 새 장비는 인벤토리에서 제거</param>
        public void SetEquipment(EquipmentSlotType slotType, EquipmentData equipment, bool updateInventory)
        {
            equippedItems.TryGetValue(slotType, out var oldEquipment);

            // 기존 장착된 아이템이 있으면 인벤토리로 복귀
            if (oldEquipment != null && inventoryDataManager != null && updateInventory)
            {
                bool added = inventoryDataManager.AddItem(oldEquipment, 1);
            }

            if (equipment == null)
            {
                equippedItems.Remove(slotType);
            }
            else
            {
                // 새 아이템 장착 시 인벤토리에서 제거 
                if (inventoryDataManager != null && updateInventory)
                {
                    inventoryDataManager.RemoveItem(equipment.ID, 1);
                }

                equippedItems[slotType] = equipment;

                // --- 추가된 로직: 원거리 무기 장착 시 투사체 기록 ---
                if (progressionManager != null && equipment is Nytherion.Data.ScriptableObjects.Weapons.WeaponData weaponData)
                {
                    if (weaponData.weaponType == global::WeaponType.Ranged &&
                        weaponData.projectilePrefab != null &&
                        weaponData.isArchivable)
                    {
                        progressionManager.RecordProjectile(weaponData.projectilePrefab);
                    }
                }
                // ----------------------------------------------------
            }

            OnEquipmentChanged?.Invoke(slotType, equipment, oldEquipment);
        }
        /// <summary>
        /// 특정 슬롯에 장착된 장비 데이터를 반환
        /// </summary>

        public EquipmentData GetEquipment(EquipmentSlotType slotType)
        {
            return equippedItems.TryGetValue(slotType, out var equipment) ? equipment : null;
        }

        /// <summary>
        /// 세이브 시스템에 저장하기 위해 장착된 아이템 목록을 변환
        /// </summary>
        /// <returns></returns>
        private List<EquippedItemEntry> GetEquipmentForSave()
        {
            List<EquippedItemEntry> entries = new List<EquippedItemEntry>();
            foreach (KeyValuePair<EquipmentSlotType, EquipmentData> kvp in equippedItems)
            {
                if (kvp.Value != null)
                {
                    entries.Add(new EquippedItemEntry
                    {
                        slotType = kvp.Key,
                        itemId = kvp.Value.ID,
                        instanceId = kvp.Value.instanceId,
                        rarity = kvp.Value.rarity
                    });
                }
            }
            return entries;
        }

        /// <summary>
        /// 로드된 세이브 데이터를 바탕으로 장비 상태 복구
        /// </summary>
        private void LoadEquipmentFromSave(List<EquippedItemEntry> entries)
        {
            equippedItems.Clear();
            if (entries == null) return;

            foreach (EquippedItemEntry entry in entries)
            {
                ItemData itemAsset = ItemDatabase.GetItemByID(entry.itemId);
                if (itemAsset == null || !(itemAsset is EquipmentData))
                {
                    continue;
                }

                EquipmentData newEquipment = Instantiate(itemAsset) as EquipmentData;
                newEquipment.instanceId = entry.instanceId;
                newEquipment.ApplyRarityStats(entry.rarity);

                // 로드 시에는 인벤토리를 업데이트하지 않음 (중복 방지)
                SetEquipment(entry.slotType, newEquipment, false);
            }
        }

        /// <summary>
        /// 모든 슬롯의 장비를 해제
        /// </summary>
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