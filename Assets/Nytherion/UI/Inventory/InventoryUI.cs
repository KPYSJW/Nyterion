using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using Nytherion.Core;
using Nytherion.UI.Shop;

namespace Nytherion.UI.Inventory
{
    public class InventoryUI : MonoBehaviour
    {
        public static InventoryUI Instance { get; private set; }

        [Header("UI Panels")]
        [Tooltip("인벤토리 전체를 감싸는 부모 패널")]
        [SerializeField] private GameObject mainUIPanel;
        [Tooltip("캐릭터 장비창 패널")]
        [SerializeField] private GameObject equipmentPanel;
        [Tooltip("캐릭터 능력치창 패널")]
        [SerializeField] private GameObject statsPanel;
        [Tooltip("실제 아이템 슬롯들이 있는 인벤토리 패널")]
        [SerializeField] private GameObject inventoryPanel;
        [SerializeField] private InputActionReference toggleInventoryAction;
        [Tooltip("인벤토리 슬롯들이 있는 부모 오브젝트")]
        [SerializeField] private Transform slotParent;

        [Header("Buttons")]
        [SerializeField] private Button closeButton;
        [SerializeField] private CanvasGroup canvasGroup;
        private bool isOpen = false;

        public event Action<bool> OnInventoryToggled;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
            HideInventoryUI();
        }

        public void Initialize()
        {
            isOpen = false;
            if (closeButton != null)
            {
                closeButton.onClick.AddListener(CloseInventory);
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
            if (ShopUI.Instance != null && ShopUI.Instance.IsOpen)
            {
                return;
            }
            ToggleInventory();
        }

        public void ToggleInventory()
        {
            if (ShopUI.Instance != null && ShopUI.Instance.IsOpen) return;
            isOpen = !isOpen;

            if (isOpen)
            {
                ShowInventoryUI();
            }
            else
            {
                HideInventoryUI();
            }
            OnInventoryToggled?.Invoke(isOpen);
        }

        public void CloseInventory()
        {
            isOpen = false;
            inventoryPanel.SetActive(false);
            OnInventoryToggled?.Invoke(false);

            if (TooltipPanel.Instance != null)
            {
                TooltipPanel.Instance.HideTooltip();
            }
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

        private void ShowInventoryUI()
        {
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
                canvasGroup.blocksRaycasts = true;
                canvasGroup.interactable = true;
            }

            if (mainUIPanel != null) mainUIPanel.SetActive(true);
            if (equipmentPanel != null) equipmentPanel.SetActive(true);
            if (statsPanel != null) statsPanel.SetActive(true);
            if (inventoryPanel != null) inventoryPanel.SetActive(true);
        }

        private void HideInventoryUI()
        {
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.blocksRaycasts = false;
                canvasGroup.interactable = false;
            }
        }

        public void OpenForShop()
        {
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
                canvasGroup.blocksRaycasts = true;
                canvasGroup.interactable = true;
            }

            if (mainUIPanel != null) mainUIPanel.SetActive(true);
            if (equipmentPanel != null) equipmentPanel.SetActive(false);
            if (statsPanel != null) statsPanel.SetActive(false);
            if (inventoryPanel != null) inventoryPanel.SetActive(true);
        }
        public void CloseAllPanels()
        {
            HideInventoryUI();
        }
    }
}