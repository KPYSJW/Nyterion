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
using Nytherion.Core.Interfaces;
using VContainer;
using VContainer.Unity;

namespace Nytherion.UI.Controllers
{
    public class ShopUI : UIPanelBase, IInitializable
    {
        private GameSceneUIRefs gameSceneuiRefs;
        [Header("UI References")]
        private Transform shopSlotParent;
        private GameObject shopSlotPrefab;
        private TextMeshProUGUI goldText;
        private Button closeButton;

        [Header("Player Inventory Display")]
        private Transform inventorySlotParent;
        private List<InventorySlotUI> inventorySlots;

        private ShopData currentShopData;
        private const float SELL_PRICE_RATIO = 0.7f;
        private InventoryDataManager inventoryDataManager;
        private CurrencyDataManager currencyDataManager;
        private EventManager eventManager;
        private ShopManager shopManager;
        //private SellSlotUI sellSlotUI;

        [Header("Buyback UI")]
        private GameObject emptyStateUI;
        [SerializeField] private Button buyTabButton;      
        [SerializeField] private Button buybackTabButton;  
        private bool isBuybackMode = false;

        private IObjectResolver container;

        private BuyPopupUI buyPopupUI;
        private SellPopupUI sellPopupUI;

        [Inject]
        public void Construct(IObjectResolver container,
            GameSceneUIRefs gameSceneuiRefs,
            ShopManager shopManagerPrefab,
            CurrencyDataManager currencyDataManagerPrefab,
            EventManager eventManagerPrefab)
        {
            this.container = container;
            this.gameSceneuiRefs = gameSceneuiRefs;
            this.shopManager = shopManagerPrefab;
            this.currencyDataManager = currencyDataManagerPrefab;
            this.eventManager = eventManagerPrefab;
            //this.sellSlotUI = gameSceneuiRefs.SellSlotUI;
            this.closeButton = gameSceneuiRefs.ShopCloseButton;
            this.goldText = gameSceneuiRefs.ShopPlayerGoldText;
            this.shopSlotParent = gameSceneuiRefs.ShopSlotParent;
            this.shopSlotPrefab = gameSceneuiRefs.ShopSlotPrefab;
            this.inventorySlotParent = gameSceneuiRefs.InventorySlotParent;
            this.controlledCanvasGroup = gameSceneuiRefs.ShopCanvasGroup;
            this.buyTabButton = gameSceneuiRefs.ShopBuyTabButton;
            this.buybackTabButton = gameSceneuiRefs.ShopBuybackTabButton;
            this.emptyStateUI = gameSceneuiRefs.ShopEmptyStateUI;
            this.buyPopupUI = gameSceneuiRefs.ShopBuyPopupUI;
            this.sellPopupUI = gameSceneuiRefs.ShopSellPopupUI;
        }

        private InventoryDataManager GetInventoryDataManager()
        {
            if (inventoryDataManager == null && container != null)
            {
                try
                {
                    inventoryDataManager = container.Resolve<InventoryDataManager>();
                }
                catch (VContainerException e)
                {
                    Debug.LogError($"[ShopUI] Failed to resolve InventoryDataManager: {e.Message}");
                    return null;
                }
            }
            return inventoryDataManager;
        }

        private CurrencyDataManager GetCurrencyDataManager()
        {
            if (currencyDataManager == null && container != null)
            {
                try
                {
                    currencyDataManager = container.Resolve<CurrencyDataManager>();
                }
                catch (VContainerException e)
                {
                    Debug.LogError($"[ShopUI] Failed to resolve CurrencyDataManager: {e.Message}");
                    return null;
                }
            }
            return currencyDataManager;
        }

        private EventManager GetEventManager()
        {
            if (eventManager == null && container != null)
            {
                try
                {
                    eventManager = container.Resolve<EventManager>();
                }
                catch (VContainerException e)
                {
                    Debug.LogError($"[ShopUI] Failed to resolve EventManager: {e.Message}");
                    return null;
                }
            }
            return eventManager;
        }

