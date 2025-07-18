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

        [Inject]
        public void Construct(InventoryManager manager)
        {
            inventoryManager = manager;
        }

        public void Initialize()
        {
            if (inventoryManager == null) return;

            InitializeSlots(inventoryManager.MaxSlotCount);

            inventoryManager.OnInventoryUpdated += UpdateSlotsUI;
            UpdateSlotsUI();
        }

        private void OnDestroy()
        {
            if (inventoryManager != null)
            {
                inventoryManager.OnInventoryUpdated -= UpdateSlotsUI;
            }
        }

        private void InitializeSlots(int slotCount)
        {
            foreach (Transform child in slotParent)
            {
                Destroy(child.gameObject);
            }
            slotPool.Clear();

            for (int i = 0; i < slotCount; i++)
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