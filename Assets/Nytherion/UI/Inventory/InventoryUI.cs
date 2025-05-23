using System;
using UnityEngine;
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
            Debug.Log("[InventoryUI] Refreshing UI...");
            
            // 인벤토리 슬롯들을 찾아서 갱신
            InventorySlotUI[] slots;
            
            // slotParent가 할당되어 있으면 그 아래에서 슬롯을 찾고, 아니면 현재 오브젝트 아래에서 찾음
            Transform searchParent = slotParent != null ? slotParent : transform;
            Debug.Log($"[InventoryUI] Looking for InventorySlotUI components under {searchParent.name}");
            
            slots = searchParent.GetComponentsInChildren<InventorySlotUI>(true);
            
            if (slots == null || slots.Length == 0)
            {
                Debug.LogWarning($"[InventoryUI] No inventory slots found in children of {searchParent.name}");
                
                // 계층 구조 디버깅을 위해 모든 자식 오브젝트 로깅
                Debug.Log($"[InventoryUI] Current hierarchy under {searchParent.name}:");
                foreach (Transform child in searchParent)
                {
                    Debug.Log($"- {child.name} (Active: {child.gameObject.activeSelf}, ActiveInHierarchy: {child.gameObject.activeInHierarchy})");
                }
                return;
            }
            
            Debug.Log($"[InventoryUI] Found {slots.Length} slots to update");
            
            // 모든 슬롯 초기화
            foreach (var slot in slots)
            {
                if (slot != null)
                {
                    slot.ClearSlot();
                }
            }
            
            // 인벤토리 매니저에서 아이템 목록 가져오기
            if (InventoryManager.Instance == null)
            {
                Debug.LogError("[InventoryUI] InventoryManager instance is null");
                return;
            }
            
            var items = InventoryManager.Instance.GetAllItems();
            Debug.Log($"[InventoryUI] Found {items.Count} items in inventory");
            
            // 각 슬롯에 아이템 할당
            int slotIndex = 0;
            foreach (var item in items)
            {
                if (slotIndex >= slots.Length)
                {
                    Debug.LogWarning("[InventoryUI] Not enough slots to display all items");
                    break;
                }
                
                if (item.Key == null || slots[slotIndex] == null)
                {
                    Debug.LogWarning($"[InventoryUI] Invalid item or slot at index {slotIndex}");
                    slotIndex++;
                    continue;
                }
                
                Debug.Log($"[InventoryUI] Setting item {item.Key.name} (x{item.Value}) to slot {slotIndex}");
                slots[slotIndex].SetItem(item.Key, item.Value);
                slotIndex++;
            }
            
            Debug.Log("[InventoryUI] UI refresh completed");
        }
    }
}