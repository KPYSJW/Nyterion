using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;
using Nytherion.Data.ScriptableObjects.Items;
using Nytherion.Data.ScriptableObjects.Weapons;
using Nytherion.Core.Interfaces;
using Nytherion.UI.Inventory;
using Nytherion.Services;

namespace Nytherion.Core
{

    [RequireComponent(typeof(InventoryUI))]
    public class InventoryManager : MonoBehaviour, IInventoryManager
    {
        [Header("Inventory Settings")]
        [SerializeField] private Transform slotParent;
        [SerializeField] private GameObject slotPrefab;
        [SerializeField] private int maxSlotCount = 24;

        private List<InventorySlotUI> slotPool = new();
        private Dictionary<ItemData, int> items = new();
        private Dictionary<QuickSlotUI, Action<ItemData, int>> quickSlotCallbacks = new();
        private Dictionary<string, ItemData> itemTable = new Dictionary<string, ItemData>();
        private bool needsRefresh;
        private float lastRefreshTime;
        private const float MIN_REFRESH_INTERVAL = 0.1f;
        
        private IInventorySaveService saveService;
        private const float SAVE_DELAY = 2f;
        private bool isScheduledForSave = false;

        public event Action<ItemData, int> OnItemAdded;
        public event Action<ItemData, int> OnItemRemoved;
        public event Action OnInventoryUpdated;
        
        public Dictionary<ItemData, int> GetAllItems()
        {
            return new Dictionary<ItemData, int>(items);
        }
        
        public bool AddItem(ItemData item) => AddItem(item, 1);

        public bool AddItem(ItemData item, int count)
        {
            if (item == null || count <= 0) return false;

            // 장비 아이템은 항상 새로운 슬롯에 할당
            bool isEquipment = item is WeaponData || item.itemType == ItemType.Armor;
            
            if (isEquipment)
            {
                // 장비 아이템의 경우, 아이템을 복제하여 고유한 인스턴스로 만듦
                for (int i = 0; i < count; i++)
                {
                    if (items.Count >= maxSlotCount)
                    {
                        Debug.LogWarning($"[Inventory] 인벤토리가 가득 찼습니다. {item.itemName}을 추가할 수 없습니다.");
                        return i > 0; // 일부라도 추가되었으면 true 반환
                    }
                    
                    // 아이템을 복제하여 고유한 인스턴스 생성
                    ItemData clonedItem = Instantiate(item);
                    clonedItem.name = item.name; // 원본 이름 유지
                    items[clonedItem] = 1;
                    NotifyItemModified(clonedItem, 1, true);
                }
                return true;
            }
            
            // 일반 아이템 처리
            if (items.TryGetValue(item, out int currentCount))
            {
                if (item.isStackable)
                {
                    items[item] = currentCount + count;
                    NotifyItemModified(item, count, true);
                    return true;
                }
                return false;
            }

            if (items.Count >= maxSlotCount)
            {
                Debug.LogWarning($"[Inventory] 인벤토리가 가득 찼습니다. {item.itemName}을 추가할 수 없습니다.");
                return false;
            }

            items[item] = count;
            NotifyItemModified(item, count, true);
            return true;
        }

        public void RegisterQuickSlot(QuickSlotUI quickSlot, ItemData item, int count, Action<ItemData, int> onUseCallback = null)
        {
            if (quickSlot == null || item == null || count <= 0)
            {
                return;
            }

            if (quickSlotCallbacks.TryGetValue(quickSlot, out var oldCallback))
            {
                quickSlot.OnItemUsed -= oldCallback;
                quickSlotCallbacks.Remove(quickSlot);
            }

            Action<ItemData, int> onUsed = onUseCallback ?? ((usedItem, usedCount) =>
            {
                if (RemoveItem(usedItem, usedCount))
                {
                }
            });

            quickSlotCallbacks[quickSlot] = onUsed;
            quickSlot.OnItemUsed += onUsed;
            quickSlot.SetItem(item, count, onUsed);
        }

        public string SaveToJson()
        {
            var state = new Nytherion.UI.Inventory.InventoryState(items.Keys);
            return JsonUtility.ToJson(state);
        }

        public void LoadFromJson(string json)
        {
            var state = JsonUtility.FromJson<Nytherion.UI.Inventory.InventoryState>(json);
            if (state == null)
            {
                return;
            }

            LoadState(state);
        }

        public void LoadState(InventoryState state)
        {
            if (state == null) return;

            ClearInventory();

            foreach (var item in state.Items)
            {
                if (itemTable.TryGetValue(item.ItemId, out var itemData))
                {
                    AddItem(itemData, item.Count);
                }
            }
        }

