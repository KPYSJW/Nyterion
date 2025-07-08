using UnityEngine;
using Nytherion.Data.ScriptableObjects.Items;
using Nytherion.Core.Data;
using Nytherion.Core.Systems;
using Nytherion.Core.Managers;

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
                slots[i].SetKeyLabel(keys[i].ToString().Replace("Alpha", ""));
                slots[i].OnItemSet += (item, count) => UpdateSlotData(i, item, count);
                slots[i].OnItemCleared += () => UpdateSlotData(i, null, 0);
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
            SaveLoadManager.Instance.SaveGame();
        }
        public void UseSlot(int index)
        {
            if (index < 0 || index >= slots.Length) return;
            slots[index].UseItem();
        }

        public void GetStateForSave(SaveData saveData)
        {
            saveData.quickSlotItemIDs.Clear();
            saveData.quickSlotItemCounts.Clear();
            for (int i = 0; i < slots.Length; i++)
            {
                if (quickSlotItems[i] == null)
                {
                    saveData.quickSlotItemIDs.Add(null);
                    saveData.quickSlotItemCounts.Add(0);
                }
                else
                {
                    saveData.quickSlotItemIDs.Add(quickSlotItems[i].ID);
                    saveData.quickSlotItemCounts.Add(quickSlotItemCounts[i]);
                }
            }
        }

        public void LoadStateFromSave(SaveData saveData)
        {
            if (saveData == null || saveData.quickSlotItemIDs == null) return;
            for (int i = 0; i < slots.Length; i++)
            {
                if (i < saveData.quickSlotItemIDs.Count && !string.IsNullOrEmpty(saveData.quickSlotItemIDs[i]))
                {
                    ItemData item = ItemDatabase.GetItemByID(saveData.quickSlotItemIDs[i]);
                    if (item != null)
                    {
                        int count = saveData.quickSlotItemCounts[i];
                        slots[i].SetItem(item, count);
                        UpdateSlotData(i, item, count);
                    }
                }
                else
                {
                    slots[i].ClearSlot();
                    UpdateSlotData(i, null, 0);
                }
            }
        }
    }
}