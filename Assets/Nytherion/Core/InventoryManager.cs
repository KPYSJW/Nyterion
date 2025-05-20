using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;
using Nytherion.Data.ScriptableObjects.Items;
using Nytherion.Interfaces;
using Nytherion.UI.Inventory;

namespace Nytherion.Core
{
    public interface IInventoryManager
    {
        bool AddItem(ItemData item);
        bool AddItem(ItemData item, int count);
        bool RemoveItem(ItemData item);
        bool RemoveItem(ItemData item, int count);
        void SwapItems(InventorySlotUI fromSlot, InventorySlotUI toSlot);
        bool IsFull { get; }
        event Action<ItemData, int> OnItemAdded;
        event Action<ItemData, int> OnItemRemoved;
        event Action OnInventoryUpdated;
    }

    [RequireComponent(typeof(InventoryUI))]
    public class InventoryManager : MonoBehaviour, Nytherion.Interfaces.IInventoryManager
    {
        // 인벤토리 슬롯 및 아이템 관리
        [Header("Inventory Settings")]
        [SerializeField] private Transform slotParent;
        [SerializeField] private GameObject slotPrefab;
        [SerializeField] private int maxSlotCount = 24;
        
        [Header("Debug")]
        public ItemData testItemData;
        public int testItemCount = 5;
        
        private List<InventorySlotUI> slotPool = new();
        private Dictionary<ItemData, int> items = new(); // 아이템별 수량 추적
        private Dictionary<QuickSlotUI, Action<ItemData, int>> quickSlotCallbacks = new();
        private Dictionary<string, ItemData> itemTable = new();
        private bool needsRefresh;
        private float lastRefreshTime;
        private const float MIN_REFRESH_INTERVAL = 0.1f; // 100ms

        // 인벤토리 상태 이벤트
        public event Action<ItemData, int> OnItemAdded;
        public event Action<ItemData, int> OnItemRemoved;
        public event Action OnInventoryUpdated;

        // IInventoryManager 인터페이스 구현
        bool Nytherion.Interfaces.IInventoryManager.AddItem(ItemData item) => AddItemInternal(item, 1);
        bool Nytherion.Interfaces.IInventoryManager.AddItem(ItemData item, int count) => AddItemInternal(item, count);

        /// <summary>
        /// 퀵슬롯에 아이템을 등록합니다.
        /// </summary>
        public void RegisterQuickSlot(QuickSlotUI quickSlot, ItemData item, int count, Action<ItemData, int> onUseCallback = null)
        {
            if (quickSlot == null || item == null || count <= 0) 
            {
                Debug.LogWarning("[Inventory] Invalid quick slot registration attempt");
                return;
            }

            // 기존 콜백 제거
            if (quickSlotCallbacks.TryGetValue(quickSlot, out var oldCallback))
            {
                quickSlot.OnItemUsed -= oldCallback;
                quickSlotCallbacks.Remove(quickSlot);
            }

            // 새 콜백 등록 (제공된 콜백이 없으면 기본 동작 사용)
            Action<ItemData, int> onUsed = onUseCallback ?? ((usedItem, usedCount) => 
            {
                if (RemoveItem(usedItem, usedCount))
                {
                    Debug.Log($"[Inventory] Used {usedCount}x {usedItem.itemName} from quick slot");
                }
            });

            quickSlotCallbacks[quickSlot] = onUsed;
            quickSlot.OnItemUsed += onUsed;
            quickSlot.SetItem(item, count, onUsed);
        }

        /// <summary>
        /// 인벤토리 상태를 JSON으로 저장합니다.
        /// </summary>
        public string SaveToJson()
        {
            var state = new InventoryState
            {
                items = items.Select(pair => new ItemSaveData 
                { 
                    itemId = pair.Key.ID, 
                    count = pair.Value 
                }).ToList()
            };
            return state.ToJson();
        }

        /// <summary>
        /// JSON에서 인벤토리 상태를 복원합니다.
        /// </summary>
        public void LoadFromJson(string json)
        {
            var state = InventoryState.FromJson(json);
            if (state == null) 
            {
                Debug.LogError("[Inventory] Failed to load inventory state from JSON");
                return;
            }
            
            ClearInventory();
            
            foreach (var itemData in state.items)
            {
                if (itemTable.TryGetValue(itemData.itemId, out var item))
                {
                    AddItem(item, itemData.count);
                }
                else
                {
                    Debug.LogWarning($"[Inventory] Item not found: {itemData.itemId}");
                }
            }
            
            Debug.Log($"[Inventory] Loaded {state.items.Count} items from save data");
        }

        // Public method for direct access
        public bool AddItem(ItemData item, int count = 1) => AddItemInternal(item, count);
        
        /// <summary>
        /// 인벤토리에서 아이템을 제거합니다.
        /// </summary>
        public bool RemoveItem(ItemData item, int count = 1)
        {
            if (item == null || count <= 0 || !items.ContainsKey(item)) 
                return false;

            int currentCount = items[item];

            if (currentCount > count)
            {
                // 일부 제거
                items[item] = currentCount - count;
                OnItemRemoved?.Invoke(item, count);
            }
            else
            {
                // 전체 제거
                items.Remove(item);
                OnItemRemoved?.Invoke(item, currentCount);
            }

            RequestSlotsUpdate();
            OnInventoryUpdated?.Invoke();
            return true;
        }

