using System.Collections.Generic;
using UnityEngine;
using Nytherion.Core;

namespace Nytherion.UI.Inventory
{
    public class InventoryPresenter : MonoBehaviour
    {
        [Header("UI Settings")]
        [SerializeField] private Transform slotParent;
        [SerializeField] private GameObject slotPrefab;

        private List<InventorySlotUI> slotPool = new();
        private InventoryModel inventoryModel;

        public void Initialize()
        {
            if (InventoryManager.Instance == null) return;
            inventoryModel = InventoryManager.Instance.InventoryModel;

            InitializeSlots(InventoryManager.Instance.MaxSlotCount);

            inventoryModel.OnInventoryUpdated += UpdateSlotsUI;
            UpdateSlotsUI();
        }

        private void OnDestroy()
        {
            if (inventoryModel != null)
            {
                inventoryModel.OnInventoryUpdated -= UpdateSlotsUI;
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
            if (inventoryModel == null) return;

            foreach (var slot in slotPool)
            {
                slot.ClearSlot();
            }

            int slotIndex = 0;
            foreach (var itemPair in inventoryModel.Items)
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