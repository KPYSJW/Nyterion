using System;
using System.Collections.Generic;
using UnityEngine;
using Nytherion.Data.ScriptableObjects.Items;
using Nytherion.Core.Systems;
using Nytherion.Core.Data;
using Nytherion.Core.Utils;

namespace Nytherion.Core.Managers
{
    /// <summary>
    /// 게임 내 인벤토리 시스템의 전반적인 관리자 클래스 
    /// InventoryModel을 생성 및 소유하며, 세이브/로드 시스템 연동을 담당
    /// </summary>
    public class InventoryManager : BaseManager
    {
        [Header("Inventory Settings")]
        [SerializeField] private int maxSlotCount = 24;

        public int MaxSlotCount => maxSlotCount;

        /// <summary>순수 데이터 조작을 담당하는 내부 모델/// </summary>

        public InventoryModel InventoryModel { get; private set; }

        /// <summary>인벤토리 데이터가 갱신될 떄 UI 등 외부 시스템을 알리기 위한 이벤트/// </summary>
        public event Action OnInventoryUpdated;

        protected override void Awake()
        {
            base.Awake();
            InventoryModel = new InventoryModel(maxSlotCount);
        }

        protected override void OnInitializeInternal()
        {
            // 초기화 시 모델이 없으면 생성하고, 모델의 이벤트를 Manager 이벤트로 포워딩
            if (InventoryModel == null)
            {
                InventoryModel = new InventoryModel(maxSlotCount);
            }

            InventoryModel.OnInventoryUpdated += () => OnInventoryUpdated?.Invoke();
        }

        /// <summary>
        /// 수동으로 인벤토리 업데이트 이벤트를 발생 
        /// </summary>
        public void TriggerInventoryUpdate()
        {
            OnInventoryUpdated?.Invoke();
        }

        /// <summary>
        /// 현재 인벤토리 상태를 세이브 데이터 객체에 저장
        /// </summary>
        public override void PopulateSaveData(SaveData saveData)
        {
            SaveLoadHelper.SafePopulateCollection(
                saveData,
                GetItemEntriesForSave(),
                (data, entries) => data.inventoryData = entries,
                nameof(InventoryManager)
            );
        }

        /// <summary>
        /// 현재 인벤토리의 유효한 아이템들만 추출하여 직렬화용 데이터(ItemEntry) 형태로 변환 
        /// </summary>
        private IEnumerable<ItemEntry> GetItemEntriesForSave()
        {
            if (InventoryModel == null)
            {
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
                        // 장비 등 Stackable하지 않은 아이템은 고유 Instance ID 보존
                        instanceId = item.isStackable ? null : item.instanceId
                    };
                }
            }
        }

        /// <summary>
        /// 저장된 세이브 데이터를 읽어와 현재 인벤토리 모델을 복원
        /// </summary>
        public override void LoadFromSaveData(SaveData saveData)
        {
            SaveLoadHelper.SafeLoadCollection(
                saveData,
                data => data?.inventoryData,
                itemEntries =>
                {
                    if (InventoryModel == null)
                    {
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
                                continue;
                            }

                            ItemData itemToPlace = itemAsset;

                            // 장비처럼 스택 불가능한 아이템은 독립적인 인스턴스로 복제(Instantiate)하여 사용
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

        // --- 외부 노출 API 래핑 메서드 --- //
        public bool AddItem(ItemData item, int count = 1) => InventoryModel.AddItem(item, count);
        public bool RemoveItem(ItemData item, int count = 1) => InventoryModel.RemoveItem(item, count);

        public bool RemoveItemFromSlot(int slotIndex, int count = 1)
        {
            var (item, currentCount) = InventoryModel.GetItemAt(slotIndex);
            if (item == null || currentCount < count) return false;

            InventoryModel.RemoveItemFromSlot(slotIndex, count);
            return true;
        }

        /// <summary>
        /// 장비 해제 등 고유 InstanceId 기반으로 특정 아이템을 찾아 제거
        /// </summary>
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

        public void SwapItems(int fromIndex, int toIndex) => InventoryModel.SwapItems(fromIndex, toIndex);
        public void ClearInventory() => InventoryModel.Clear();

        // --- Item Query (조회) 로직 --- //

        public bool IsFull => InventoryModel.IsFull;
        public (ItemData item, int count) GetItemAt(int index) => InventoryModel.GetItemAt(index);

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

        public bool HasItem(ItemData item) => GetItemCount(item) > 0;

        public bool HasItemByInstanceId(string instanceId)
        {
            for (int i = 0; i < InventoryModel.MaxSlots; i++)
            {
                var (item, _) = InventoryModel.GetItemAt(i);
                if (item != null && !item.isStackable && item.instanceId == instanceId) return true;
            }
            return false;
        }

        /// <summary>
        /// 현재 소지한 모든 아이템 종류와 그 총합 수량을 Dictionary 형태로 반환
        /// </summary>
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
        /// 인벤토리의 빈 슬롯 수를 계산하여 반환
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
        /// 디버깅 및 시스템 상태 확인용 문자열 반환
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
                InventoryModel = null;
            }
        }
    }
}