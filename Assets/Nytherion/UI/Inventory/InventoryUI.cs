using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using Nytherion.Data.ScriptableObjects.Items;
using Nytherion.Core;

namespace Nytherion.UI.Inventory
{
    public class InventoryUI : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private GameObject inventoryPanel;
        [SerializeField] private InputActionReference toggleInventoryAction;
        [Tooltip("인벤토리 슬롯들이 있는 부모 오브젝트")]
        [SerializeField] private Transform slotParent; // 인벤토리 슬롯들이 있는 부모 오브젝트

        [Header("Buttons")]
        [SerializeField] private Button closeButton; // 닫기 버튼

        private bool isOpen = false;

        public event Action<bool> OnInventoryToggled; // true: opened, false: closed

        private void Start()
        {
            inventoryPanel.SetActive(false);
            isOpen = false;
            if (closeButton != null)
            {
                closeButton.onClick.AddListener(CloseInventory);
            }

            if (InventoryManager.Instance != null)
            {
                RefreshUI();
            }
        }

        private void OnEnable()
        {
            if (InventoryManager.Instance != null)
            {
                InventoryManager.Instance.OnInventoryUpdated += RefreshUI;
                RefreshUI();
            }

            if (toggleInventoryAction != null && toggleInventoryAction.action != null)
            {
                toggleInventoryAction.action.performed += OnToggleInventory;
                toggleInventoryAction.action.Enable();
            }
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

        public void CloseInventory()
        {
            isOpen = false;
            inventoryPanel.SetActive(false);
            OnInventoryToggled?.Invoke(false);
        }

        public void RefreshUI()
        {
            if (slotParent == null) return;

            var slots = slotParent.GetComponentsInChildren<InventorySlotUI>(true);

            if (slots == null || slots.Length == 0)
            {
                Debug.LogWarning($"인벤토리 슬롯을 찾을 수 없습니다: {slotParent.name}");
                return;
            }

            foreach (var slot in slots)
            {
                slot?.ClearSlot();
            }

            if (InventoryManager.Instance == null) return;

            var items = InventoryManager.Instance.GetAllItems();

            int slotIndex = 0;
            foreach (var item in items)
            {
                if (slotIndex >= slots.Length || item.Key == null || slots[slotIndex] == null)
                {
                    slotIndex++;
                    continue;
                }

                slots[slotIndex].SetItem(item.Key, item.Value);
                slotIndex++;
            }
        }
    }
}