        private ShopManager GetShopManager()
        {
            if (shopManager == null && container != null)
            {
                try
                {
                    shopManager = container.Resolve<ShopManager>();
                }
                catch (VContainerException e)
                {
                    Debug.LogError($"[ShopUI] Failed to resolve ShopManager: {e.Message}");
                    return null;
                }
            }
            return shopManager;
        }

        private void FindUIElements()
        {
            // UI 요소들을 동적으로 찾기
            if (controlledCanvasGroup == null)
                controlledCanvasGroup = GetComponent<CanvasGroup>();

            if (closeButton == null)
                closeButton = GetComponentInChildren<Button>();

            if (goldText == null)
                goldText = GetComponentInChildren<TextMeshProUGUI>();

        }

        protected override void Awake()
        {
            base.Awake();
        }
        public void Initialize()
        {

            // UI 요소들 찾기
            FindUIElements();

            if (closeButton != null)
            {
                closeButton.onClick.AddListener(Close);
            }

            if (buyTabButton != null) buyTabButton.onClick.AddListener(() => ChangeTab(false));
            if (buybackTabButton != null) buybackTabButton.onClick.AddListener(() => ChangeTab(true));

            // SellSlotUI 이벤트 구독 처리
            //if (sellSlotUI != null)
            //{
            //    sellSlotUI.OnItemSold -= HandleSellItem;
            //    sellSlotUI.OnItemSold += HandleSellItem;
            //}
            //else
            //{
            //    Debug.LogError("[ShopUI] SellSlotUI is null, cannot subscribe to OnItemSold event");
            //}

            CurrencyDataManager currencyMgr = GetCurrencyDataManager();
            if (currencyMgr != null)
            {
                currencyMgr.OnDataChanged += OnCurrencyDataChanged;
            }

            InventoryDataManager inventoryMgr = GetInventoryDataManager();
            if (inventoryMgr != null)
            {
                inventoryMgr.OnDataChanged += OnInventoryDataChanged;
            }

            EventManager eventMgr = GetEventManager();
            if (eventMgr != null)
            {
                eventMgr.OnInteraction += HandleInteraction;
            }
            if (inventorySlotParent != null)
            {
                inventorySlots = new List<InventorySlotUI>(inventorySlotParent.GetComponentsInChildren<InventorySlotUI>(true));
            }

            ShopManager shopMgr = GetShopManager();
            if (shopMgr != null)
            {
                shopMgr.OnBuybackChanged += RefreshShopUI;
            }
            Close();
        }
        private void ChangeTab(bool toBuyback)
        {
            isBuybackMode = toBuyback;

            if (buyTabButton != null) buyTabButton.interactable = toBuyback;
            if (buybackTabButton != null) buybackTabButton.interactable = !toBuyback;

            RefreshShopUI();
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
            isBuybackMode = false;
            currentShopData = data;
            PopulateShop();
            ChangeTab(false);
            EventManager eventMgr = GetEventManager();
            if (eventMgr != null)
            {
                eventMgr.TriggerOpenInventoryForShop();
            }

            CurrencyDataManager currencyMgr = GetCurrencyDataManager();
            if (currencyMgr != null)
            {
                UpdateCurrencyUI(CurrencyType.Gold, currencyMgr.GetCurrency(CurrencyType.Gold));
            }

            Open();
        }
        public void OpenSellPopup(ItemData item, int maxAmount)
        {
            int unitSellPrice = Mathf.RoundToInt(item.baseValue * SELL_PRICE_RATIO);

            if (sellPopupUI != null)
            {
                sellPopupUI.Setup(this, item, maxAmount, unitSellPrice);
            }
            else
            {
                QuickSellItem(item, maxAmount);
            }
        }

        public override void Open()
        {
            base.Open();
            ShopManager shopMgr = GetShopManager();
            if (shopMgr != null)
            {
                shopMgr.SetShopState(true);
                shopMgr.OnStockChanged += RefreshShopUI;
            }
            RefreshShopUI();
        }

        public override void Close()
        {
            base.Close();
            ShopManager shopMgr = GetShopManager();
            if (shopMgr != null)
            {
                shopMgr.SetShopState(false);
                shopMgr.OnStockChanged -= RefreshShopUI;
            }

            EventManager eventMgr = GetEventManager();
            if (eventMgr != null)
            {
                eventMgr.TriggerCloseInventoryForShop();
            }

            //if (sellSlotUI != null) sellSlotUI.ClearSlot();
        }

