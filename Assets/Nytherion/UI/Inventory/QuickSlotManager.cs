using UnityEngine;
using Nytherion.Data.ScriptableObjects.Items;
using Nytherion.Core.Data;
using Nytherion.Core.Systems;
using Nytherion.Core.Managers;
using System.Collections.Generic;

namespace Nytherion.UI.Inventory
{
    public class QuickSlotManager : MonoBehaviour
    {
        public static QuickSlotManager Instance { get; private set; }

        [SerializeField] private QuickSlotUI[] slots;
        [SerializeField]
        private KeyCode[] keys =
        {
            KeyCode.Alpha1,
            KeyCode.Alpha2,
            KeyCode.Alpha3,
        };

        private ItemData[] quickSlotItems;
        private int[] quickSlotItemCounts;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                quickSlotItems = new ItemData[slots.Length];
                quickSlotItemCounts = new int[slots.Length];
            }
            else Destroy(gameObject);
        }

        private void Start()
        {
            for (int i = 0; i < slots.Length && i < keys.Length; i++)
            {
                int slotIndex = i;
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

        private void UpdateSlotData(int index, ItemData item, int count)
        {
            if (index < 0 || index >= quickSlotItems.Length) return;
            quickSlotItems[index] = item;
            quickSlotItemCounts[index] = count;
            if(SaveLoadManager.Instance != null)
            {
                SaveLoadManager.Instance.SaveGame();
            }
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
            saveData.quickSlotData.Clear();
            for (int i = 0; i < slots.Length; i++)
            {
                if (quickSlotItems[i] != null)
                {
                    saveData.quickSlotData.Add(new QuickSlotEntry
                    {
                        slotIndex = i,
                        itemId = quickSlotItems[i].ID,
                        count = quickSlotItemCounts[i],
                        instanceId = quickSlotItems[i].isStackable ? null : quickSlotItems[i].instanceId
                    });
                }
            }
        }

        public void LoadStateFromSave(SaveData saveData)
        {
            if (saveData == null) return;
        
            for (int i = 0; i < slots.Length; i++)
            {
                slots[i].ClearSlot();
            }
        
            if (saveData.quickSlotData != null)
            {
                foreach (var entry in saveData.quickSlotData)
                {
                    if (entry.slotIndex < 0 || entry.slotIndex >= slots.Length) continue;
        
                    ItemData itemAsset = ItemDatabase.GetItemByID(entry.itemId);
                    if (itemAsset == null || !(itemAsset is ConsumableData))
                    {
                        continue;
                    }
                    
                    slots[entry.slotIndex].SetItem(itemAsset, entry.count);
                    quickSlotItems[entry.slotIndex] = itemAsset;
                    quickSlotItemCounts[entry.slotIndex] = entry.count;
                }
            }
        }
    }
}