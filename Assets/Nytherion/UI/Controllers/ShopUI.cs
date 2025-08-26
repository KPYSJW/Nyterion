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
using Zenject;

namespace Nytherion.UI.Controllers
{
    public class ShopUI : UIPanelBase, IInitializable
    {

        [Header("UI References")]
        private Transform shopSlotParent;
        private GameObject shopSlotPrefab;
        private Button closeButton;
        private TextMeshProUGUI playerGoldText;

        [Header("Player Inventory Display")]
        private Transform playerInventoryParent;
        private List<InventorySlotUI> playerInventorySlots;

        private ShopData currentShopData;
        private const float SELL_PRICE_RATIO = 0.7f;
        private InventoryManager inventoryManager;
        private CurrencyManager currencyManager;
        private EventManager eventManager;
        private ShopManager shopManager;
        private SellSlotUI sellSlotUI;
        private DiContainer container;


        [Inject]
        public void Construct(
            InventoryManager inventoryManager,
            CurrencyManager currencyManager,
            EventManager eventManager,
            ShopManager shopManager,
            [Inject(Id = "ShopCanvasGroup")] CanvasGroup controlledCanvasGroup,
            [Inject(Id = "ShopSlotParent")] Transform shopSlotParent,
            [Inject(Id = "ShopSlotPrefab")] GameObject shopSlotPrefab,
            [Inject(Id = "ShopCloseButton")] Button closeButton,
            [Inject(Id = "ShopPlayerGoldText")] TextMeshProUGUI playerGoldText,
            [Inject(Id = "ShopPlayerInventoryParent")] Transform playerInventoryParent,
            SellSlotUI sellSlotUI,
            DiContainer container)
        {
            this.controlledCanvasGroup = controlledCanvasGroup;
            this.shopSlotParent = shopSlotParent;
            this.shopSlotPrefab = shopSlotPrefab;
            this.closeButton = closeButton;
            this.playerGoldText = playerGoldText;
            this.playerInventoryParent = playerInventoryParent;
            this.sellSlotUI = sellSlotUI;
            this.inventoryManager = inventoryManager;
            this.currencyManager = currencyManager;
            this.eventManager = eventManager;
            this.shopManager = shopManager;
            this.container = container;
        }

        protected override void Awake()
        {
            base.Awake();
        }
        public void Initialize()
        {
            Debug.Log("ShopUI.Initialize() 호출됨!");
            if (closeButton != null)
            {
                closeButton.onClick.AddListener(Close);
            }
            if (sellSlotUI != null)
            {
                sellSlotUI.OnItemSold += HandleSellItem;
            }
            if (currencyManager != null)
            {
                currencyManager.onCurrencyChanged += UpdateCurrencyUI;
            }
            if (inventoryManager != null)
            {
                inventoryManager.OnInventoryUpdated += RefreshPlayerInventoryUI;
            }
            if (eventManager != null)
            {
                eventManager.OnInteraction += HandleInteraction;
            }
            if (playerInventoryParent != null)
            {
                playerInventorySlots = new List<InventorySlotUI>(playerInventoryParent.GetComponentsInChildren<InventorySlotUI>(true));
            }

            Close();
        }
        // private void Start()
        // {
        //     if (closeButton != null)
        //     {
        //         closeButton.onClick.AddListener(Close);
        //     }
        //     if (sellSlotUI != null)
        //     {
        //         sellSlotUI.OnItemSold += HandleSellItem;
        //     }
        // }

        // private void OnEnable()
        // {
        //     if (currencyManager != null) currencyManager.onCurrencyChanged += UpdateCurrencyUI;
        //     if (inventoryManager != null) inventoryManager.OnInventoryUpdated += RefreshPlayerInventoryUI;
        //     if (eventManager != null)
        //     {
        //         eventManager.OnInteraction += HandleInteraction;
        //     }
        //     if (playerInventoryParent != null)
        //     {
        //         playerInventorySlots = new List<InventorySlotUI>(playerInventoryParent.GetComponentsInChildren<InventorySlotUI>(true));
        //     }
        // }

