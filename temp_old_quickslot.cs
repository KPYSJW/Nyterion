using UnityEngine;
using Nytherion.Data.ScriptableObjects.Items;
using Nytherion.Core.Data;
using Nytherion.Core.Systems;
using System;
using System.Collections.Generic;

namespace Nytherion.UI.Inventory
{
    public class QuickSlotManager : MonoBehaviour
    {
        public static QuickSlotManager Instance { get; private set; }

        [SerializeField] private QuickSlotUI[] slots;

        private void EnsureSlotReferences()
        {
            if (slots == null || slots.Length == 0)
            {
                slots = GetComponentsInChildren<QuickSlotUI>(includeInactive: true);
            }
        }
        [SerializeField]
        private KeyCode[] keys =
        {
            KeyCode.Alpha1,
            KeyCode.Alpha2,
            KeyCode.Alpha3,
        };

        private ItemData[] quickSlotItems;
        private int[] quickSlotItemCounts;
        public event Action OnQuickSlotUpdated;
        private void Awake()
        {
            EnsureSlotReferences();
            if (Instance == null)
            {
                Instance = this;
                quickSlotItems = new ItemData[slots.Length];
                quickSlotItemCounts = new int[slots.Length];
            }
            else
            {
                Destroy(gameObject);
                return;
            }

            for (int i = 0; i < slots.Length && i < keys.Length; i++)
            {
                int slotIndex = i;
                slots[i].Initialize(slotIndex);
                slots[i].SetKeyLabel(keys[i].ToString().Replace("Alpha", ""));
                slots[i].OnItemSet += (item, count) => UpdateSlotData(slotIndex, item, count);
                slots[i].OnItemCleared += () => UpdateSlotData(slotIndex, null, 0);
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
            UpdateSlotData(index, item, count);
        }

        private void UpdateSlotData(int index, ItemData item, int count)
        {
            if (index < 0 || index >= quickSlotItems.Length) return;
            quickSlotItems[index] = item;
            quickSlotItemCounts[index] = count;
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
            UpdateSlotData(index, null, 0);
        }

        public (ItemData item, int count) GetItemInfo(int index)
        {
            if (index < 0 || index >= slots.Length) return (null, 0);
            return (quickSlotItems[index], quickSlotItemCounts[index]);
        }

        public void GetStateForSave(SaveData saveData)
        {
            if (saveData == null) return;

            if (quickSlotItems == null || quickSlotItems.Length != slots.Length)
            {
                quickSlotItems = new ItemData[slots.Length];
                quickSlotItemCounts = new int[slots.Length];
            }

            for (int i = 0; i < slots.Length; i++)
            {
                var (uiItem, uiCount) = slots[i].GetItemInfo();
                quickSlotItems[i] = uiItem;
                quickSlotItemCounts[i] = uiCount;
            }

            saveData.quickSlotData.Clear();
            saveData.quickSlotItemIDs.Clear();
            saveData.quickSlotItemCounts.Clear();

            for (int i = 0; i < quickSlotItems.Length; i++)
            {
                var item = quickSlotItems[i];
                var count = quickSlotItemCounts[i];

                if (item != null && count > 0)
                {
                    saveData.quickSlotData.Add(new QuickSlotEntry
                    {
                        slotIndex = i,
                        itemId = item.ID,
                        count = count,
                        instanceId = item.isStackable ? null : item.instanceId
                    });

                    saveData.quickSlotItemIDs.Add(item.ID);
                    saveData.quickSlotItemCounts.Add(count);

                }
                else
                {
                    saveData.quickSlotItemIDs.Add(string.Empty);
                    saveData.quickSlotItemCounts.Add(0);
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
                UpdateSlotData(slotIndex, item, newCount);
            }

            return true;
        }
        public void LoadStateFromSave(SaveData saveData)
        {
            if (saveData == null) return;

            for (int i = 0; i < slots.Length; i++)
            {
                slots[i].ClearSlot();
            }

            List<QuickSlotEntry> entries = null;

            if (saveData.quickSlotData != null && saveData.quickSlotData.Count > 0)
            {
                entries = saveData.quickSlotData;
            }
            else if (saveData.quickSlotItemIDs != null && saveData.quickSlotItemIDs.Count > 0)
            {
                entries = new List<QuickSlotEntry>();
                for (int i = 0; i < saveData.quickSlotItemIDs.Count && i < slots.Length; i++)
                {
                    string legacyId = saveData.quickSlotItemIDs[i];
                    if (string.IsNullOrEmpty(legacyId)) continue;

                    int legacyCount = (i < saveData.quickSlotItemCounts.Count) ? saveData.quickSlotItemCounts[i] : 1;

                    entries.Add(new QuickSlotEntry
                    {
                        slotIndex = i,
                        itemId = legacyId,
                        count = legacyCount,
                        instanceId = null
                    });
                }

                saveData.quickSlotData = entries;
            }

            if (entries == null) return;

            foreach (var entry in entries)
            {
                if (entry.slotIndex < 0 || entry.slotIndex >= slots.Length) continue;

                ItemData itemAsset = ItemDatabase.GetItemByID(entry.itemId);
                if (itemAsset == null)
                {
                    continue;
                }

                if (!(itemAsset is ConsumableData))
                {
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
            }
        }
    }
}