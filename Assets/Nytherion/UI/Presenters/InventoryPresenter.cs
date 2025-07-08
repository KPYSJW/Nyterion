using System.Collections.Generic;
using UnityEngine;
using Nytherion.Core.Systems;
using Nytherion.Core.Managers;
using Nytherion.UI.Inventory;
using Nytherion.Data.ScriptableObjects.Items;

namespace Nytherion.UI.Presenters
{
    public class InventoryPresenter : MonoBehaviour
    {
        [Header("UI Settings")]
        [SerializeField] private Transform slotParent;
        [SerializeField] private GameObject slotPrefab;

        private List<InventorySlotUI> slotPool = new List<InventorySlotUI>();
        private InventoryModel inventoryModel;

        public void Initialize()
        {
            if (InventoryManager.Instance == null) return;
            inventoryModel = InventoryManager.Instance.InventoryModel;

            InitializeSlots(InventoryManager.Instance.MaxSlotCount);

            if (InventoryManager.Instance != null)
            {
                InventoryManager.Instance.OnInventoryUpdated += UpdateSlotsUI;
            }
            UpdateSlotsUI();
        }

        private void OnDestroy()
        {
            if (InventoryManager.Instance != null)
            {
                InventoryManager.Instance.OnInventoryUpdated -= UpdateSlotsUI;
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
            if (InventoryManager.Instance == null) return;

            foreach (var slot in slotPool)
            {
                slot.ClearSlot();
            }
            Dictionary<ItemData, int> itemsToDisplay = InventoryManager.Instance.GetAllItems();

            int slotIndex = 0;
            foreach (var itemPair in itemsToDisplay)
            {
                if (slotIndex < slotPool.Count)
                {
                    slotPool[slotIndex].SetItem(itemPair.Key, itemPair.Value);
                    slotIndex++;
                }
                else break;
            }
        }
    }
}