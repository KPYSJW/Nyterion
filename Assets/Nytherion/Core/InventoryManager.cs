using System;
using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using Nytherion.Data.ScriptableObjects.Items;
using Nytherion.Interfaces;
using Nytherion.UI.Inventory;
using Nytherion.Utils;


namespace Nytherion.Core
{
    [System.Serializable]
    public class ItemAddedEvent : UnityEvent<ItemData> { }

    [System.Serializable]
    public class InventoryUpdatedEvent : UnityEvent { }

    [RequireComponent(typeof(InventoryUI))]
    public class InventoryManager : MonoBehaviour, IInventoryManager
    {
        public bool AddItem(ItemData item) => AddItem(item, 1);
        public static InventoryManager Instance;

        [SerializeField] private Transform slotParent;
        [SerializeField] private GameObject slotPrefab;

        private List<InventorySlotUI> slotPool = new List<InventorySlotUI>();
        private List<ItemData> items = new List<ItemData>();

        public event Action<ItemData> OnItemAdded;
        public event Action OnInventoryUpdated;

        private readonly ItemAddedEvent _onItemAdded = new ItemAddedEvent();
        private readonly InventoryUpdatedEvent _onInventoryUpdated = new InventoryUpdatedEvent();


        [SerializeField] private ItemData initialItem;
        [SerializeField] private int initialCount = 1;

        private IEnumerator Start()
        {
            yield return null; // 슬롯 초기화가 먼저 되도록 한 프레임 대기

            if (initialItem != null)
            {
                for (int i = 0; i < initialCount; i++)
                {
                    AddItem(initialItem);
                }
            }
        }
        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
                return;
            }

            if (slotPrefab == null || slotParent == null)
            {
                InventoryLogger.LogError("slotPrefab or slotParent is not assigned.");
                return;
            }

            _onItemAdded.AddListener(item => OnItemAdded?.Invoke(item));
            _onInventoryUpdated.AddListener(() => OnInventoryUpdated?.Invoke());

            InitializeSlots();
        }

        private void InitializeSlots()
        {
            for (int i = 0; i < 24; i++)
            {
                GameObject slotObj = Instantiate(slotPrefab, slotParent);

                if (!slotObj.TryGetComponent(out InventorySlotUI slot))
                {
                    InventoryLogger.LogError("slotPrefab is missing InventorySlotUI component.");
                    continue;
                }

                slot.ClearSlot();
                slotPool.Add(slot);
            }
        }

        public bool AddItem(ItemData newItem, int count = 1)
        {
            foreach (var slot in slotPool)
            {
                if (slot.HasItem(newItem))
                {
                    slot.IncreaseCount(count);
                    _onItemAdded?.Invoke(newItem);
                    _onInventoryUpdated?.Invoke();
                    return true;
                }
            }

            foreach (var slot in slotPool)
            {
                if (slot.IsEmpty)
                {
                    slot.SetItem(newItem, count);
                    items.Add(newItem);
                    _onItemAdded?.Invoke(newItem);
                    _onInventoryUpdated?.Invoke();
                    return true;
                }
            }

            InventoryLogger.LogWarning("Inventory is full. Cannot add item.");
            return false;
        }

        public bool RemoveItem(ItemData item)
        {
            return RemoveItem(item, 1);
        }

        public bool RemoveItem(ItemData itemToRemove, int count)
        {
            int removed = 0;
            foreach (var slot in slotPool)
            {
                if (slot.HasItem(itemToRemove))
                {
                    int toRemove = Mathf.Min(count - removed, slot.StackCount);
                    slot.DecreaseCount(toRemove);
                    removed += toRemove;

                    if (slot.StackCount <= 0) slot.ClearSlot();

                    if (removed >= count) break;
                }
            }

            _onInventoryUpdated?.Invoke();
            return removed == count;
        }

        public void SwapItems(InventorySlotUI fromSlot, InventorySlotUI toSlot)
        {
            if (fromSlot == null || toSlot == null || fromSlot.IsEmpty)
                return;

            if (!toSlot.IsEmpty && fromSlot.Item == toSlot.Item && fromSlot.Item.isStackable)
            {
                int total = fromSlot.StackCount + toSlot.StackCount;
                if (total <= fromSlot.Item.maxStack)
                {
                    toSlot.SetItem(fromSlot.Item, total);
                    fromSlot.ClearSlot();
                }
                else
                {
                    int remaining = total - fromSlot.Item.maxStack;
                    toSlot.SetItem(fromSlot.Item, fromSlot.Item.maxStack);
                    fromSlot.SetItem(fromSlot.Item, remaining);
                }
            }
            else
            {
                if (fromSlot.Item == null || toSlot == null)
                {
                    InventoryLogger.LogError("SwapItems failed: null ItemData in fromSlot or toSlot");
                    return;
                }

                ItemData tempItem = toSlot.Item;
                int tempCount = toSlot.StackCount;

                if (fromSlot.Item != null && fromSlot.StackCount > 0)
                {
                    toSlot.SetItem(fromSlot.Item, fromSlot.StackCount);
                }
                if (tempItem != null && tempCount > 0)
                {
                    fromSlot.SetItem(tempItem, tempCount);
                }
                else
                {
                    fromSlot.ClearSlot();
                }
            }

            OnInventoryUpdated?.Invoke();
        }

        public bool IsFull => !slotPool.Exists(slot => slot.IsEmpty);

        public void ClearInventory()
        {
            foreach (var slot in slotPool)
            {
                slot.ClearSlot();
            }
            items.Clear();
            _onInventoryUpdated?.Invoke();
        }

        public bool HasItem(ItemData item) => items.Contains(item);

        public bool HasItem(string itemId) => items.Exists(item => item.ID == itemId);

        public int GetItemCount(ItemData item) => items.FindAll(x => x == item).Count;
    }
}
