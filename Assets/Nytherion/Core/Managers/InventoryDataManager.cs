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
    /// UI 시스템과 분리되어 아이템의 추가, 제거, 이동, 저장 등의 로직 당담
    /// </summary>
    public class InventoryDataManager : BaseManager, IDataManager, IInventoryDataNotifier
    {
        [Header("Inventory Settings")]
        [Tooltip("인벤토리의 최대 슬롯 개수를 지정")]
        [SerializeField] private int maxSlotCount = 24;

        public int MaxSlotCount => maxSlotCount;

        /// <summary> 실제 인벤토리 데이터가 담겨있는 객체 </summary>
        public InventoryModel InventoryModel { get; private set; }

        /// <summary>
        /// 인벤토리 데이터에 변경이 발생하면 호출
        /// UI에서 활용하여 화면을 갱신
        /// </summary>
        public event Action<InventoryChangeData> OnDataChanged;

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

        /// <summary>
        /// 내부에서 변경이 일어났을 때, 외부로 전체 갱신 알림 보냄
        /// </summary>
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

        /* ==========================================================
         * 데이터 조작 메서드 (추가 / 제거 / 이동)
         * ========================================================== */

        /// <summary>
        /// 인벤토리에 아이템을 추가. 빈 슬롯을 자동으로 찾는다
        /// </summary>
        /// <param name="itemData">추가할 아이템 데이터</param>
        /// <param name="count">추가할 수량</param>
        /// <returns>추가 성공 여부</returns>
        public bool AddItem(ItemData itemData, int count = 1)
        {
            if (!IsInitialized || itemData == null)
            {
                Debug.LogError("초기화되지 않았거나 itemData가 null");
                return false;
            }

            Debug.Log($"AddItem 호출됨. 아이템: {itemData.ID}, 개수: {count}");

            bool result = InventoryModel.AddItem(itemData, count);

            if (result)
            {
                Debug.Log($"[디버그 - InventoryDataManager] AddItem 실제 모델 추가 성공.");
                NotifyDataChanged(new InventoryChangeData
                {
                    slotIndex = -1, 
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
            else
            {
                Debug.LogWarning($" AddItem 실패 (인벤토리가 꽉 찼거나 기타 이유)");
            }
            return result;
        }

        /// <summary>
        /// 특정 슬롯에 아이템을 장제로 추가하거나 덮어쓴다
        /// </summary>
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
                if (saveLoadManager != null) saveLoadManager.SaveGame();
            }

            return result;
        }

        /// <summary>
        /// 아이템 ID를 기반으로 인벤토리에서 해당 아이템을 검색하여 제거
        /// </summary>
        /// <param name="itemId">제거할 아이템의 고유 ID</param>
        /// <param name="count">제거할 수량 (기본값 : 1)</param>
        /// <returns>제거 성공 여부</returns>
        public bool RemoveItem(string itemId, int count = 1)
        {
            if (!IsInitialized) return false;

            // ID로 아이템을 먼저 찾는다
            ItemData itemToRemove = null;
            for (int i = 0; i < InventoryModel.MaxSlots; i++)
            {
                (ItemData item, int count) slotData = InventoryModel.GetItemAt(i);
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
                    slotIndex = -1, 
                    itemId = itemId,
                    newCount = 0, 
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
        /// <summary>
        /// 특정 슬롯 인덱스를 지정하여 해당 슬롯의 아이템을 제거
        /// </summary>
        /// <param name="slotIndex">제거할 아이템이 있는 슬롯 인덱스</param>
        /// <param name="count">제거할 수량(기본값 : 1)</param>
        /// <returns>제거 성공 여부</returns>
        public bool RemoveItemFromSlot(int slotIndex, int count = 1)
        {
            if (!IsInitialized) return false;

            (ItemData item, int count) slot = InventoryModel.GetItemAt(slotIndex);
            if (slot.item == null) return false;

            string itemId = slot.item.ID;
            int originalCount = slot.count;

            InventoryModel.RemoveItemFromSlot(slotIndex, count);

            // 변경 후 슬롯의 상태를 다시 확인하여 남은 개수를 파악
            (ItemData item, int count) updatedSlot = InventoryModel.GetItemAt(slotIndex);
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

        
        /// <summary>
        /// 두 슬롯 간의 아이템 위치를 서로 바꾼다
        /// </summary>

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

        /// <summary>
        /// 두 슬롯의 아이템 위치를 교환합니다.
        /// </summary>
        public bool MoveItem(int fromSlotIndex, int toSlotIndex)
        {
            return SwapItems(fromSlotIndex, toSlotIndex);
        }

        /* ==========================================================
         * 데이터 조회 메서드 (Get / 확인용)
         * ========================================================== */

        /// <summary> 특정 슬롯의 아이템 데이터와 수량을 반환 </summary>
        public (ItemData item, int count) GetSlot(int slotIndex)
        {
            return IsInitialized ? InventoryModel.GetItemAt(slotIndex) : (null, 0);
        }

        /// <summary> 특정 ID를 가진 아이템이 인벤토리에 총 몇개 있는지 계산하여 반환 </summary>
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
        /// <summary> 특정 아이템을 요구 수량 이상 보유하고 있는지 확인 </summary>
        public bool HasItem(string itemId, int requiredCount = 1)
        {
            return GetItemCount(itemId) >= requiredCount;
        }

        /// <summary> 비어있지 않은 모든 인벤토리 슬롯의 아이템 정보를 리스트로 반환 </summary>
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

        /// <summary> 현재 비어 있는 슬롯의 개수를 반환 </summary>
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

        /// <summary> 인벤토리가 가득 찼는지 여부를 반환 </summary>
        public bool IsFull()
        {
            return GetEmptySlotCount() == 0;
        }



        /* ==========================================================
         * 세이브 / 로드 관련 메서드
         * ========================================================== */
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
            if(saveData?.inventoryData == null || !IsInitialized) return;

            // 기존 인벤토리 클리어 후 새로 할당 
            InventoryModel = new InventoryModel(maxSlotCount);

            int loadedCount = 0;
            foreach (var entry in saveData.inventoryData)
            {
                var itemData = ItemDatabase.GetItemByID(entry.itemId);
                if (itemData != null && entry.slotIndex >= 0 && entry.slotIndex < maxSlotCount)
                {
                    ItemData itemToLoad = itemData;

                    if (itemData is EquipmentData equipmentAsset)
                    {
                        EquipmentData clonedEquipment = Instantiate(equipmentAsset) as EquipmentData;
                        clonedEquipment.instanceId = entry.instanceId;
                        itemToLoad = clonedEquipment;
                    }

                    bool success = InventoryModel.AddItemToSlot(itemToLoad, entry.count, entry.slotIndex, true);
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

            // 로드 완료 후 전체 UI 갱신 요청
            NotifyDataChanged(new InventoryChangeData
            {
                slotIndex = -1,
                itemId = "",
                newCount = 0,
                changeType = InventoryChangeType.InventoryLoaded
            });

        }

        /// <summary>
        /// 세이브 시스템에 전달하기 위해 현재 인벤토리 상태를 리스트로 변환
        /// </summary>
        private List<ItemEntry> GetItemEntriesForSave()
        {
            var entries = new List<ItemEntry>();

            for (int i = 0; i < maxSlotCount; i++)
            {
                var slot = InventoryModel.GetItemAt(i);
                if (slot.item != null)
                {
                    string savedInstanceId = "";
                    if (slot.item is EquipmentData equipmentData && !string.IsNullOrEmpty(equipmentData.instanceId))
                    {
                        savedInstanceId = equipmentData.instanceId;
                    }

                    entries.Add(new ItemEntry
                    {
                        slotIndex = i,
                        itemId = slot.item.ID,
                        count = slot.count,
                        instanceId = savedInstanceId
                    });
                }
            }

            return entries;
        }

        /// <summary>
        /// 내부적으로 데이터 변경 이벤트를 발생
        /// </summary>
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