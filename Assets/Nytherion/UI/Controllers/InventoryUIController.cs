using UnityEngine;
using VContainer;
using VContainer.Unity;
using Nytherion.Core.Managers;
using Nytherion.Core.Interfaces;
using Nytherion.UI.Inventory;
using System;

namespace Nytherion.UI.Controllers
{
    /// <summary>
    /// 인벤토리 UI만 담당하는 컨트롤러
    /// 데이터는 InventoryDataManager에서 가져와서 UI에 표시
    /// </summary>
    public class InventoryUIController : MonoBehaviour, IInitializable, IDisposable
    {
        [Header("UI References")]
        [SerializeField] private InventoryUI inventoryUI;
        [SerializeField] private bool autoFindInventoryUI = true;

        private InventoryDataManager inventoryDataManager;
        private EventManager eventManager;
        private GameSceneUIRefs uiRefs;
        private bool isInitialized = false;

        [Inject]
        public void Construct(InventoryDataManager inventoryDataManager, EventManager eventManager, GameSceneUIRefs uiRefs, InventoryUI inventoryUI)
        {
            this.inventoryDataManager = inventoryDataManager;
            this.eventManager = eventManager;
            this.uiRefs = uiRefs;
            this.inventoryUI = inventoryUI; // 직접 주입받은 InventoryUI 사용
        }

        public void Initialize()
        {
            if (isInitialized) return;

            // InventoryUI는 의존성 주입으로 이미 할당됨
            if (inventoryUI == null && autoFindInventoryUI)
            {
                inventoryUI = FindObjectOfType<InventoryUI>();
                Debug.LogWarning("[InventoryUIController] 의존성 주입 실패 - FindObjectOfType으로 검색");
            }

            if (inventoryUI == null)
            {
                Debug.LogWarning("[InventoryUIController] InventoryUI를 찾을 수 없습니다. UI 업데이트가 불가능합니다.");
                return;
            }

            if (inventoryDataManager == null)
            {
                Debug.LogError("[InventoryUIController] InventoryDataManager가 주입되지 않았습니다!");
                return;
            }

            // 데이터 변경 이벤트 구독
            inventoryDataManager.OnDataChanged += HandleInventoryDataChanged;

            // UI 초기화
            RefreshInventoryUI();

            isInitialized = true;
        }

        private void HandleInventoryDataChanged(InventoryChangeData changeData)
        {
            if (!isInitialized || inventoryUI == null) return;

            switch (changeData.changeType)
            {
                case InventoryChangeType.ItemAdded:
                    OnItemAdded(changeData);
                    break;

                case InventoryChangeType.ItemRemoved:
                    OnItemRemoved(changeData);
                    break;

                case InventoryChangeType.ItemCountChanged:
                    OnItemCountChanged(changeData);
                    break;

                case InventoryChangeType.SlotCleared:
                    OnSlotCleared(changeData);
                    break;

                case InventoryChangeType.InventoryLoaded:
                    RefreshInventoryUI();
                    break;
            }

            // UI 전체 업데이트 이벤트 발송
            TriggerInventoryUIUpdate();
        }

        private void OnItemAdded(InventoryChangeData changeData)
        {
            if (changeData.slotIndex >= 0)
            {
                UpdateSlotUI(changeData.slotIndex);
            }
            else
            {
                RefreshInventoryUI();
            }
        }

        private void OnItemRemoved(InventoryChangeData changeData)
        {
            if (changeData.slotIndex >= 0)
            {
                UpdateSlotUI(changeData.slotIndex);
            }
            else
            {
                RefreshInventoryUI();
            }
        }

        private void OnItemCountChanged(InventoryChangeData changeData)
        {
            UpdateSlotUI(changeData.slotIndex);
        }

        private void OnSlotCleared(InventoryChangeData changeData)
        {
            UpdateSlotUI(changeData.slotIndex);
        }

        private void UpdateSlotUI(int slotIndex)
        {
            if (inventoryUI == null || slotIndex < 0) return;

            var slot = inventoryDataManager.GetSlot(slotIndex);

            // InventoryUI에 슬롯 업데이트 요청
            // 실제 구현은 InventoryUI의 API에 따라 달라짐
            try
            {
                // inventoryUI.UpdateSlot(slotIndex, slot);
                // 또는 InventoryUI가 자체적으로 데이터를 조회하도록 할 수도 있음
                inventoryUI.RefreshUI(); // 임시로 전체 업데이트
            }
            catch (Exception ex)
            {
                Debug.LogError($"[InventoryUIController] 슬롯 UI 업데이트 실패: {ex.Message}");
            }
        }

        private void RefreshInventoryUI()
        {
            if (inventoryUI == null) return;

            try
            {
                inventoryUI.RefreshUI();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[InventoryUIController] 인벤토리 UI 새로고침 실패: {ex.Message}");
            }
        }

        private void TriggerInventoryUIUpdate()
        {
            // 다른 UI 시스템들에게 인벤토리 UI가 업데이트되었음을 알림
            eventManager?.TriggerEvent("InventoryUIUpdated", null);
        }

        /// <summary>
        /// 외부에서 UI 업데이트를 요청할 때 사용
        /// </summary>
        public void RequestUIUpdate()
        {
            if (isInitialized)
            {
                RefreshInventoryUI();
            }
        }

        /// <summary>
        /// 인벤토리 UI 표시/숨김
        /// </summary>
        public void SetInventoryUIVisible(bool visible)
        {
            if (inventoryUI == null) return;

            try
            {
                inventoryUI.gameObject.SetActive(visible);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[InventoryUIController] 인벤토리 UI 표시 상태 변경 실패: {ex.Message}");
            }
        }

        /// <summary>
        /// 현재 인벤토리 UI 상태 조회
        /// </summary>
        public bool IsInventoryUIVisible()
        {
            return inventoryUI != null && inventoryUI.gameObject.activeInHierarchy;
        }

        /// <summary>
        /// 인벤토리 UI가 준비되었는지 확인
        /// </summary>
        public bool IsUIReady()
        {
            return isInitialized && inventoryUI != null && inventoryDataManager != null;
        }

        public void Dispose()
        {
            if (inventoryDataManager != null)
            {
                inventoryDataManager.OnDataChanged -= HandleInventoryDataChanged;
            }

            isInitialized = false;
        }

        private void OnDestroy()
        {
            Dispose();
        }

        // 디버그 메서드들
        [ContextMenu("인벤토리 UI 새로고침")]
        public void DebugRefreshUI()
        {
            RequestUIUpdate();
        }

        [ContextMenu("인벤토리 UI 토글")]
        public void DebugToggleUI()
        {
            if (IsUIReady())
            {
                SetInventoryUIVisible(!IsInventoryUIVisible());
            }
        }
    }
}