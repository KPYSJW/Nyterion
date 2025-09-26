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
        [SerializeField] private Transform slotParent;

        [SerializeField] private GameObject slotPrefab;

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
        }

        private InventoryDataManager GetInventoryDataManager()
        {
            if (inventoryDataManager == null && container != null)
            {
                try
                {
                    inventoryDataManager = container.Resolve<InventoryDataManager>();
                    Debug.Log("[InventoryPresenter] Successfully resolved InventoryDataManager");
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

        // Cleanup method to be called when the inventory is closed or destroyed
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
            Debug.Log($"[InventoryPresenter] 인벤토리 데이터 변경 이벤트 수신: {changeData.changeType}, 아이템: {changeData.itemId}");
            UpdateSlotsUI();
        }

        private void UpdateSlotsUI()
        {
            Debug.Log("[InventoryPresenter] UpdateSlotsUI 호출");
            InventoryDataManager manager = GetInventoryDataManager();
            if (manager == null || slotPool == null)
            {
                Debug.LogWarning($"[InventoryPresenter] UpdateSlotsUI 실패 - manager: {manager?.GetType().Name ?? "null"}, slotPool: {slotPool?.Count ?? 0}");
                return;
            }

            Debug.Log($"[InventoryPresenter] 슬롯 업데이트 시작 - 슬롯 수: {slotPool.Count}");

            for (int i = 0; i < slotPool.Count; i++)
            {
                var (item, count) = manager.GetSlot(i);
                if (item != null)
                {
                    Debug.Log($"[InventoryPresenter] 슬롯 {i}: {item.name} x{count}");
                }
                slotPool[i].SetItem(item, count);
            }
        }
    }
}