        // private void OnDisable()
        // {
        //     if (currencyManager != null) currencyManager.onCurrencyChanged -= UpdateCurrencyUI;
        //     if (inventoryManager != null) inventoryManager.OnInventoryUpdated -= RefreshPlayerInventoryUI;
        //     if (sellSlotUI != null) sellSlotUI.OnItemSold -= HandleSellItem;
        //     if (eventManager != null)
        //     {
        //         eventManager.OnInteraction -= HandleInteraction;
        //     }
        // }

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
            Debug.Log("[ShopUI] 인벤토리 열기 이벤트 발생시킴!");
            eventManager.TriggerOpenInventoryForShop();
            UpdateCurrencyUI(CurrencyType.Gold, currencyManager.GetCurrency(CurrencyType.Gold));
            Open();
        }

        public override void Open()
        {
            base.Open();
            if (shopManager != null)
            {
                shopManager.SetShopState(true);
                shopManager.OnStockChanged += RefreshShopUI;
            }
            RefreshShopUI();
        }

        public override void Close()
        {
            base.Close();
            if (shopManager != null)
            {
                shopManager.SetShopState(false);
            }
            eventManager.TriggerCloseInventoryForShop();
            if (sellSlotUI != null) sellSlotUI.ClearSlot();

            if (shopManager != null)
            {
                shopManager.OnStockChanged -= RefreshShopUI;
            }
        }

        public void BuyItem(ShopSlotUI slot)
        {
            var shopItem = slot.CurrentItem;
            if (shopItem == null || (!shopItem.isUnlimited && shopItem.stock <= 0))
            {
                return;
            }

            int amountToBuy = (shopItem.item is EquipmentData) ? 1 : 1;

            if (currencyManager.SpendCurrency(CurrencyType.Gold, shopItem.price * amountToBuy))
            {
                if (inventoryManager.AddItem(shopItem.item, amountToBuy))
                {
                    Debug.Log($"[ShopUI] '{shopItem.item.itemName}' ({shopItem.item.GetType().Name}) 구매 완료. (ID: {shopItem.shopItemId}) from shop '{currentShopData.shopName}'");

                    if (shopManager != null && !shopItem.isUnlimited)
                    {
                        shopManager.RecordPurchase(currentShopData.shopName, shopItem.shopItemId);
                    }

                    if (shopItem.item is WeaponData weaponData)
                    {
                        var equipmentSlot = FindObjectOfType<EquipmentSlotUI>();
                        if (equipmentSlot != null && equipmentSlot.IsEmpty)
                        {
                            if (inventoryManager.RemoveItem(shopItem.item, 1))
                            {
                                equipmentSlot.SetItem(shopItem.item, 1);
                            }
                        }
                    }
                }
                else
                {
                    currencyManager.AddCurrency(CurrencyType.Gold, shopItem.price * amountToBuy);
                }
            }
        }

        private void HandleSellItem(ItemData item, int amount)
        {
            if (inventoryManager.RemoveItem(item, amount))
            {
                int totalPrice = Mathf.RoundToInt(item.baseValue * SELL_PRICE_RATIO) * amount;
                currencyManager.AddCurrency(CurrencyType.Gold, totalPrice);
            }
        }

        private void PopulateShop()
        {
            if (currentShopData == null) return;
            foreach (Transform child in shopSlotParent) Destroy(child.gameObject);

            var itemsToDisplay = shopManager.GetShopItems(currentShopData.shopName);
            if (itemsToDisplay == null) return;

            foreach (ShopItemData shopItem in itemsToDisplay)
            {
                ShopSlotUI slotUI = container.InstantiatePrefabForComponent<ShopSlotUI>(shopSlotPrefab, shopSlotParent);
                if (slotUI != null)
                {
                    slotUI.Setup(shopItem);
                }
            }
        }

        private void RefreshPlayerInventoryUI()
        {
            if (!IsOpen || playerInventorySlots == null) return;
            var items = inventoryManager.GetAllItems();
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