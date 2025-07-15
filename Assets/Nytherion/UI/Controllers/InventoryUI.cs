using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using Nytherion.Core.Managers;
using Nytherion.UI.Components;
using Nytherion.UI.Inventory;

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

        public event Action<bool> OnInventoryToggled;

        protected override void Awake()
        {
            base.Awake();
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        public void Initialize()
        {
            if (closeButton != null)
            {
                closeButton.onClick.AddListener(Close);
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
                InventoryManager.Instance.OnInventoryUpdated -= RefreshUI;
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
            if (slotParent == null || InventoryManager.Instance == null) return;

            var slots = slotParent.GetComponentsInChildren<InventorySlotUI>(true);
            if (slots == null || slots.Length == 0) return;

            foreach (var slot in slots) slot?.ClearSlot();

            var items = InventoryManager.Instance.GetAllItems();
            int slotIndex = 0;
            foreach (var item in items)
            {
                if (slotIndex >= slots.Length) break;
                if (item.Key != null && slots[slotIndex] != null)
                {
                    slots[slotIndex].SetItem(item.Key, item.Value);
                }
                slotIndex++;
            }
        }
    }
}