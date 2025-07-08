using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Nytherion.Data.ScriptableObjects.Shop;
using Nytherion.Core.Managers;
using Nytherion.Data.ScriptableObjects.Items;
using Nytherion.UI.Inventory;
using TMPro;
using Nytherion.Core.Enums;
using Nytherion.UI.Shop;
using Nytherion.Data.ScriptableObjects.Weapons;

namespace Nytherion.UI.Controllers
{
    public class ShopUI : UIPanelBase
    {
        public static ShopUI Instance { get; private set; }

        [Header("UI References")]
        [SerializeField] private Transform shopSlotParent;
        [SerializeField] private GameObject shopSlotPrefab;
        [SerializeField] private Button closeButton;
        [SerializeField] private TextMeshProUGUI playerGoldText;

        [Header("Player Inventory Display")]
        [SerializeField] private Transform playerInventoryParent;
        private List<InventorySlotUI> playerInventorySlots;

        private ShopData currentShopData;
        private const float SELL_PRICE_RATIO = 0.7f;

        protected override void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this.gameObject);
                return;
            }
            Instance = this;
            base.Awake();
        }

        private void Start()
        {
            if (closeButton != null)
            {
                closeButton.onClick.AddListener(Close);
            }
            if (SellSlotUI.Instance != null)
            {
                SellSlotUI.Instance.OnItemSold += HandleSellItem;
            }
        }

        private void OnEnable()
        {
            if (CurrencyManager.Instance != null) CurrencyManager.Instance.onCurrencyChanged += UpdateCurrencyUI;
            if (InventoryManager.Instance != null) InventoryManager.Instance.OnInventoryUpdated += RefreshPlayerInventoryUI;
            if (EventManager.Instance != null)
            {
                EventManager.Instance.OnInteraction += HandleInteraction;
            }
            if (playerInventoryParent != null)
            {
                playerInventorySlots = new List<InventorySlotUI>(playerInventoryParent.GetComponentsInChildren<InventorySlotUI>(true));
            }
        }

        private void OnDisable()
        {
            if (CurrencyManager.Instance != null) CurrencyManager.Instance.onCurrencyChanged -= UpdateCurrencyUI;
            if (InventoryManager.Instance != null) InventoryManager.Instance.OnInventoryUpdated -= RefreshPlayerInventoryUI;
            if (SellSlotUI.Instance != null) SellSlotUI.Instance.OnItemSold -= HandleSellItem;
            if (EventManager.Instance != null)
            {
                EventManager.Instance.OnInteraction -= HandleInteraction;
            }
        }

        private void HandleInteraction(InteractableType type)
        {
            if (IsOpen && type != InteractableType.ShopDealer)
            {
                Close();
            }
        }

        public void OpenShop(ShopData data)
        {
            currentShopData = data;
            PopulateShop();
            if (InventoryUI.Instance != null) InventoryUI.Instance.OpenForShop();
            UpdateCurrencyUI(CurrencyType.Gold, CurrencyManager.Instance.GetCurrency(CurrencyType.Gold));
            Open();
        }

        public override void Open()
        {
            base.Open();
            if (ShopManager.Instance != null)
            {
                ShopManager.Instance.OnStockChanged += RefreshShopUI;
            }
            RefreshShopUI();
        }

        public override void Close()
        {
            base.Close();
            if (InventoryUI.Instance != null) InventoryUI.Instance.Close();
            if (SellSlotUI.Instance != null) SellSlotUI.Instance.ClearSlot();

            if (ShopManager.Instance != null)
            {
                ShopManager.Instance.OnStockChanged -= RefreshShopUI;
            }
        }

        public void BuyItem(ShopSlotUI slot)
        {
            var shopItem = slot.CurrentItem;
            if (shopItem == null || (!shopItem.isUnlimited && shopItem.stock <= 0))
            {
                Debug.LogWarning("[ShopUI] 유효하지 않은 상점 아이템이거나 재고가 없습니다.");
                return;
            }

            int amountToBuy = (shopItem.item is EquipmentData) ? 1 : 1;

            if (CurrencyManager.Instance.SpendCurrency(CurrencyType.Gold, shopItem.price * amountToBuy))
            {
                if (InventoryManager.Instance.AddItem(shopItem.item, amountToBuy))
                {
                    Debug.Log($"[ShopUI] '{shopItem.item.itemName}' ({shopItem.item.GetType().Name}) 구매 완료. (ID: {shopItem.shopItemId}) from shop '{currentShopData.shopName}'");

                    if (ShopManager.Instance != null && !shopItem.isUnlimited)
                    {
                        ShopManager.Instance.RecordPurchase(currentShopData.shopName, shopItem.shopItemId);
                    }

                    if (shopItem.item is WeaponData weaponData)
                    {
                        var equipmentSlot = FindObjectOfType<EquipmentSlotUI>();
                        if (equipmentSlot != null && equipmentSlot.IsEmpty)
                        {
                            if (InventoryManager.Instance.RemoveItem(shopItem.item, 1))
                            {
                                equipmentSlot.SetItem(shopItem.item, 1);
                                Debug.Log($"[ShopUI] '{weaponData.itemName}' 아이템을 장비 슬롯에 자동으로 장착했습니다.");
                            }
                        }
                    }
                }
                else
                {
                    Debug.LogWarning("[ShopUI] 인벤토리에 아이템을 추가하지 못했습니다. 골드를 환불합니다.");
                    CurrencyManager.Instance.AddCurrency(CurrencyType.Gold, shopItem.price * amountToBuy);
                }
            }
        }

        private void HandleSellItem(ItemData item, int amount)
        {
            if (InventoryManager.Instance.RemoveItem(item, amount))
            {
                int totalPrice = Mathf.RoundToInt(item.baseValue * SELL_PRICE_RATIO) * amount;
                CurrencyManager.Instance.AddCurrency(CurrencyType.Gold, totalPrice);
            }
        }

        private void PopulateShop()
        {
            if (currentShopData == null) return;
            foreach (Transform child in shopSlotParent) Destroy(child.gameObject);

            var itemsToDisplay = ShopManager.Instance.GetShopItems(currentShopData.shopName);
            if (itemsToDisplay == null) return;

            foreach (ShopItemData shopItem in itemsToDisplay)
            {
                GameObject slotGO = Instantiate(shopSlotPrefab, shopSlotParent);
                if (slotGO.TryGetComponent(out ShopSlotUI slotUI))
                {
                    slotUI.Setup(shopItem);
                }
            }
        }

        private void RefreshPlayerInventoryUI()
        {
            if (!IsOpen || playerInventorySlots == null) return;
            var items = InventoryManager.Instance.GetAllItems();
            int i = 0;
            foreach (var itemEntry in items)
            {
                if (i >= playerInventorySlots.Count) break;
                if (playerInventorySlots[i] != null && itemEntry.Key != null)
                {
                    playerInventorySlots[i].SetItem(itemEntry.Key, itemEntry.Value);
                }
                i++;
            }
            for (; i < playerInventorySlots.Count; i++)
            {
                if (playerInventorySlots[i] != null) playerInventorySlots[i].ClearSlot();
            }
        }

        private void UpdateCurrencyUI(CurrencyType type, int amount)
        {
            if (type == CurrencyType.Gold && playerGoldText != null)
            {
                playerGoldText.text = $"{amount} G";
            }
        }

        private void RefreshShopUI()
        {
            PopulateShop();
        }
    }
}