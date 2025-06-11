using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Nytherion.Data.Shop;
using Nytherion.Core;
using Nytherion.Data.ScriptableObjects.Items;
using Nytherion.UI.Inventory;
using Nytherion.UI.Shop;
using TMPro;

namespace Nytherion.UI.Shop
{
    public class ShopUI : MonoBehaviour
    {
        public static ShopUI Instance;

        [Header("UI 패널")]
        [SerializeField] private GameObject shopPanel;
        [SerializeField] private CanvasGroup canvasGroup;

        [Header("슬롯 설정")]
        [SerializeField] private GameObject shopSlotPrefab;
        [SerializeField] private Transform shopSlotParent;

        [Header("플레이어 인벤토리")]
        [SerializeField] private Transform playerInventoryParent;
        private List<InventorySlotUI> playerInventorySlots;

        [Header("버튼")]
        [SerializeField] private Button closeButton;
        [SerializeField] private TextMeshProUGUI playerGoldText;

        private ShopData currentShopData;
        private bool isShopOpen = false;
        public bool IsOpen => shopPanel.activeInHierarchy;
        private const float SELL_PRICE_RATIO = 0.7f;
        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
            shopPanel.SetActive(false);
        }
        private void Start()
        {
            if (closeButton != null)
            {
                closeButton.onClick.AddListener(CloseShop);
            }
            if (SellSlotUI.Instance != null)
            {
                SellSlotUI.Instance.OnItemSold += HandleSellItem;
            }
        }
        private void OnEnable()
        {
            if (CurrencyManager.Instance != null)
            {
                CurrencyManager.Instance.onCurrencyChanged += UpdateCurrencyUI;
            }
            if (InventoryManager.Instance != null)
            {
                InventoryManager.Instance.OnInventoryUpdated += RefreshPlayerInventoryUI;
            }
            playerInventorySlots = new List<InventorySlotUI>(
                playerInventoryParent.GetComponentsInChildren<InventorySlotUI>(true)
                    .Where(slot => slot != null)
            );

            foreach (InventorySlotUI slot in playerInventorySlots)
            {
                slot.OnSellItemAction += (baseSlot) =>
                {
                    if (baseSlot.CurrentItem != null)
                    {
                        SellSlotUI.Instance?.SetItem(baseSlot.CurrentItem, 1);
                    }
                };
            }
        }

        private void OnDisable()
        {
            if (CurrencyManager.Instance != null)
            {
                CurrencyManager.Instance.onCurrencyChanged -= UpdateCurrencyUI;
            }
            if (InventoryManager.Instance != null)
            {
                InventoryManager.Instance.OnInventoryUpdated -= RefreshPlayerInventoryUI;
            }
            if (SellSlotUI.Instance != null)
            {
                SellSlotUI.Instance.OnItemSold -= HandleSellItem;
            }
        }

        public bool IsShopOpen() => isShopOpen;

        public void OpenShop(ShopData data)
        {
            currentShopData = data;
            ShowShopUI();
            isShopOpen = true;

            PopulateShop();

            if (InventoryUI.Instance != null)
            {
                InventoryUI.Instance.OpenForShop();
                RefreshPlayerInventoryUI();
            }
            else
            {
                Debug.LogError("InventoryUI.Instance is null in ShopUI.OpenShop()");
            }

            UpdateCurrencyUI(CurrencyType.Gold, CurrencyManager.Instance.GetCurrency(CurrencyType.Gold));
        }

        public void CloseShop()
        {
            HideShopUI();
            isShopOpen = false;

            if (InventoryUI.Instance != null)
            {
                InventoryUI.Instance.CloseAllPanels();
            }
        }

        private void PopulateShop()
        {
            foreach (Transform child in shopSlotParent)
            {
                Destroy(child.gameObject);
            }

            foreach (ShopItemData shopItem in currentShopData.itemsForSale)
            {
                GameObject slotGO = Instantiate(shopSlotPrefab, shopSlotParent);
                ShopSlotUI slotUI = slotGO.GetComponent<ShopSlotUI>();
                if (slotUI != null)
                {
                    slotUI.Setup(shopItem);
                }
            }
        }

        public void BuyItem(ShopSlotUI slot)
        {
            var shopItem = slot.CurrentItem;

            if (!shopItem.isUnlimited && shopItem.stock == 0)
            {
                Debug.Log("해당 아이템은 품절되었습니다.");
                return;
            }

            if (CurrencyManager.Instance.SpendCurrency(CurrencyType.Gold, shopItem.price))
            {
                InventoryManager.Instance.AddItem(shopItem.item);

                if (!shopItem.isUnlimited && shopItem.stock > 0)
                {
                    shopItem.stock--;
                }

                slot.UpdateStockUI();
            }
            else
            {
                Debug.Log("골드가 부족합니다.");
            }
        }

        private void HandleSellItem(ItemData item, int amount)
        {
            if (InventoryManager.Instance.RemoveItem(item, amount))
            {
                int totalPrice = Mathf.RoundToInt(item.baseValue * SELL_PRICE_RATIO) * amount;
                CurrencyManager.Instance.AddCurrency(CurrencyType.Gold, totalPrice);
                Debug.Log($"[Shop] {item.itemName} x{amount} 판매 완료 (+{totalPrice}G)");
            }
            else
            {
                Debug.LogWarning($"[Shop] {item.itemName} 판매 실패");
            }
        }

        private void RefreshPlayerInventoryUI()
        {
            if (!isShopOpen) return;

            var items = InventoryManager.Instance.GetAllItems();
            int i = 0;

            foreach (var itemEntry in items)
            {
                if (itemEntry.Key == null)
                {
                    Debug.LogError($"[ShopUI] Null item key at index {i}");
                    continue;
                }

                if (i >= playerInventorySlots.Count)
                {
                    Debug.LogWarning($"[ShopUI] Slot index {i} out of range.");
                    continue;
                }

                if (playerInventorySlots[i] == null)
                {
                    Debug.LogError($"[ShopUI] Slot at index {i} is null");
                    continue;
                }

                try
                {
                    playerInventorySlots[i].SetItem(itemEntry.Key, itemEntry.Value);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[ShopUI] Exception at slot {i}: {ex.Message}\n{ex.StackTrace}");
                }

                i++;
            }

            for (; i < playerInventorySlots.Count; i++)
            {
                if (playerInventorySlots[i] != null)
                    playerInventorySlots[i].ClearSlot();
            }
        }

        private void UpdateCurrencyUI(CurrencyType type, int amount)
        {
            if (type == CurrencyType.Gold && playerGoldText != null)
            {
                playerGoldText.text = $"{amount} G";
            }
        }

        private void ShowShopUI()
        {
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
                canvasGroup.blocksRaycasts = true;
                canvasGroup.interactable = true;
            }

            if (shopPanel != null) shopPanel.SetActive(true);
        }

        private void HideShopUI()
        {
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.blocksRaycasts = false;
                canvasGroup.interactable = false;
            }

            if (shopPanel != null) shopPanel.SetActive(false);
            if (SellSlotUI.Instance != null) SellSlotUI.Instance.ClearSlot();
        }
    }
}

