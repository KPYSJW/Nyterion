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

        [Inject]
        public void Construct(InventoryDataManager inventoryDataManager)
        {
            this.inventoryDataManager = inventoryDataManager;
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

            Debug.Log($"SetEquipment 호출됨. 대상 슬롯: {slotType}, 새 장비: {(equipment == null ? "null" : equipment.ID)}, updateInventory 값: {updateInventory}");
            Debug.Log($"현재 착용 중이던 장비(oldEquipment): {(oldEquipment == null ? "없음" : oldEquipment.ID)}");

            // 기존 장착된 아이템이 있으면 인벤토리로 복귀
            if (oldEquipment != null && inventoryDataManager != null && updateInventory)
            {
                Debug.LogWarning($" updateInventory가 true라서 oldEquipment를 인벤토리에 다시 추가 시도합니다!");
                bool added = inventoryDataManager.AddItem(oldEquipment, 1);
                Debug.Log($"oldEquipment 인벤토리 추가 결과: {added}");
            }

            if (equipment == null)
            {
                Debug.Log($"장착 데이터에서 {slotType} 슬롯을 제거(해제)합니다.");
                equippedItems.Remove(slotType);
            }
            else
            {
                // 새 아이템 장착 시 인벤토리에서 제거 
                if (inventoryDataManager != null && updateInventory)
                {
                    Debug.Log($"새 장비 장착을 위해 인벤토리에서 {equipment.ID} 제거 시도.");
                    inventoryDataManager.RemoveItem(equipment.ID, 1);
                }

                equippedItems[slotType] = equipment;
            }

            Debug.Log($"OnEquipmentChanged 이벤트 발생시킵니다.");
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

        /// <summary>
        /// 로드된 세이브 데이터를 바탕으로 장비 상태 복구
        /// </summary>
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