using System;
using UnityEngine;
using UnityEngine.InputSystem;
using Nytherion.Data.ScriptableObjects.Items;
using Nytherion.Core;

namespace Nytherion.UI.Inventory
{
    public class InventoryUI : MonoBehaviour
    {
        [SerializeField] private GameObject inventoryPanel;
        [SerializeField] private InputActionReference toggleInventoryAction;

        private bool isOpen = false;

        public event Action<bool> OnInventoryToggled; // true: opened, false: closed

        private void OnEnable()
        {
            if (toggleInventoryAction == null || toggleInventoryAction.action == null)
            {
                Debug.LogError("Toggle Inventory Action is not assigned!");
                return;
            }

            toggleInventoryAction.action.Enable();
            toggleInventoryAction.action.performed += OnToggleInventory;

            if (InventoryManager.Instance != null)
                InventoryManager.Instance.OnInventoryUpdated += RefreshUI;
        }

        private void OnDisable()
        {
            if (toggleInventoryAction != null && toggleInventoryAction.action != null)
            {
                toggleInventoryAction.action.performed -= OnToggleInventory;
                toggleInventoryAction.action.Disable();
            }

            if (InventoryManager.Instance != null)
                InventoryManager.Instance.OnInventoryUpdated -= RefreshUI;
        }

        private void OnToggleInventory(InputAction.CallbackContext context)
        {
            ToggleInventory();
        }

        public void ToggleInventory()
        {
            isOpen = !isOpen;
            inventoryPanel.SetActive(isOpen);
            OnInventoryToggled?.Invoke(isOpen);
        }

        private void RefreshUI()
        {
            Debug.Log("[InventoryUI] UI Refreshed");
        }
    }
}