        public bool RemoveItem(ItemData item) => RemoveItem(item, 1);

        public bool RemoveItem(ItemData item, int count = 1)
        {
            if (item == null || count <= 0) return false;

            if (items.TryGetValue(item, out int currentCount))
            {
                if (currentCount > count)
                {
                    items[item] = currentCount - count;
                    NotifyItemModified(item, -count, true);
                    return true;
                }
                else if (currentCount == count)
                {
                    items.Remove(item);
                    NotifyItemModified(item, -currentCount, false);
                    return true;
                }
            }
            return false;
        }
        
        /// <summary>
        /// 아이템을 장비 슬롯에서 인벤토리로 이동시킵니다.
        /// </summary>
        public bool MoveToInventory(ItemData item, int count = 1)
        {
            if (item == null || count <= 0) return false;
            
            // 인벤토리에 빈 슬롯이 있는지 확인
            if (items.Count >= maxSlotCount && !items.ContainsKey(item))
            {
                Debug.LogWarning("[Inventory] 인벤토리에 빈 슬롯이 없습니다.");
                return false;
            }
            
            // 인벤토리에 아이템 추가
            return AddItem(item, count);
        }
        
        /// <summary>
        /// 인벤토리에서 아이템을 제거하고 장비 슬롯으로 이동시킵니다.
        /// </summary>
        public bool MoveToEquipment(ItemData item, int count = 1)
        {
            if (item == null || count <= 0) return false;
            
            // 인벤토리에 아이템이 충분히 있는지 확인
            if (!items.TryGetValue(item, out int currentCount) || currentCount < count)
            {
                Debug.LogWarning($"[Inventory] 인벤토리에 {item.itemName}이(가) 부족합니다.");
                return false;
            }
            
            // 인벤토리에서 아이템 제거
            return RemoveItem(item, count);
        }
        
        /// <summary>
        /// 인벤토리에 빈 슬롯이 있는지 확인합니다.
        /// </summary>
        public bool HasEmptySlot()
        {
            return items.Count < maxSlotCount;
        }

        public bool HasItem(ItemData item) => items.ContainsKey(item);
        public bool HasItem(string itemId) => items.Keys.Any(item => item.ID == itemId);
        public int GetItemCount(ItemData item) => items.TryGetValue(item, out var count) ? count : 0;
        public bool IsFull => items.Count >= maxSlotCount;
        public int MaxSlotCount => maxSlotCount;

        public (ItemData item, int count) GetSlotContents(int slotIndex)
        {
            if (slotPool == null || slotIndex < 0 || slotIndex >= slotPool.Count)
            {
                return (null, 0);
            }
            return (slotPool[slotIndex].Item, slotPool[slotIndex].StackCount);
        }

        private void NotifyItemModified(ItemData item, int count, bool isAdded)
        {
            if (isAdded)
            {
                UpdateSlotsUI();
                OnItemAdded?.Invoke(item, count);
            }
            else
            {
                RequestSlotsUpdate();
                OnItemRemoved?.Invoke(item, count);
            }
            
            OnInventoryUpdated?.Invoke();
            
            ScheduleSave();
        }

        private void NotifyItemRemoved(ItemData item, int count)
        {
            RequestSlotsUpdate();
            OnItemRemoved?.Invoke(item, count);
            OnInventoryUpdated?.Invoke();
        }

        public static InventoryManager Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            // Make sure we're working with a root object
            transform.SetParent(null);
            Instance = this;
            DontDestroyOnLoad(gameObject);
            
            LoadItemTable();
            InitializeSlots();
            InitializeSaveSystem();
            LoadInventory();
        }

        private void LateUpdate()
        {
            if (needsRefresh && Time.time - lastRefreshTime >= MIN_REFRESH_INTERVAL)
            {
                needsRefresh = false;
                lastRefreshTime = Time.time;
                ForceUpdateSlotsUI();
            }
        }

        private void LoadItemTable()
        {
            itemTable = new Dictionary<string, ItemData>();
            
            #if UNITY_EDITOR
            string[] guids = UnityEditor.AssetDatabase.FindAssets("t:ItemData", new[] {"Assets/Nytherion/Data/ScriptableObjects/Items"});
            
            foreach (string guid in guids)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                ItemData item = UnityEditor.AssetDatabase.LoadAssetAtPath<ItemData>(path);
                
                if (item != null && !string.IsNullOrEmpty(item.ID))
                {
                    if (!itemTable.ContainsKey(item.ID))
                    {
                        itemTable[item.ID] = item;
                    }
                }
            }
            #endif
            
