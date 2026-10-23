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
            if (updateInventory)
            {
                if (equipment == GetEquipment(slotType)) return;
                if (equipment == null)
                {
                    TryUnequipToInventory(slotType, GetEquipment(slotType));
                    return;
                }

                // 같은 원본 ID를 가진 장비 중에서도 요청한 인스턴스만 이동한다.
                if (inventoryDataManager == null || !inventoryDataManager.IsInitialized) return;
                for (int i = 0; i < inventoryDataManager.MaxSlotCount; i++)
                {
                    if (inventoryDataManager.GetSlot(i).item != equipment) continue;
                    TryEquipFromInventory(slotType, i, equipment);
                    return;
                }
                return;
            }

            // 인벤토리 연동을 생략하는 경로는 저장 복원과 초기 데이터 설정용이다.
            equippedItems.TryGetValue(slotType, out var oldEquipment);
            if (equipment == null)
            {
                equippedItems.Remove(slotType);
            }
            else
            {
                if (string.IsNullOrEmpty(equipment.instanceId)) equipment.instanceId = Guid.NewGuid().ToString();
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

        /// <summary> 보관 슬롯과 장착 슬롯의 기존 인스턴스를 교환한다. 가득 찬 보관함에서도 교체할 수 있다. </summary>
        public bool TryEquipFromInventory(EquipmentSlotType slotType, int inventorySlotIndex, EquipmentData expectedEquipment)
        {
            if (expectedEquipment == null) return false;
            return TryExchangeWithInventory(slotType, inventorySlotIndex, expectedEquipment, GetEquipment(slotType));
        }

        /// <summary> 지정 보관 슬롯으로 해제하거나 교환한다. 인덱스를 생략하면 첫 빈 슬롯을 사용한다. </summary>
        public bool TryUnequipToInventory(EquipmentSlotType slotType, EquipmentData expectedEquipment, int inventorySlotIndex = -1)
        {
            if (expectedEquipment == null || inventoryDataManager == null || !inventoryDataManager.IsInitialized) return false;
            if (inventorySlotIndex == -1)
            {
                for (int i = 0; i < inventoryDataManager.MaxSlotCount; i++)
                {
                    if (inventoryDataManager.InventoryModel.IsDismantleReserved(i) || inventoryDataManager.GetSlot(i).item != null) continue;
                    inventorySlotIndex = i;
                    break;
                }
                if (inventorySlotIndex == -1) return false;
            }

            var target = inventoryDataManager.GetSlot(inventorySlotIndex);
            if (target.item != null && !(target.item is EquipmentData)) return false;
            return TryExchangeWithInventory(slotType, inventorySlotIndex, target.item as EquipmentData, expectedEquipment);
        }

        private bool TryExchangeWithInventory(EquipmentSlotType slotType, int inventorySlotIndex,
            EquipmentData inventoryEquipment, EquipmentData equippedEquipment)
        {
            if (slotType != EquipmentSlotType.Weapon || !IsInitialized || inventoryDataManager == null || !inventoryDataManager.IsInitialized ||
                GetEquipment(slotType) != equippedEquipment || inventoryEquipment == equippedEquipment) return false;
            if (inventoryEquipment != null &&
                (slotType != EquipmentSlotType.Weapon || inventoryEquipment.equipmentType != EquipmentType.Weapon)) return false;

            // 두 상태를 모두 바꾸기 전에는 UI 이벤트와 자동 저장을 발생시키지 않는다.
            if (!inventoryDataManager.InventoryModel.TryExchangeEquipmentWithoutNotify(
                inventorySlotIndex, inventoryEquipment, equippedEquipment)) return false;

            try
            {
                SetEquipment(slotType, inventoryEquipment, false);
            }
            finally
            {
                inventoryDataManager.CompleteEquipmentTransfer(inventorySlotIndex);
            }
            return true;
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
