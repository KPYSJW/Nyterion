using System.Collections.Generic;
using UnityEngine;
using Nytherion.Core.Managers;
using Nytherion.Core.Interfaces;
using Nytherion.UI.Inventory;
using VContainer;

namespace Nytherion.UI.Presenters
{
    public class InventoryPresenter : MonoBehaviour
    {
        [Header("UI Settings")]
        private Transform slotParent;

        private GameObject slotPrefab;

        private List<InventorySlotUI> slotPool = new List<InventorySlotUI>();
        private InventoryDataManager inventoryDataManager;
        private bool isInitialized = false;
        private IObjectResolver container;
        private GameSceneUIRefs gameSceneuiRefs;

        [Inject]
        public void Construct(IObjectResolver container, GameSceneUIRefs gameSceneuiRefs)
        {
            this.container = container;
            this.gameSceneuiRefs = gameSceneuiRefs;
            this.slotParent = gameSceneuiRefs.InventorySlotParent;
            this.slotPrefab = gameSceneuiRefs.InventorySlotPrefab;
        }

        private InventoryDataManager GetInventoryDataManager()
        {
            if (inventoryDataManager == null && container != null)
            {
                try
                {
                    inventoryDataManager = container.Resolve<InventoryDataManager>();
                }
                catch (VContainerException e)
                {
                    Debug.LogError($"[InventoryPresenter] Failed to resolve InventoryDataManager: {e.Message}");
                    return null;
                }
            }
            return inventoryDataManager;
        }

        public void Initialize()
        {
            InventoryDataManager manager = GetInventoryDataManager();
            if (manager == null || isInitialized || slotParent == null)
            {
                if (slotParent == null) Debug.LogError("[InventoryPresenter] slotParent가 null입니다. GameSceneUIRefs를 확인하세요.");
                return;
            }

            foreach (Transform child in slotParent)
            {
                Destroy(child.gameObject);
            }
            slotPool.Clear();

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

            manager.OnDataChanged += OnInventoryDataChanged;
            UpdateSlotsUI();
            isInitialized = true;
        }

        private void OnDestroy()
        {
            InventoryDataManager manager = GetInventoryDataManager();
            if (manager != null)
            {
                manager.OnDataChanged -= OnInventoryDataChanged;
            }
        }

        public void Cleanup()
        {
            InventoryDataManager manager = GetInventoryDataManager();
            if (manager != null)
            {
                manager.OnDataChanged -= OnInventoryDataChanged;
            }
            isInitialized = false;
        }

        private void OnInventoryDataChanged(InventoryChangeData changeData)
        {
            UpdateSlotsUI();
        }

        private void UpdateSlotsUI()
        {
            InventoryDataManager manager = GetInventoryDataManager();
            if (manager == null || slotPool == null)
            {
                Debug.LogWarning($"[InventoryPresenter] UpdateSlotsUI 실패 - manager: {manager?.GetType().Name ?? "null"}, slotPool: {slotPool?.Count ?? 0}");
                return;
            }

            for (int i = 0; i < slotPool.Count; i++)
            {
                var (item, count) = manager.GetSlot(i);
                slotPool[i].SetItem(item, count);
            }
        }
    }
}