        public void BuyItem(ShopSlotUI slot)
        {
            var shopItem = slot.CurrentItem;
            if (shopItem == null || (!shopItem.isUnlimited && shopItem.stock <= 0)) return;

            CurrencyDataManager currencyMgr = GetCurrencyDataManager();
            if (currencyMgr == null) return;

            int playerGold = currencyMgr.GetCurrency(CurrencyType.Gold);
            int maxAffordable = shopItem.price > 0 ? playerGold / shopItem.price : 99;

            if (maxAffordable <= 0)
            {
                Debug.Log("[ShopUI] 골드가 부족하여 1개도 살 수 없습니다.");
                return;
            }

            
            if (shopItem.item is Data.ScriptableObjects.Items.EquipmentData)
            {
                ConfirmPurchase(slot, 1);
            }
            else
            {
                if (buyPopupUI != null)
                {
                    buyPopupUI.Setup(this, slot, maxAffordable);
                }
                else
                {
                    ConfirmPurchase(slot, 1); 
                }
            }
        }

        public void ConfirmPurchase(ShopSlotUI slot, int amountToBuy)
        {
            var shopItem = slot.CurrentItem;
            CurrencyDataManager currencyMgr = GetCurrencyDataManager();
            InventoryDataManager inventoryMgr = GetInventoryDataManager();

            int totalPrice = shopItem.price * amountToBuy; 

            if (!currencyMgr.HasCurrency(CurrencyType.Gold, totalPrice)) return;

            if (currencyMgr.SpendCurrency(CurrencyType.Gold, totalPrice))
            {
                if (inventoryMgr.AddItem(shopItem.item, amountToBuy)) 
                {
                    ShopManager shopMgr = GetShopManager();
                    if (shopMgr != null)
                    {
                        if (isBuybackMode)
                        {
                            shopMgr.RecordBuybackPurchase(shopItem.shopItemId, amountToBuy);
                        }
                        else if (!shopItem.isUnlimited)
                        {
                            shopMgr.RecordPurchase(currentShopData.shopName, shopItem.shopItemId, amountToBuy);
                        }
                    }

                    if (shopItem.isUnlimited) slot.SetInteractable(true);

                }
                else
                {
                    currencyMgr.AddCurrency(CurrencyType.Gold, totalPrice);
                    slot.SetInteractable(true);
                }
            }

            slot.UpdateStockUI(); // 재고 UI 갱신 (선택 사항: RefreshShopUI()를 호출해도 좋습니다)
        }
        public void HandleSellItem(ItemData item, int amount)
        {

            InventoryDataManager inventoryMgr = GetInventoryDataManager();
            CurrencyDataManager currencyMgr = GetCurrencyDataManager();
            ShopManager shopMgr = GetShopManager();

            if (inventoryMgr == null || currencyMgr == null)
            {
                Debug.LogError("[ShopUI] 판매 처리에 필요한 매니저가 없습니다.");
                return;
            }

            // 현재 인벤토리에서 해당 아이템의 개수 확인
            int currentCount = inventoryMgr.GetItemCount(item.ID);

            if (currentCount < amount)
            {
                Debug.LogWarning($"[ShopUI] 판매 실패: 인벤토리에 충분한 아이템이 없습니다. 요청: {amount}, 보유: {currentCount}");
                return;
            }

            if (inventoryMgr.RemoveItem(item.ID, amount))
            {
                int unitSellPrice = Mathf.RoundToInt(item.baseValue * SELL_PRICE_RATIO);
                int totalPrice = unitSellPrice * amount;

                currencyMgr.AddCurrency(CurrencyType.Gold, totalPrice);

                if (shopMgr != null)
                {
                    shopMgr.AddToBuyback(item, amount, unitSellPrice);
                }
            }
            else
            {
                Debug.LogWarning($"[ShopUI] '{item.itemName}' 판매 실패: 인벤토리에서 아이템 제거 실패");
            }
        }
        public void QuickSellItem(ItemData item, int amount)
        {
            HandleSellItem(item, amount);
        }
        //public void OpenSellPopup(ItemData item, int maxAmount)
        //{
        //    if (sellSlotUI != null)
        //    {
        //        sellSlotUI.gameObject.SetActive(true);
        //        sellSlotUI.SetItem(item, maxAmount);
        //    }
        //    else
        //    {
        //        Debug.LogWarning("SellslotUI가 할당되지 않았다");
        //    }
        //}
        private void PopulateShop()
        {
            if (currentShopData == null || shopSlotParent == null) return;

            foreach (Transform child in shopSlotParent)
            {
                if (child != null) Destroy(child.gameObject);
            }

            ShopManager shopMgr = GetShopManager();
            if (shopMgr == null) return;

            List<ShopItemData> itemsToDisplay;
            if (isBuybackMode)
            {
                itemsToDisplay = shopMgr.GetBuybackItems();
            }
            else
            {
                itemsToDisplay = shopMgr.GetShopItems(currentShopData.shopName);
            }

            bool isEmpty = (itemsToDisplay == null || itemsToDisplay.Count == 0);
            if (emptyStateUI != null)
            {
                emptyStateUI.SetActive(isEmpty);

                var textUI = emptyStateUI.GetComponent<TMPro.TextMeshProUGUI>();
                if (textUI != null)
                {
                    textUI.text = isBuybackMode ? "No items available for repurchase" : "No items on sale.";
                }
            }

            if (isEmpty) return; // 보여줄 아이템이 없으면 여기서 종료

            foreach (ShopItemData shopItem in itemsToDisplay)
            {
                ShopSlotUI slotUI = container.Instantiate(shopSlotPrefab, shopSlotParent).GetComponent<ShopSlotUI>();
                if (slotUI != null)
                {
                    slotUI.Setup(shopItem, currentShopData.shopName);
                }
            }
        }

