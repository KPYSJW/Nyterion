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
        public bool IsInitialized => isInitialized;
        private IObjectResolver container;
        private ObjectPoolManager objectPoolManager;

        [Inject]
        public void Construct(IObjectResolver container, GameSceneUIRefs gameSceneuiRefs, ObjectPoolManager objectPoolManager)
        {
            this.container = container;
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

            // 프리팹이 없는 수동 테스트 UI는 배치된 슬롯을 그대로 사용한다.
            if (slotPrefab == null)
            {
                slotPool = new List<InventorySlotUI>(slotParent.GetComponentsInChildren<InventorySlotUI>(true));
                for (int i = 0; i < slotPool.Count; i++) slotPool[i].Initialize(i);
            }
            else
            {
                for (int i = slotParent.childCount - 1; i >= 0; i--)
                    objectPoolManager.ReturnToPool(slotPrefab.name, slotParent.GetChild(i).gameObject);
                slotPool.Clear();
            }

            EnsureSlotPoolCapacity(manager.MaxSlotCount);

            manager.OnDataChanged += OnInventoryDataChanged;
            UpdateSlotsUI();
            isInitialized = true;
        }

        private void EnsureSlotPoolCapacity(int requiredSlots)
        {
            for (int i = slotPool.Count; i < requiredSlots; i++)
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

        }

        private void OnDestroy()
        {
            // 파괴 순서에 따라 참조가 먼저 사라져도 새 매니저를 Resolve하지 않는다.
            if (inventoryDataManager != null)
            {
                inventoryDataManager.OnDataChanged -= OnInventoryDataChanged;
            }
        }

        public void Cleanup()
        {
            if (inventoryDataManager != null)
            {
                inventoryDataManager.OnDataChanged -= OnInventoryDataChanged;
            }
            isInitialized = false;
        }

        private void OnInventoryDataChanged(InventoryChangeData changeData)
        {
            EnsureSlotPoolCapacity(inventoryDataManager.MaxSlotCount);
            if (changeData.slotIndex < 0)
            {
                UpdateSlotsUI();
                return;
            }
            UpdateSlotUI(changeData.slotIndex);
            if (changeData.changeType == InventoryChangeType.SlotsSwapped)
                UpdateSlotUI(changeData.otherSlotIndex);
        }

        public void RefreshUI()
        {
            if (!isInitialized) Initialize();
            else UpdateSlotsUI();
        }

        private void UpdateSlotUI(int index)
        {
            if (index < 0 || index >= slotPool.Count) return;
            var (item, count) = inventoryDataManager.GetSlot(index);
            InventorySlotUI slot = slotPool[index];
            // 전체 갱신에서도 내용이 같은 슬롯은 다시 그리지 않는다.
            if (slot.CurrentItem != item || slot.CurrentCount != count) slot.SetItem(item, count);
        }

        private void UpdateSlotsUI()
        {
            InventoryDataManager manager = GetInventoryDataManager();
            if (manager == null || slotPool == null)
            {
                Debug.LogWarning($"[InventoryPresenter] UpdateSlotsUI 실패 - manager: {manager?.GetType().Name ?? "null"}, slotPool: {slotPool?.Count ?? 0}");
                return;
            }

            EnsureSlotPoolCapacity(manager.MaxSlotCount);

            for (int i = 0; i < slotPool.Count; i++)
            {
                slotPool[i].gameObject.SetActive(i < manager.MaxSlotCount);
                UpdateSlotUI(i);
            }
        }
    }
}
