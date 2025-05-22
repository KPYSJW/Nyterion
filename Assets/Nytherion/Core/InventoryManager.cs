using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;
using Nytherion.Data.ScriptableObjects.Items;
using Nytherion.Core.Interfaces;
using Nytherion.UI.Inventory;
using Nytherion.Services;

namespace Nytherion.Core
{

    [RequireComponent(typeof(InventoryUI))]
    public class InventoryManager : MonoBehaviour, IInventoryManager
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
        private Dictionary<string, ItemData> itemTable = new Dictionary<string, ItemData>();
        private bool needsRefresh;
        private float lastRefreshTime;
        private const float MIN_REFRESH_INTERVAL = 0.1f; // 100ms
        
        // 저장/로드 관련
        private IInventorySaveService saveService;
        private const float SAVE_DELAY = 2f; // 자동 저장 지연 시간(초)
        private bool isScheduledForSave = false;

        // 인벤토리 상태 이벤트
        public event Action<ItemData, int> OnItemAdded;
        public event Action<ItemData, int> OnItemRemoved;
        public event Action OnInventoryUpdated;

        // IInventoryManager 인터페이스 구현
        public bool AddItem(ItemData item) => AddItem(item, 1);

        public bool AddItem(ItemData item, int count)
        {
            if (item == null || count <= 0) return false;

            // 이미 동일한 아이템이 있는 경우
            if (items.TryGetValue(item, out int currentCount))
            {
                // 스택 가능한 아이템이면 수량 추가
                if (item.isStackable)
                {
                    items[item] = currentCount + count;
                    NotifyItemModified(item, count, true);
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
            NotifyItemModified(item, count, true);
            return true;
        }

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
            var state = new Nytherion.UI.Inventory.InventoryState(items.Keys);
            return JsonUtility.ToJson(state);
        }

        /// <summary>
        /// JSON에서 인벤토리 상태를 복원합니다.
        /// </summary>
        public void LoadFromJson(string json)
        {
            var state = JsonUtility.FromJson<Nytherion.UI.Inventory.InventoryState>(json);
            if (state == null)
            {
                Debug.LogError("[Inventory] Failed to load inventory state from JSON");
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
                else
                {
                    Debug.LogWarning($"[Inventory] Item not found: {item.ItemId}");
                }
            }

            Debug.Log($"[Inventory] Loaded {state.Items.Count} items from save data");
        }

        /// <summary>
        /// 인벤토리에서 아이템을 제거합니다.
        /// </summary>
        public bool RemoveItem(ItemData item) => RemoveItem(item, 1);

        public bool RemoveItem(ItemData item, int count)
        {
            if (item == null || count <= 0 || !items.ContainsKey(item))
                return false;

            int currentCount = items[item];
            bool isRemoved = false;

            if (currentCount > count)
            {
                // 일부 제거
                items[item] = currentCount - count;
                NotifyItemModified(item, count, false);
                isRemoved = true;
            }
            else if (currentCount == count)
            {
                // 전체 제거
                items.Remove(item);
                NotifyItemModified(item, currentCount, false);
                isRemoved = true;
            }

            return isRemoved;
        }

        public bool HasItem(ItemData item) => items.ContainsKey(item);
        public bool HasItem(string itemId) => items.Keys.Any(item => item.ID == itemId);
        public int GetItemCount(ItemData item) => items.TryGetValue(item, out var count) ? count : 0;
        public bool IsFull => items.Count >= maxSlotCount;

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
            
            // 인벤토리 업데이트 이벤트 발생
            OnInventoryUpdated?.Invoke();
            
            // 변경 사항 저장 예약 (자동 저장)
            ScheduleSave();
        }

        /// <summary>
        /// 아이템 제거 알림을 처리합니다.
        /// </summary>
        private void NotifyItemRemoved(ItemData item, int count)
        {
            RequestSlotsUpdate();
            OnItemRemoved?.Invoke(item, count);
            OnInventoryUpdated?.Invoke();
        }