        private void RefreshPlayerInventoryUI()
        {
            if (!IsOpen || inventorySlots == null) return;

            InventoryDataManager inventoryMgr = GetInventoryDataManager();
            if (inventoryMgr == null) return;

            var items = inventoryMgr.GetAllItems();
            int i = 0;
            foreach (var itemEntry in items)
            {
                if (i >= inventorySlots.Count) break;
                if (inventorySlots[i] != null && itemEntry.item != null)
                {
                    inventorySlots[i].SetItem(itemEntry.item, itemEntry.count);
                }
                i++;
            }
            for (; i < inventorySlots.Count; i++)
            {
                if (inventorySlots[i] != null) inventorySlots[i].ClearSlot();
            }
        }

        private void UpdateCurrencyUI(CurrencyType type, int amount)
        {
            if (type == CurrencyType.Gold && goldText != null)
            {
                goldText.text = $"{amount} G";
            }
        }

        // 새로운 DataManager 이벤트 핸들러들
        private void OnCurrencyDataChanged(CurrencyChangeData changeData)
        {
            UpdateCurrencyUI(changeData.currencyType, changeData.newAmount);
        }

        private void OnInventoryDataChanged(InventoryChangeData changeData)
        {
            RefreshPlayerInventoryUI();
        }

        private void OnDestroy()
        {
            CurrencyDataManager currencyMgr = GetCurrencyDataManager();
            if (currencyMgr != null)
            {
                currencyMgr.OnDataChanged -= OnCurrencyDataChanged;
            }

            InventoryDataManager inventoryMgr = GetInventoryDataManager();
            if (inventoryMgr != null)
            {
                inventoryMgr.OnDataChanged -= OnInventoryDataChanged;
            }

            // ★ 이 부분이 에러를 해결하는 핵심입니다! (이벤트 구독 해제)
            ShopManager shopMgr = GetShopManager();
            if (shopMgr != null)
            {
                shopMgr.OnBuybackChanged -= RefreshShopUI;
            }

            //if (sellSlotUI != null)
            //{
            //    sellSlotUI.OnItemSold -= HandleSellItem;
            //}
        }
        private void RefreshShopUI()
        {
            PopulateShop();
        }
    }
}