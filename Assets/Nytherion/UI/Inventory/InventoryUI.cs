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
            // 닫기 버튼 이벤트 등록
            if (closeButton != null)
            {
                closeButton.onClick.AddListener(CloseInventory);
            }
            
            // InventoryManager가 초기화되었는지 확인하고 수동으로 한 번 Refresh
            if (InventoryManager.Instance != null)
            {
                RefreshUI();
            }
        }

        private void OnEnable()
        {
            // 인벤토리 매니저의 이벤트 구독
            if (InventoryManager.Instance != null)
            {
                InventoryManager.Instance.OnInventoryUpdated += RefreshUI;
                // 인벤토리 매니저가 이미 초기화된 경우 수동으로 한 번 호출
                RefreshUI();
            }
            
            // 인벤토리 토글 액션 설정
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
        
        // 인벤토리를 닫는 메서드
        public void CloseInventory()
        {
            isOpen = false;
            inventoryPanel.SetActive(false);
            OnInventoryToggled?.Invoke(false);
        }

        public void RefreshUI()
        {
            if (slotParent == null) return;
            
            // 인벤토리 슬롯들을 찾아서 갱신
            var slots = slotParent.GetComponentsInChildren<InventorySlotUI>(true);
            
            if (slots == null || slots.Length == 0)
            {
                Debug.LogWarning($"인벤토리 슬롯을 찾을 수 없습니다: {slotParent.name}");
                return;
            }
            
            // 모든 슬롯 초기화
            foreach (var slot in slots)
            {
                slot?.ClearSlot();
            }
            
            // 인벤토리 매니저에서 아이템 목록 가져오기
            if (InventoryManager.Instance == null) return;
            
            var items = InventoryManager.Instance.GetAllItems();
            
            // 모든 아이템을 인벤토리에 표시
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