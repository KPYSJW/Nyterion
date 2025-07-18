using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using Nytherion.Core.Managers;
using Nytherion.UI.Components;
using Nytherion.UI.Inventory;
using Nytherion.Data.ScriptableObjects.Items;
using System.Linq;

namespace Nytherion.UI.Controllers
{
    public class InventoryUI : UIPanelBase
    {
        public static InventoryUI Instance { get; private set; }

        [Header("UI Panels")]
        [SerializeField] private GameObject equipmentPanel;
        [SerializeField] private GameObject statsPanel;

        [Header("Input")]
        [SerializeField] private InputActionReference toggleInventoryAction;
        
        [Header("References")]
        [SerializeField] private Transform slotParent;
        [SerializeField] private Button closeButton;

        private List<InventorySlotUI> slotPool = new List<InventorySlotUI>();

        public event Action<bool> OnInventoryToggled;

        protected override void Awake()
        {
            base.Awake();
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
            
            InitializeSlotPool();
        }

        public void Initialize()
        {
            if (closeButton != null)
            {
                closeButton.onClick.AddListener(Close);
            }
        }

        private void InitializeSlotPool()
        {
            slotPool = slotParent.GetComponentsInChildren<InventorySlotUI>(true).ToList();
            for (int i = 0; i < slotPool.Count; i++)
            {
                slotPool[i].Initialize(i);
            }
        }

        private void OnEnable()
        {
            if (InventoryManager.Instance != null)
            {
                InventoryManager.Instance.OnInventoryUpdated += RefreshUI;
            }

            if (toggleInventoryAction != null && toggleInventoryAction.action != null)
            {
                toggleInventoryAction.action.performed += OnToggleAction;
                toggleInventoryAction.action.Enable();
            }
        }

        private void OnDisable()
        {
            if (toggleInventoryAction != null && toggleInventoryAction.action != null)
            {
                toggleInventoryAction.action.performed -= OnToggleAction;
            }

            if (InventoryManager.Instance != null)
            {
                InventoryManager.Instance.OnInventoryUpdated -= RefreshUI;
            }
        }

        private void OnToggleAction(InputAction.CallbackContext context)
        {
            if (ShopUI.Instance != null && ShopUI.Instance.IsOpen)
            {
                return;
            }
            if(!equipmentPanel.activeSelf)
            {
                equipmentPanel.SetActive(true);
            }
            if(!statsPanel.activeSelf)
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
            Open();
        }

        public void RefreshUI()
        {
            if (slotPool == null || InventoryManager.Instance == null) return;

            for (int i = 0; i < slotPool.Count; i++)
            {
                if (i < InventoryManager.Instance.MaxSlotCount)
                {
                    var (item, count) = InventoryManager.Instance.GetItemAt(i);
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