using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using Nytherion.Core.Managers;
using Nytherion.Core.Interfaces;
using Nytherion.UI.Components;
using Nytherion.UI.Inventory;
using Nytherion.UI.Presenters;
using System.Linq;
using VContainer;
using VContainer.Unity;

namespace Nytherion.UI.Controllers
{
    public class InventoryUI : UIPanelBase, IInitializable
    {

        [Header("Input")]
        [SerializeField] private InputActionReference toggleInventoryAction;
        [SerializeField] private GameSceneUIRefs gameSceneuiRefs;

        [Header("UI References")]
        private List<InventorySlotUI> slotPool = new List<InventorySlotUI>();
        private bool isSlotPoolInitialized = false;

        public event Action<bool> OnInventoryToggled;

        [Header("Managers")]
        private InventoryDataManager inventoryDataManager;
        private EventManager eventManager;
        private ShopManager shopManager;
        private GameObject equipmentPanel;
        private GameObject statsPanel;
        private Transform inventorySlotParent;
        private Button closeButton;
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
            this.inventorySlotParent = gameSceneuiRefs.InventorySlotParent;
            this.closeButton = gameSceneuiRefs.InventoryCloseButton;
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
        protected override void Awake()
        {
            base.Awake();

            inventoryPresenter?.Initialize();

            InitializeSlotPool();
        }

        public void Initialize()
        {
            
            FindUIElements();
            
            if (closeButton != null)
            {
                closeButton.onClick.AddListener(Close);
            }
            else
            {
                Debug.LogWarning("[InventoryUI] closeButton이 null입니다.");
            }

            InventoryDataManager inventoryDataMgr = GetInventoryDataManager();
            if (inventoryDataMgr != null)
            {
                inventoryDataMgr.OnDataChanged += OnInventoryDataChanged;
            }
            else
            {
                Debug.LogError("[InventoryUI] InventoryDataManager를 찾을 수 없습니다!");
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

            inventoryPresenter?.Initialize();
            InitializeSlotPool();

            Close();
        }

        private void FindUIElements()
        {
            // UI 요소들을 동적으로 찾기
            if (closeButton == null)
                closeButton = GetComponentInChildren<Button>();
            
            if (inventorySlotParent == null)
            {
                var slotParentGO = transform.Find("InventorySlotParent");
                if (slotParentGO != null)
                    inventorySlotParent = slotParentGO;
            }

            if (inventoryPresenter == null)
                inventoryPresenter = GetComponentInChildren<InventoryPresenter>();
        }

        private void InitializeSlotPool()
        {
            if (isSlotPoolInitialized)
            {
                return;
            }

            if (inventorySlotParent == null)
            {
                return;
            }

            // 기존 슬롯들 먼저 찾기
            slotPool = inventorySlotParent.GetComponentsInChildren<InventorySlotUI>(true).ToList();

            // 슬롯이 없고 프리팹이 설정되어 있으면 동적 생성
            if (slotPool.Count == 0 && gameSceneuiRefs?.InventorySlotPrefab != null)
            {
                CreateSlotsFromPrefab();
            }

            // 슬롯 초기화
            for (int i = 0; i < slotPool.Count; i++)
            {
                slotPool[i].Initialize(i);
            }
            
            isSlotPoolInitialized = true;
        }

        private void OnInventoryDataChanged(InventoryChangeData changeData)
        {
            RefreshUI();
        }

        private void CreateSlotsFromPrefab()
        {
            InventoryDataManager inventoryDataMgr = GetInventoryDataManager();
            int slotCount = inventoryDataMgr?.MaxSlotCount ?? 24;
            
            GameObject prefab = gameSceneuiRefs.InventorySlotPrefab;

            slotPool = new List<InventorySlotUI>();
            
            for (int i = 0; i < slotCount; i++)
            {
                GameObject slotObj = Instantiate(prefab, inventorySlotParent);
                slotObj.name = $"InventorySlot_{i}";
                
                InventorySlotUI slotUI = slotObj.GetComponent<InventorySlotUI>();
                if (slotUI != null)
                {
                    slotPool.Add(slotUI);
                }
                else
                {
                    Debug.LogWarning($"[InventoryUI] 슬롯 {i}에 InventorySlotUI 컴포넌트가 없습니다!");
                }
            }
        }

        // private void OnEnable()
        // {
        //     if (inventoryManager != null)
        //     {
        //         inventoryManager.OnInventoryUpdated += RefreshUI;
        //     }

        //     if (toggleInventoryAction != null && toggleInventoryAction.action != null)
        //     {
        //         toggleInventoryAction.action.performed += OnToggleAction;
        //         toggleInventoryAction.action.Enable();
        //     }
        //     if (eventManager != null)
        //     {
        //         eventManager.OnOpenInventoryForShop += OpenForShop;
        //         eventManager.OnCloseInventoryForShop += Close;
        //     }
        // }
        // private void OnDisable()
        // {
        //     if (toggleInventoryAction != null && toggleInventoryAction.action != null)
        //     {
        //         toggleInventoryAction.action.performed -= OnToggleAction;
        //     }

        //     if (inventoryManager != null)
        //     {
        //         inventoryManager.OnInventoryUpdated -= RefreshUI;
        //     }

        //     if (closeButton != null)
        //     {
        //         closeButton.onClick.RemoveListener(Close);
        //     }

        //     if (inventoryPresenter != null)
        //     {
        //         var cleanupMethod = inventoryPresenter.GetType().GetMethod("Cleanup");
        //         cleanupMethod?.Invoke(inventoryPresenter, null);
        //     }
        // }
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
            OnInventoryToggled?.Invoke(isOpen);

            if (isOpen) RefreshUI();

            if (!isOpen && TooltipPanel.Instance != null)
            {
                TooltipPanel.Instance.HideTooltip();
            }
        }

        public void OpenForShop()
        {
            if (equipmentPanel != null) equipmentPanel.SetActive(false);
            if (statsPanel != null) statsPanel.SetActive(false);
            Open(false); // 다른 UI(상점)를 닫지 않고 열기
        }

        public void RefreshUI()
        {
            InventoryDataManager inventoryDataMgr = GetInventoryDataManager();
            if (slotPool == null || inventoryDataMgr == null)
            {
                Debug.LogWarning($"[InventoryUI] RefreshUI 실패 - slotPool: {slotPool?.Count ?? 0}, inventoryDataMgr: {inventoryDataMgr?.GetType().Name ?? "null"}");
                return;
            }

            for (int i = 0; i < slotPool.Count; i++)
            {
                if (i < inventoryDataMgr.MaxSlotCount)
                {
                    var (item, count) = inventoryDataMgr.GetSlot(i);
                    slotPool[i].SetItem(item, count);
                }
                else
                {
                    slotPool[i].ClearSlot();
                    slotPool[i].gameObject.SetActive(false);
                }
            }
        }

        private void OnDestroy()
        {
            // 이벤트 구독 해제
            InventoryDataManager inventoryDataMgr = GetInventoryDataManager();
            if (inventoryDataMgr != null)
            {
                inventoryDataMgr.OnDataChanged -= OnInventoryDataChanged;
            }

            EventManager eventMgr = GetEventManager();
            if (eventMgr != null)
            {
                eventMgr.OnOpenInventoryForShop -= OpenForShop;
                eventMgr.OnCloseInventoryForShop -= Close;
            }

            if (toggleInventoryAction != null && toggleInventoryAction.action != null)
            {
                toggleInventoryAction.action.performed -= OnToggleAction;
            }
        }
    }
}