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
        private ObjectPoolManager objectPoolManager;

        [Inject]
        public void Construct(IObjectResolver container, GameSceneUIRefs gameSceneuiRefs, ObjectPoolManager objectPoolManager)
        {
            this.container = container;
            this.gameSceneuiRefs = gameSceneuiRefs;
            this.objectPoolManager = objectPoolManager;
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

            // 기존 슬롯들을 풀로 반환
            foreach (Transform child in slotParent)
            {
                if (slotPrefab != null)
                {
                    objectPoolManager.ReturnToPool(slotPrefab.name, child.gameObject);
                }
                else
                {
                    Destroy(child.gameObject);
                }
            }
            slotPool.Clear();

            for (int i = 0; i < manager.MaxSlotCount; i++)
            {
                if (slotPrefab != null)
                {
                    // 오브젝트 풀에서 슬롯 가져오기
                    var slotObj = objectPoolManager.SpawnFromPool(slotPrefab, Vector3.zero, Quaternion.identity);
                    slotObj.transform.SetParent(slotParent, false);
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