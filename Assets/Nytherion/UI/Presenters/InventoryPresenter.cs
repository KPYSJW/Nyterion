using System.Collections.Generic;
using UnityEngine;
using Nytherion.Core.Managers;
using Nytherion.UI.Inventory;
using Zenject;

namespace Nytherion.UI.Presenters
{
    public class InventoryPresenter : MonoBehaviour
    {
        [Header("UI Settings")]
        [Inject(Id = "InventorySlotParent")] private Transform slotParent;

        [SerializeField] private GameObject slotPrefab;

        private List<InventorySlotUI> slotPool = new List<InventorySlotUI>();
        private InventoryManager inventoryManager;
        private bool isInitialized = false;

        [Inject]
        public void Construct(InventoryManager manager)
        {
            inventoryManager = manager;
        }

        public void Initialize()
        {
            if (inventoryManager == null || isInitialized) return;

            // Clear any existing slots
            foreach (Transform child in slotParent)
            {
                Destroy(child.gameObject);
            }
            slotPool.Clear();

            // Create new slots
            for (int i = 0; i < inventoryManager.MaxSlotCount; i++)
            {
                if (slotPrefab != null)
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

            inventoryManager.OnInventoryUpdated += UpdateSlotsUI;
            UpdateSlotsUI();
            isInitialized = true;
        }

        private void OnDestroy()
        {
            if (inventoryManager != null)
            {
                inventoryManager.OnInventoryUpdated -= UpdateSlotsUI;
            }
        }

        // Cleanup method to be called when the inventory is closed or destroyed
        public void Cleanup()
        {
            if (inventoryManager != null)
            {
                inventoryManager.OnInventoryUpdated -= UpdateSlotsUI;
            }
            isInitialized = false;
        }

        private void UpdateSlotsUI()
        {
            if (inventoryManager == null || slotPool == null) return;

            for (int i = 0; i < slotPool.Count; i++)
            {
                var (item, count) = inventoryManager.GetItemAt(i);
                slotPool[i].SetItem(item, count);
            }
        }
    }
}