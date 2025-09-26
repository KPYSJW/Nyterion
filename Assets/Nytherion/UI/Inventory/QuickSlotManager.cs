using UnityEngine;
using Nytherion.Data.ScriptableObjects.Items;
using Nytherion.Core.Data;
using Nytherion.Core.Interfaces;
using Nytherion.Core.Systems;
using System;
using System.Collections.Generic;
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
                    slots[i].SetKeyLabel(keys[i].ToString().Replace("Alpha", ""));
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
            // 더 이상 내부 배열을 업데이트하지 않고 이벤트만 발생
            OnQuickSlotUpdated?.Invoke();
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

            for (int i = 0; i < slots.Length; i++)
            {
                var (uiItem, uiCount) = slots[i].GetItemInfo();

                if (uiItem != null && uiCount > 0)
                {
                    saveData.quickSlotData.Add(new QuickSlotEntry
                    {
                        slotIndex = i,
                        itemId = uiItem.ID,
                        count = uiCount,
                        instanceId = uiItem.isStackable ? null : uiItem.instanceId
                    });
                }
            }
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

            foreach (var entry in saveData.quickSlotData)
            {
                if (entry.slotIndex < 0 || entry.slotIndex >= slots.Length) continue;

                ItemData itemAsset = ItemDatabase.GetItemByID(entry.itemId);
                if (itemAsset == null) continue;

                if (!(itemAsset is ConsumableData)) continue;

                ItemData itemToPlace = itemAsset;
                if (!itemToPlace.isStackable)
                {
                    itemToPlace = Instantiate(itemAsset);
                    itemToPlace.instanceId = string.IsNullOrEmpty(entry.instanceId)
                                            ? System.Guid.NewGuid().ToString()
                                            : entry.instanceId;
                }

                slots[entry.slotIndex].SetItem(itemToPlace, entry.count);
            }
        }
    }
}