using System.Collections.Generic;
using UnityEngine;
using Nytherion.Core.Managers;
using Nytherion.UI.Inventory;
using VContainer;

namespace Nytherion.UI.Presenters
{
    public class InventoryPresenter : MonoBehaviour
    {
        [Header("UI Settings")]
        [SerializeField] private Transform slotParent;

        [SerializeField] private GameObject slotPrefab;

        private List<InventorySlotUI> slotPool = new List<InventorySlotUI>();
        private InventoryManager inventoryManager;
        private bool isInitialized = false;
        private IObjectResolver container;
        private GameSceneUIRefs gameSceneuiRefs;

        [Inject]
        public void Construct(IObjectResolver container, GameSceneUIRefs gameSceneuiRefs)
        {
            this.container = container;
            this.gameSceneuiRefs = gameSceneuiRefs;
        }

        private InventoryManager GetInventoryManager()
        {
            if (inventoryManager == null && container != null)
            {
                try
                {
                    inventoryManager = container.Resolve<InventoryManager>();
                    Debug.Log("[InventoryPresenter] Successfully resolved InventoryManager");
                }
                catch (VContainerException e)
                {
                    Debug.LogError($"[InventoryPresenter] Failed to resolve InventoryManager: {e.Message}");
                    return null;
                }
            }
            return inventoryManager;
        }

        public void Initialize()
        {
            InventoryManager manager = GetInventoryManager();
            if (manager == null || isInitialized) return;

            // Clear any existing slots
            foreach (Transform child in slotParent)
            {
                Destroy(child.gameObject);
            }
            slotPool.Clear();

            // Create new slots
            for (int i = 0; i < manager.MaxSlotCount; i++)
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

            manager.OnInventoryUpdated += UpdateSlotsUI;
            UpdateSlotsUI();
            isInitialized = true;
        }

        private void OnDestroy()
        {
            InventoryManager manager = GetInventoryManager();
            if (manager != null)
            {
                manager.OnInventoryUpdated -= UpdateSlotsUI;
            }
        }

        // Cleanup method to be called when the inventory is closed or destroyed
        public void Cleanup()
        {
            InventoryManager manager = GetInventoryManager();
            if (manager != null)
            {
                manager.OnInventoryUpdated -= UpdateSlotsUI;
            }
            isInitialized = false;
        }

        private void UpdateSlotsUI()
        {
            InventoryManager manager = GetInventoryManager();
            if (manager == null || slotPool == null) return;

            for (int i = 0; i < slotPool.Count; i++)
            {
                var (item, count) = manager.GetItemAt(i);
                slotPool[i].SetItem(item, count);
            }
        }
    }
}