        public static InventoryManager Instance { get; private set; }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                LoadItemTable();
                InitializeSlots();
                InitializeSaveSystem(); // 저장 시스템 초기화
                LoadInventory(); // 게임 시작 시 인벤토리 자동 로드
            }
            else
            {
                Destroy(gameObject);
            }
        }
        private void Start()
        {
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
            UpdateSlotsUI(true); // forceActivate를 true로 설정하여 호출
        }

        private void UpdateSlotsUI(bool forceActivate = false)
        {
            // 모든 슬롯 초기화 및 필요시 활성화
            foreach (var slot in slotPool)
            {
                slot.ClearSlot();
                if (forceActivate)
                {
                    slot.gameObject.SetActive(true);
                }
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
            var itemsToRemove = items.Keys.ToList();
            foreach (var item in itemsToRemove)
            {
                RemoveItem(item, items[item]);
            }

            // 슬롯 업데이트
            ForceUpdateSlotsUI();
        }

        // 저장/로드 관련 메서드
        private void InitializeSaveSystem()
        {
            try
            {
                // 저장 서비스 초기화
                saveService = new PlayerPrefsInventorySaveService();
                Debug.Log("[Inventory] 저장 시스템 초기화 완료");
            }
            catch (Exception e)
            {
                Debug.LogError($"[Inventory] 저장 시스템 초기화 실패: {e.Message}");
            }
        }
        
        public void SaveInventory()
        {
            if (saveService == null)
            {
                Debug.LogWarning("[Inventory] Save system is not initialized");
                return;
            }

            try
            {
                // Use the constructor that takes Dictionary<ItemData, int>
                var state = new InventoryState(items);
                saveService.SaveInventory(state);
                Debug.Log("[Inventory] Inventory saved successfully");
            }
            catch (Exception e)
            {
                Debug.LogError($"[Inventory] Error saving inventory: {e.Message}");
            }
        }
        
        public void LoadInventory()
        {
            var backupItems = new Dictionary<ItemData, int>(items);
            
            try
            {
                var state = saveService.LoadInventory();
                if (state == null || state.Items == null)
                {
                    Debug.Log("[Inventory] No saved inventory data found");
                    items.Clear();
                    OnInventoryUpdated?.Invoke();
                    return;
                }


                var newItems = new Dictionary<ItemData, int>();
                bool hasError = false;

                foreach (var entry in state.Items)
                {
                    if (string.IsNullOrEmpty(entry.ItemId))
                    {
                        Debug.LogWarning("[Inventory] Invalid item ID (null or empty)");
                        hasError = true;
                        continue;
                    }


                    if (entry.Count <= 0)
                    {
                        Debug.LogWarning($"[Inventory] Invalid count for item {entry.ItemId}: {entry.Count}");
                        hasError = true;
                        continue;
                    }


                    ItemData item = itemTable.Values.FirstOrDefault(i => i.ID == entry.ItemId);
                    if (item != null)
                    {
                        newItems[item] = entry.Count;
                    }
                    else
                    {
                        Debug.LogWarning($"[Inventory] Item not found in database: {entry.ItemId}");
                        hasError = true;
                    }
                }


                if (hasError)
                {
                    Debug.LogWarning("[Inventory] Some items may not have loaded correctly");
                }
                else
                {
                    Debug.Log("[Inventory] All items loaded successfully");
                }


                items = newItems;
                Debug.Log($"[Inventory] Loaded {items.Count} items");
                OnInventoryUpdated?.Invoke();
            }
            catch (Exception e)
            {
                Debug.LogError($"[Inventory] Error loading inventory: {e.Message}");
                // Restore from backup on error
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
            SaveInventory();
            isScheduledForSave = false;
        }
        
        private void OnDestroy()
        {
            if (Instance == this)
            {
                // 게임 종료 시 인벤토리 저장
                SaveInventory();
            }
        }

    }

}
