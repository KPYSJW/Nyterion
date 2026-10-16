using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using Nytherion.Core.Managers;
using Nytherion.UI.Components;
using Nytherion.UI.Inventory;
using Nytherion.UI.Presenters;
using VContainer;
using VContainer.Unity;

namespace Nytherion.UI.Controllers
{
    public class InventoryUI : UIPanelBase, IInitializable
    {

        [Header("Input")]
        [SerializeField] private InputActionReference toggleInventoryAction;
        [SerializeField] private GameSceneUIRefs gameSceneuiRefs;

        private bool isInitialized;
        public bool IsInitialized => isInitialized;
        protected override bool UsesBookOpenSound => true;

        public event Action<bool> OnInventoryToggled;

        [Header("Managers")]
        private InventoryDataManager inventoryDataManager;
        private EventManager eventManager;
        private ShopManager shopManager;
        private GameObject equipmentPanel;
        private GameObject statsPanel;
        private Button closeButton;
        private Button sortButton;
        private Button dismantleButton;
        private InventoryDismantlePanel dismantlePanel;
        public bool IsDismantleOpen => dismantlePanel != null && dismantlePanel.IsOpen;
        private InventoryPresenter inventoryPresenter;
        private IObjectResolver container;

        [Inject]
        public void Construct(IObjectResolver container,
            GameSceneUIRefs gameSceneuiRefs)
        {
            this.container = container;
            this.gameSceneuiRefs = gameSceneuiRefs;
            this.equipmentPanel = gameSceneuiRefs.EquipmentPanel;
            this.statsPanel = gameSceneuiRefs.StatsPanel;
            this.closeButton = gameSceneuiRefs.InventoryCloseButton;
            this.sortButton = gameSceneuiRefs.InventorySortButton;
            this.dismantleButton = gameSceneuiRefs.InventoryDismantleButton;
            this.dismantlePanel = gameSceneuiRefs.DismantlePanel;
            this.controlledCanvasGroup = gameSceneuiRefs.InventoryCanvasGroup;
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
                    Debug.LogError($"[InventoryUI] Failed to resolve InventoryDataManager: {e.Message}");
                    return null;
                }
            }
            return inventoryDataManager;
        }

        private EventManager GetEventManager()
        {
            if (eventManager == null && container != null)
            {
                try
                {
                    eventManager = container.Resolve<EventManager>();
                }
                catch (VContainerException e)
                {
                    Debug.LogError($"[InventoryUI] Failed to resolve EventManager: {e.Message}");
                    return null;
                }
            }
            return eventManager;
        }

        private ShopManager GetShopManager()
        {
            if (shopManager == null && container != null)
            {
                try
                {
                    shopManager = container.Resolve<ShopManager>();
                }
                catch (VContainerException e)
                {
                    Debug.LogError($"[InventoryUI] Failed to resolve ShopManager: {e.Message}");
                    return null;
                }
            }
            return shopManager;
        }
        public void Initialize()
        {
            // 수동 검증 등에서 다시 호출해도 이벤트는 한 번만 연결한다.
            if (isInitialized) return;
            FindUIElements();
            if (inventoryPresenter == null)
            {
                Debug.LogError("[InventoryUI] InventoryPresenter가 등록되지 않았습니다.");
                return;
            }
            inventoryPresenter.Initialize();
            if (!inventoryPresenter.IsInitialized) return;
            isInitialized = true;
            if (dismantlePanel != null)
                dismantlePanel.Initialize(GetInventoryDataManager(), container.Resolve<CurrencyDataManager>(),
                    equipmentPanel, statsPanel, audioManager);
            if (dismantleButton != null) dismantleButton.onClick.AddListener(ToggleDismantle);
            
            if (closeButton != null)
            {
                closeButton.onClick.AddListener(Close);
            }
            else
            {
                Debug.LogWarning("[InventoryUI] closeButton이 null입니다.");
            }

            if (sortButton != null)
            {
                sortButton.onClick.RemoveListener(SortInventory);
                sortButton.onClick.AddListener(SortInventory);
            }

            if (toggleInventoryAction != null && toggleInventoryAction.action != null)
            {
                toggleInventoryAction.action.performed += OnToggleAction;
                toggleInventoryAction.action.Enable();
            }

            EventManager eventMgr = GetEventManager();
            if (eventMgr != null)
            {
                eventMgr.OnOpenInventoryForShop += OpenForShop;
                eventMgr.OnCloseInventoryForShop += Close;
            }
            else
            {
                Debug.LogWarning("[InventoryUI] EventManager를 찾을 수 없습니다.");
            }

            Close();
        }

        private void FindUIElements()
        {
            // UI 요소들을 동적으로 찾기
            if (closeButton == null)
                closeButton = GetComponentInChildren<Button>();
            
            if (inventoryPresenter == null)
            {
                if (container != null) container.TryResolve(out inventoryPresenter);
                if (inventoryPresenter == null)
                    inventoryPresenter = GetComponentInChildren<InventoryPresenter>(true);
            }
        }

        private void SortInventory()
        {
            if (GetInventoryDataManager()?.SortItems() == true && TooltipPanel.Instance != null)
                TooltipPanel.Instance.HideTooltip();
        }

        private void ToggleDismantle()
        {
            if (IsOpen) dismantlePanel?.Toggle();
        }

        public bool TryStageForDismantle(InventorySlotUI slot)
        {
            return IsOpen && IsDismantleOpen && dismantlePanel.TryAdd(slot);
        }

        private void OnToggleAction(InputAction.CallbackContext context)
        {
            ShopManager shopMgr = GetShopManager();
            if (shopMgr != null && shopMgr.IsShopOpen)
            {
                return;
            }
            if (IsOpen)
            {
                Close();
                return;
            }
            if (!equipmentPanel.activeSelf)
            {
                equipmentPanel.SetActive(true);
            }
            if (!statsPanel.activeSelf)
            {
                statsPanel.SetActive(true);
            }
            Toggle();
        }

        protected override void OnPanelStateChanged(bool isOpen)
        {
            if (!isOpen) dismantlePanel?.Close();
            OnInventoryToggled?.Invoke(isOpen);

            if (isOpen) RefreshUI();

            if (!isOpen && TooltipPanel.Instance != null)
            {
                TooltipPanel.Instance.HideTooltip();
            }
        }

        public void OpenForShop()
        {
            dismantlePanel?.Close();
            if (equipmentPanel != null) equipmentPanel.SetActive(false);
            if (statsPanel != null) statsPanel.SetActive(false);
            Open(false); // 다른 UI(상점)를 닫지 않고 열기
        }

        public void RefreshUI()
        {
            inventoryPresenter?.RefreshUI();
        }

        private void OnDestroy()
        {
            dismantlePanel?.Close();
            if (dismantleButton != null) dismantleButton.onClick.RemoveListener(ToggleDismantle);
            if (sortButton != null) sortButton.onClick.RemoveListener(SortInventory);
            if (closeButton != null) closeButton.onClick.RemoveListener(Close);

            // 이벤트 구독 해제
            if (eventManager != null)
            {
                eventManager.OnOpenInventoryForShop -= OpenForShop;
                eventManager.OnCloseInventoryForShop -= Close;
            }

            if (toggleInventoryAction != null && toggleInventoryAction.action != null)
            {
                toggleInventoryAction.action.performed -= OnToggleAction;
            }
        }
    }
}