        private bool AddItemInternal(ItemData item, int count)
        {
            if (item == null || count <= 0) return false;

            // 이미 동일한 아이템이 있는 경우
            if (items.TryGetValue(item, out int currentCount))
            {
                // 스택 가능한 아이템이면 수량 추가
                if (item.isStackable)
                {
                    items[item] = currentCount + count;
                    UpdateSlotsUI();
                    OnItemAdded?.Invoke(item, count);
                    OnInventoryUpdated?.Invoke();
                    return true;
                }
                // 스택 불가능한 아이템이고, 아이템이 이미 있으면 실패 (중복 불가)
                return false;
            }

            
            // 인벤토리 공간 확인 (새로운 아이템만 공간 체크)
            if (items.Count >= maxSlotCount)
            {
                Debug.LogWarning($"[Inventory] 인벤토리가 가득 찼습니다. {item.itemName}을 추가할 수 없습니다.");
                return false;
            }

            // 새 아이템 추가
            items[item] = count;
            OnItemAdded?.Invoke(item, count);
            RequestSlotsUpdate();
            OnInventoryUpdated?.Invoke();
            return true;
        }
        
        bool Nytherion.Interfaces.IInventoryManager.RemoveItem(ItemData item) => RemoveItem(item, 1);
        bool Nytherion.Interfaces.IInventoryManager.RemoveItem(ItemData item, int count) => RemoveItem(item, count);
        bool Nytherion.Interfaces.IInventoryManager.HasItem(ItemData item) => items.ContainsKey(item);
        bool Nytherion.Interfaces.IInventoryManager.HasItem(string itemId) => items.Keys.Any(item => item.ID == itemId);
        int Nytherion.Interfaces.IInventoryManager.GetItemCount(ItemData item) => items.TryGetValue(item, out var count) ? count : 0;
        void Nytherion.Interfaces.IInventoryManager.ClearInventory() => ClearInventory();
        void Nytherion.Interfaces.IInventoryManager.SwapItems(InventorySlotUI fromSlot, InventorySlotUI toSlot) => SwapItems(fromSlot, toSlot);
        bool Nytherion.Interfaces.IInventoryManager.IsFull => IsFull;
        public static InventoryManager Instance { get; private set; }
        
        [System.Serializable]
        public class ItemSaveData
        {
            public string itemId;
            public int count;
        }

        [System.Serializable]
        public class InventoryState
        {
            public List<ItemSaveData> items = new List<ItemSaveData>();
            public int version = 1;
            
            public string ToJson() => JsonUtility.ToJson(this);
            public static InventoryState FromJson(string json) => JsonUtility.FromJson<InventoryState>(json);
        }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                InitializeSlots();
            }
            else
            {
                Destroy(gameObject);
            }
        }
        private void Start()
        {
            LoadItemTable();
            if (testItemData != null)
            {
                AddItem(testItemData, testItemCount);
            }
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
            var allItems = Resources.LoadAll<ItemData>("Items");
            itemTable = allItems.ToDictionary(item => item.ID, item => item);
            Debug.Log($"[Inventory] Loaded {itemTable.Count} items");
        }
        private void InitializeSlots()
        {
            // 기존 슬롯 정리
            foreach (Transform child in slotParent)
            {
                Destroy(child.gameObject);
            }
            slotPool.Clear();
            items.Clear();

            // 새 슬롯 생성 및 활성화
            for (int i = 0; i < maxSlotCount; i++)
            {
                var slotObj = Instantiate(slotPrefab, slotParent);
                slotObj.SetActive(true); // 프리팹이 비활성화 상태여도 활성화
                
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
            // 모든 슬롯 초기화 및 활성화
            foreach (var slot in slotPool)
            {
                slot.ClearSlot();
                slot.gameObject.SetActive(true); // 모든 슬롯 항상 활성화
            }

            // 아이템으로 슬롯 채우기
            int slotIndex = 0;
            foreach (var itemPair in items)
            {
                if (slotIndex >= slotPool.Count) break;
                slotPool[slotIndex].SetItem(itemPair.Key, itemPair.Value);
                slotIndex++;
            }
        }

        private void UpdateSlotsUI()
        {
            // 모든 슬롯 초기화
            foreach (var slot in slotPool)
            {
                slot.ClearSlot();
            }

            // 아이템으로 슬롯 채우기
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

            // 실제 데이터 스왑
            var fromItem = fromSlot.Item;
            var toItem = toSlot.Item;
            var fromCount = fromSlot.StackCount;
            var toCount = toSlot.StackCount;

            // 데이터 업데이트
            if (fromItem == toItem && fromItem.isStackable)
            {
                // 같은 아이템 스택 합치기
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
                // 서로 다른 아이템 교환
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
            ForceUpdateSlotsUI(); // 모든 슬롯 클리어 후 표시
            OnInventoryUpdated?.Invoke();
        }

        public bool IsFull => items.Count >= maxSlotCount;
    }

}
