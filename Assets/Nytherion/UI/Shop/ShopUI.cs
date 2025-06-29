using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Nytherion.Data.Shop;
using Nytherion.Core;
using Nytherion.Data.ScriptableObjects.Items;
using Nytherion.UI.Inventory;
using TMPro;

namespace Nytherion.UI.Shop
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
        [Tooltip("상점 UI에 표시될 플레이어 인벤토리 슬롯들의 부모 오브젝트")]
        [SerializeField] private Transform playerInventoryParent;
        private List<InventorySlotUI> playerInventorySlots;

        private ShopData currentShopData;
        private const float SELL_PRICE_RATIO = 0.7f;
        protected override void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("씬에 여러 개의 ShopUI가 존재하여 이 인스턴스를 파괴합니다.", this.gameObject);
                Destroy(this.gameObject);
                return;
            }
            Instance = this;
            Debug.Log("ShopUI.Instance가 설정되었습니다. ID: " + this.GetInstanceID() + ", 오브젝트: " + this.gameObject.name);

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

            if (playerInventoryParent != null)
            {
                playerInventorySlots = new List<InventorySlotUI>(playerInventoryParent.GetComponentsInChildren<InventorySlotUI>(true));
            }
            else
            {
                Debug.LogError("ShopUI: playerInventoryParent가 할당되지 않았습니다!", this);
            }
        }

        private void OnDisable()
        {
            if (CurrencyManager.Instance != null) CurrencyManager.Instance.onCurrencyChanged -= UpdateCurrencyUI;
            if (InventoryManager.Instance != null) InventoryManager.Instance.OnInventoryUpdated -= RefreshPlayerInventoryUI;
            if (SellSlotUI.Instance != null) SellSlotUI.Instance.OnItemSold -= HandleSellItem;
        }

        public void OpenShop(ShopData data)
        {
            currentShopData = data;
            PopulateShop();
            if (InventoryUI.Instance != null) InventoryUI.Instance.OpenForShop();
            UpdateCurrencyUI(CurrencyType.Gold, CurrencyManager.Instance.GetCurrency(CurrencyType.Gold));
            Open();
        }

        public override void Close()
        {
            base.Close();
            if (InventoryUI.Instance != null) InventoryUI.Instance.Close();
            if (SellSlotUI.Instance != null) SellSlotUI.Instance.ClearSlot();
        }

        public void BuyItem(ShopSlotUI slot)
        {
            var shopItem = slot.CurrentItem;
            if (!shopItem.isUnlimited && shopItem.stock <= 0) return;

            if (CurrencyManager.Instance.SpendCurrency(CurrencyType.Gold, shopItem.price))
            {
                InventoryManager.Instance.AddItem(shopItem.item);
                if (!shopItem.isUnlimited) shopItem.stock--;
                slot.UpdateStockUI();
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
            foreach (ShopItemData shopItem in currentShopData.itemsForSale)
            {
                GameObject slotGO = Instantiate(shopSlotPrefab, shopSlotParent);
                if (slotGO.TryGetComponent(out ShopSlotUI slotUI)) slotUI.Setup(shopItem);
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
    }
}