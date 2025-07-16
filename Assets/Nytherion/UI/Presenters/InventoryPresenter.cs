using System.Collections.Generic;
using UnityEngine;
using Nytherion.Core.Managers;
using Nytherion.UI.Inventory;

namespace Nytherion.UI.Presenters
{
    public class InventoryPresenter : MonoBehaviour
    {
        [Header("UI Settings")]
        [SerializeField] private Transform slotParent;
        [SerializeField] private GameObject slotPrefab;

        private List<InventorySlotUI> slotPool = new List<InventorySlotUI>();

        public void Initialize()
        {
            if (InventoryManager.Instance == null) return;

            InitializeSlots(InventoryManager.Instance.MaxSlotCount);

            InventoryManager.Instance.OnInventoryUpdated += UpdateSlotsUI;
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
            if (InventoryManager.Instance == null || slotPool == null) return;
            
            for (int i = 0; i < slotPool.Count; i++)
            {
                 var (item, count) = InventoryManager.Instance.GetItemAt(i);
                 slotPool[i].SetItem(item, count);
            }
        }
    }
}