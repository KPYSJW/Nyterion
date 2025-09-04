using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using Nytherion.Core.Managers;
using Nytherion.UI.Components;
using Nytherion.UI.Inventory;
using Nytherion.UI.Presenters;
using System.Linq;
using Zenject;

namespace Nytherion.UI.Controllers
{
    public class InventoryUI : UIPanelBase, IInitializable
    {

        [Header("Input")]
        [SerializeField] private InputActionReference toggleInventoryAction;

        private List<InventorySlotUI> slotPool = new List<InventorySlotUI>();

        public event Action<bool> OnInventoryToggled;

        private InventoryManager inventoryManager;
        private EventManager eventManager;
        private ShopManager shopManager;
        private GameObject equipmentPanel;
        private GameObject statsPanel;
        private Transform inventorySlotParent;
        private Button closeButton;
        private InventoryPresenter inventoryPresenter;

        [Inject]
        public void Construct(
         InventoryManager inventoryManager,
         EventManager eventManager,
         ShopManager shopManager,
         [Inject(Id = "EquipmentPanel")] GameObject equipmentPanel,
         [Inject(Id = "StatsPanel")] GameObject statsPanel,
         [Inject(Id = "InventorySlotParent")] Transform inventorySlotParent,
         [Inject(Id = "CloseButton")] Button closeButton,
         [Inject(Id = "InventoryCanvasGroup")] CanvasGroup canvasGroup,
         InventoryPresenter inventoryPresenter)
        {
            this.inventoryManager = inventoryManager;
            this.eventManager = eventManager;
            this.shopManager = shopManager;
            this.equipmentPanel = equipmentPanel;
            this.statsPanel = statsPanel;
            this.inventorySlotParent = inventorySlotParent;
            this.closeButton = closeButton;
            this.controlledCanvasGroup = canvasGroup;
            this.inventoryPresenter = inventoryPresenter;
        }
        protected override void Awake()
        {
            base.Awake();

            inventoryPresenter?.Initialize();

            InitializeSlotPool();
        }

        public void Initialize()
        {
            Debug.Log("InventoryUI.Initialize() 호출됨!");
            if (closeButton != null)
            {
                closeButton.onClick.AddListener(Close);
            }
            if (inventoryManager != null)
            {
                inventoryManager.OnInventoryUpdated += RefreshUI;
            }

            if (toggleInventoryAction != null && toggleInventoryAction.action != null)
            {
                toggleInventoryAction.action.performed += OnToggleAction;
                toggleInventoryAction.action.Enable();
            }

            if (eventManager != null)
            {
                eventManager.OnOpenInventoryForShop += OpenForShop;
                eventManager.OnCloseInventoryForShop += Close;
            }

            inventoryPresenter?.Initialize();
            InitializeSlotPool();

            Close();
        }

        private void InitializeSlotPool()
        {
            slotPool = inventorySlotParent.GetComponentsInChildren<InventorySlotUI>(true).ToList();

            if (slotPool.Count == 0) return;

            for (int i = 0; i < slotPool.Count; i++)
            {
                slotPool[i].Initialize(i);
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
            Debug.Log("[InventoryUI] 토글 액션 입력 받음");
            if (shopManager != null)
            {
                Debug.Log($"[InventoryUI] 현재 상점 상태(IsShopOpen): {shopManager.IsShopOpen}");
                if (shopManager.IsShopOpen)
                {
                    return;
                }
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
            Debug.Log("[InventoryUI] 상점 열기 이벤트 수신!");
            if (equipmentPanel != null) equipmentPanel.SetActive(false);
            if (statsPanel != null) statsPanel.SetActive(false);
            Open();
        }

        public void RefreshUI()
        {
            if (slotPool == null || inventoryManager == null) return;

            for (int i = 0; i < slotPool.Count; i++)
            {
                if (i < inventoryManager.MaxSlotCount)
                {
                    var (item, count) = inventoryManager.GetItemAt(i);
                    slotPool[i].SetItem(item, count);
                }
                else
                {
                    slotPool[i].ClearSlot();
                    slotPool[i].gameObject.SetActive(false);
                }
            }
        }
    }
}