            if (Application.isPlaying && itemTable.Count == 0)
            {
                var allItems = Resources.LoadAll<ItemData>("");
                foreach (var item in allItems)
                {
                    if (item != null && !string.IsNullOrEmpty(item.ID) && !itemTable.ContainsKey(item.ID))
                    {
                        itemTable[item.ID] = item;
                    }
                }
            }
        }
        private void InitializeSlots()
        {
            foreach (Transform child in slotParent)
            {
                Destroy(child.gameObject);
            }
            slotPool.Clear();
            items.Clear();

            for (int i = 0; i < maxSlotCount; i++)
            {
                var slotObj = Instantiate(slotPrefab, slotParent);
                slotObj.SetActive(true);

                if (slotObj.TryGetComponent(out InventorySlotUI slot))
                {
                    slot.Initialize(i);
                    slotPool.Add(slot);
                }
            }
        }

        public void RequestSlotsUpdate()
        {
            needsRefresh = true;
        }

        private void ForceUpdateSlotsUI()
        {
            UpdateSlotsUI(true);
        }

        private void UpdateSlotsUI(bool forceActivate = false)
        {
            foreach (var slot in slotPool)
            {
                slot.ClearSlot();
                if (forceActivate)
                {
                    slot.gameObject.SetActive(true);
                }
            }
            int slotIndex = 0;
            foreach (var itemPair in items)
            {
                if (slotIndex >= slotPool.Count) break;
                slotPool[slotIndex].SetItem(itemPair.Key, itemPair.Value);
                slotIndex++;
            }
        }

        public void SwapItems(InventorySlotUI fromSlot, InventorySlotUI toSlot)
        {
            if (fromSlot == null || toSlot == null || fromSlot.IsEmpty) return;

            var fromItem = fromSlot.Item;
            var toItem = toSlot.Item;
            var fromCount = fromSlot.StackCount;
            var toCount = toSlot.StackCount;

            if (fromItem == toItem && fromItem.isStackable)
            {
                int total = fromCount + toCount;
                if (total <= fromItem.maxStack)
                {
                    items[fromItem] = total;
                    toSlot.SetItem(fromItem, total);
                    fromSlot.ClearSlot();
                    items.Remove(fromItem);
                }
                else
                {
                    int remaining = total - fromItem.maxStack;
                    items[fromItem] = fromItem.maxStack;
                    toSlot.SetItem(fromItem, fromItem.maxStack);
                    fromSlot.SetItem(fromItem, remaining);
                }
            }
            else
            {
                if (!fromSlot.IsEmpty) items[fromItem] = fromCount;
                if (!toSlot.IsEmpty) items[toItem] = toCount;

                fromSlot.SetItem(toItem, toCount);
                toSlot.SetItem(fromItem, fromCount);
            }
            OnInventoryUpdated?.Invoke();
        }

        public void ClearInventory()
        {
            items.Clear();
            ForceUpdateSlotsUI();
            OnInventoryUpdated?.Invoke();
        }

        private void InitializeSaveSystem()
        {
            saveService = new PlayerPrefsInventorySaveService();
        }
        
        public void SaveInventory()
        {
            var state = new InventoryState(items);
            saveService?.SaveInventory(state);
        }
        
        public void LoadInventory()
        {
            var backupItems = new Dictionary<ItemData, int>(items);
            
            try
            {
                var state = saveService.LoadInventory();
                if (state?.Items == null)
                {
                    items.Clear();
                    OnInventoryUpdated?.Invoke();
                    return;
                }

                var newItems = new Dictionary<ItemData, int>();

                foreach (var entry in state.Items)
                {
                    if (string.IsNullOrEmpty(entry.ItemId) || entry.Count <= 0)
                    {
                        continue;
                    }

                    ItemData item = itemTable.Values.FirstOrDefault(i => i.ID == entry.ItemId);
                    if (item != null)
                    {
                        newItems[item] = entry.Count;
                    }
                }

                items = newItems;
                OnInventoryUpdated?.Invoke();
            }
            catch (Exception)
            {
                items = backupItems;
                OnInventoryUpdated?.Invoke();
            }
        }
        
        private void ScheduleSave()
        {
            if (isScheduledForSave) return;
            
            isScheduledForSave = true;
            Invoke(nameof(PerformDelayedSave), SAVE_DELAY);
        }
        
        private void PerformDelayedSave()
        {
            if (isScheduledForSave)
            {
                SaveInventory();
                isScheduledForSave = false;
            }
        }
        
        public void ForceUpdateUI()
        {
            OnInventoryUpdated?.Invoke();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                SaveInventory();
            }
        }
    }
}
