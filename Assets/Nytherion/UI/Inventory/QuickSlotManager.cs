using UnityEngine;
using Nytherion.Data.ScriptableObjects.Items;
using Nytherion.Core.Data;
using Nytherion.Core.Interfaces;
using Nytherion.Core.Systems;
using System;
using VContainer;

namespace Nytherion.UI.Inventory
{
    public class QuickSlotManager : MonoBehaviour, ISaveable
    {

        private QuickSlotUI[] slots;

        public void TryInjectSlots(QuickSlotUI[] quickSlots)
        {
            if (quickSlots != null && quickSlots.Length > 0)
            {
                slots = quickSlots;
            }
        }

        private void Start()
        {
            
        }
        private GameSceneUIRefs uiRefs;

        [Inject]
        public void Construct(GameSceneUIRefs uiRefs)
        {
            this.uiRefs = uiRefs;
        }

        private void EnsureSlotReferences()
        {
            if (slots == null || slots.Length == 0)
            {
                if (uiRefs != null && uiRefs.QuickSlots != null && uiRefs.QuickSlots.Length > 0)
                {
                    slots = uiRefs.QuickSlots;
                }
                else
                {
                    slots = GetComponentsInChildren<QuickSlotUI>(includeInactive: true);
                    if (slots == null || slots.Length == 0)
                    {
                        slots = new QuickSlotUI[0];
                    }
                }
            }
        }
        [SerializeField]
        private KeyCode[] keys =
        {
            KeyCode.Alpha1,
            KeyCode.Alpha2,
            KeyCode.Alpha3,
        };

        public event Action OnQuickSlotUpdated;
        private void Awake()
        {
            EnsureSlotReferences();

            if (slots != null && slots.Length > 0)
            {
                for (int i = 0; i < slots.Length && i < keys.Length; i++)
                {
                    int slotIndex = i;
                    slots[i].Initialize(slotIndex);
                    slots[i].OnItemSet += (item, count) => OnQuickSlotUpdated?.Invoke();
                    slots[i].OnItemCleared += () => OnQuickSlotUpdated?.Invoke();
                }
            }
            else
            {
                Debug.LogError("[QuickSlotManager] Awake에서 슬롯을 찾을 수 없어 초기화를 건너뜁니다.");
            }
        }


        private void Update()
        {
            for (int i = 0; i < keys.Length && i < slots.Length; i++)
            {
                if (Input.GetKeyDown(keys[i]))
                {
                    UseSlot(i);
                }
            }
        }

        public void UpdateSlotDataExternal(int index, ItemData item, int count)
        {
            //Debug.Log($"[QuickSlotManager] UpdateSlotDataExternal 호출 - 슬롯: {index}, 아이템: {item?.ID ?? "null"}, 개수: {count}");

            if (index < 0 || index >= slots.Length)
            {
                Debug.LogWarning($"[QuickSlotManager] 잘못된 슬롯 인덱스: {index}");
                return;
            }

            // QuickSlotUI가 이미 업데이트했으므로, 여기서는 이벤트만 발생
            // (실제 데이터는 slots[index].GetItemInfo()로 가져올 수 있음)
            OnQuickSlotUpdated?.Invoke();

            //Debug.Log($"[QuickSlotManager] 슬롯 {index} 업데이트 이벤트 발생 완료");
        }

        public void UseSlot(int index)
        {
            if (index < 0 || index >= slots.Length) return;
            slots[index].UseItem();
        }

        public void ClearSlot(int index)
        {
            if (index < 0 || index >= slots.Length) return;
            slots[index].ClearSlot();
        }

        public (ItemData item, int count) GetItemInfo(int index)
        {
            if (index < 0 || index >= slots.Length) return (null, 0);
            return slots[index].GetItemInfo();
        }

        public void PopulateSaveData(SaveData saveData)
        {
            if (saveData == null)
            {
                Debug.LogWarning("[QuickSlotManager] PopulateSaveData: saveData가 null");
                return;
            }

            saveData.quickSlotData.Clear();
            //Debug.Log($"[QuickSlotManager] PopulateSaveData 시작 - slots 개수: {slots?.Length ?? 0}");

            for (int i = 0; i < slots.Length; i++)
            {
                var (uiItem, uiCount) = slots[i].GetItemInfo();

                if (uiItem != null && uiCount > 0)
                {
                    //Debug.Log($"[QuickSlotManager] 슬롯 {i} 저장: {uiItem.ID}, 개수: {uiCount}");
                    saveData.quickSlotData.Add(new QuickSlotEntry
                    {
                        slotIndex = i,
                        itemId = uiItem.ID,
                        count = uiCount,
                        instanceId = uiItem.isStackable ? null : uiItem.instanceId
                    });
                }
                else
                {
                    //Debug.Log($"[QuickSlotManager] 슬롯 {i} 비어있음");
                }
            }
            //Debug.Log($"[QuickSlotManager] PopulateSaveData 완료 - 저장된 항목 수: {saveData.quickSlotData.Count}");
        }
        public bool ConsumeItemInQuickSlot(int slotIndex, int count = 1)
        {
            if (slotIndex < 0 || slotIndex >= slots.Length) return false;

            var (item, currentCount) = slots[slotIndex].GetItemInfo();

            if (item == null || currentCount < count) return false;

            int newCount = currentCount - count;

            if (newCount <= 0)
            {
                ClearSlot(slotIndex);
            }
            else
            {
                slots[slotIndex].SetItem(item, newCount);
            }

            return true;
        }
        public void LoadFromSaveData(SaveData saveData)
        {

            // 모든 슬롯 초기화
            for (int i = 0; i < slots.Length; i++)
            {
                slots[i].ClearSlot();
            }

            if (saveData?.quickSlotData == null || saveData.quickSlotData.Count == 0)
            {
                return;
            }

            Debug.Log($"[QuickSlotManager] 로드할 퀵슬롯 항목 수: {saveData.quickSlotData.Count}");

            foreach (var entry in saveData.quickSlotData)
            {
                Debug.Log($"[QuickSlotManager] 로드 시도 - 슬롯: {entry.slotIndex}, 아이템ID: {entry.itemId}, 개수: {entry.count}");

                if (entry.slotIndex < 0 || entry.slotIndex >= slots.Length)
                {
                    Debug.LogWarning($"[QuickSlotManager] 잘못된 슬롯 인덱스: {entry.slotIndex}");
                    continue;
                }

                ItemData itemAsset = ItemDatabase.GetItemByID(entry.itemId);
                if (itemAsset == null)
                {
                    Debug.LogWarning($"[QuickSlotManager] 아이템을 찾을 수 없음: {entry.itemId}");
                    continue;
                }

                if (!(itemAsset is ConsumableData))
                {
                    Debug.LogWarning($"[QuickSlotManager] ConsumableData가 아님: {entry.itemId}, 타입: {itemAsset.GetType()}");
                    continue;
                }

                ItemData itemToPlace = itemAsset;
                if (!itemToPlace.isStackable)
                {
                    itemToPlace = Instantiate(itemAsset);
                    itemToPlace.instanceId = string.IsNullOrEmpty(entry.instanceId)
                                            ? System.Guid.NewGuid().ToString()
                                            : entry.instanceId;
                }

                slots[entry.slotIndex].SetItem(itemToPlace, entry.count);
                Debug.Log($"[QuickSlotManager] 슬롯 {entry.slotIndex}에 {entry.itemId} 로드 완료");
            }

            Debug.Log("[QuickSlotManager] LoadFromSaveData 완료");
        }
    }
}