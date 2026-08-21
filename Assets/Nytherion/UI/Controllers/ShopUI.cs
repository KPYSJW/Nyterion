using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Nytherion.Data.ScriptableObjects.Shop;
using Nytherion.Core.Managers;
using Nytherion.Data.ScriptableObjects.Items;
using Nytherion.UI.Inventory;
using TMPro;
using Nytherion.Core.Utils;
using Nytherion.Core.Enums;
using Nytherion.UI.Shop;
using Nytherion.Data.ScriptableObjects.Weapons;
using Nytherion.Core.Interfaces;
using VContainer;
using VContainer.Unity;
using Nytherion.GamePlay.Relics;
using Nytherion.Core.Systems;

namespace Nytherion.UI.Controllers
{
    public class ShopUI : UIPanelBase, IInitializable
    {
        private GameSceneUIRefs gameSceneuiRefs;
        private ShopSlotUI[] shopSlots;
        private TextMeshProUGUI goldText;
        private Button closeButton;

        [Header("Reroll Settings UI")]
        private Button shopRerollButton;
        private TextMeshProUGUI shopRerollCostText;
        private Button shopAdvancedRerollButton;
        private TextMeshProUGUI shopAdvancedRerollCostText;

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

        private GameObject emptyStateUI;

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
            this.closeButton = gameSceneuiRefs.ShopCloseButton;
            this.goldText = gameSceneuiRefs.ShopPlayerGoldText;
            this.shopSlots = gameSceneuiRefs.ShopSlots;
            this.inventorySlotParent = gameSceneuiRefs.InventorySlotParent;
            this.controlledCanvasGroup = gameSceneuiRefs.ShopCanvasGroup;
            this.emptyStateUI = gameSceneuiRefs.ShopEmptyStateUI;
            this.buyPopupUI = gameSceneuiRefs.ShopBuyPopupUI;
            this.sellPopupUI = gameSceneuiRefs.ShopSellPopupUI;
            this.shopRerollButton = gameSceneuiRefs.ShopRerollButton;
            this.shopRerollCostText = gameSceneuiRefs.ShopRerollCostText;
            this.shopAdvancedRerollButton = gameSceneuiRefs.ShopAdvancedRerollButton;
            this.shopAdvancedRerollCostText = gameSceneuiRefs.ShopAdvancedRerollCostText;
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

            LocalizationText.LanguageChanged += OnLanguageChanged;

            // UI 요소들 찾기
            FindUIElements();

            if (closeButton != null)
            {
                closeButton.onClick.AddListener(Close);
            }

            if (shopRerollButton != null)
            {
                shopRerollButton.onClick.AddListener(HandleReroll);
            }

            if (shopAdvancedRerollButton != null)
            {
                shopAdvancedRerollButton.onClick.AddListener(HandleAdvancedReroll);
            }

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

            Close();
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
            RefreshShopUI();

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

        public override void Open(bool closeOthers = true)
        {
            base.Open(closeOthers);
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
            ShopItemData shopItem = slot.CurrentItem;
            CurrencyDataManager currencyMgr = GetCurrencyDataManager();
            InventoryDataManager inventoryMgr = GetInventoryDataManager();

            int unitPrice = shopItem.price;

            // 쿠폰 조각 (CouponPiece) 유물 효과 적용: 상점 상품 가격 15% 할인
            RelicManager relicManager = UnityEngine.Object.FindObjectOfType<RelicManager>();
            if (relicManager != null)
            {
                foreach (KeyValuePair<string, Vector2Int> pair in relicManager.GetPlacedBlocks())
                {
                    RelicBlock block = relicManager.GetBlockAt(pair.Value.y, pair.Value.x);
                    if (block != null && block.RelicId == "CouponPiece" && !block.SourceData.isDisabled)
                    {
                        unitPrice = Mathf.RoundToInt(unitPrice * 0.85f);
                        break;
                    }
                }
            }

            int totalPrice = unitPrice * amountToBuy; 

            if (!currencyMgr.HasCurrency(CurrencyType.Gold, totalPrice)) return;

            if (currencyMgr.SpendCurrency(CurrencyType.Gold, totalPrice))
            {
                bool addSuccess = false;
                if (shopItem.item is Data.ScriptableObjects.Items.EquipmentData eq)
                {
                    addSuccess = inventoryMgr.AddEquipmentInstance(eq);
                }
                else
                {
                    addSuccess = inventoryMgr.AddItem(shopItem.item, amountToBuy);
                }

                if (addSuccess) 
                {
                    ShopManager shopMgr = GetShopManager();
                    if (shopMgr != null && !shopItem.isUnlimited)
                    {
                        shopMgr.RecordPurchase(currentShopData.shopName, shopItem.shopItemId, amountToBuy);
                    }

                    // 쿠폰 조각 (CouponPiece) 장착 상태로 상점 아이템 구매 업적 연동
                    bool hasCoupon = false;
                    if (relicManager != null)
                    {
                        foreach (KeyValuePair<string, Vector2Int> pair in relicManager.GetPlacedBlocks())
                        {
                            RelicBlock block = relicManager.GetBlockAt(pair.Value.y, pair.Value.x);
                            if (block != null && block.RelicId == "CouponPiece" && !block.SourceData.isDisabled)
                            {
                                hasCoupon = true;
                                break;
                            }
                        }
                    }

                    if (hasCoupon)
                    {
                        ProgressionManager progressionManager = DataLifetimeScope.Instance != null ? DataLifetimeScope.Instance.GetDataManager<ProgressionManager>() : null;
                        if (progressionManager != null)
                        {
                            progressionManager.ProcessAction(ProgressionType.BuyShopItem, amountToBuy);
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
            if (currentShopData == null || shopSlots == null || shopSlots.Length == 0) return;

            ShopManager shopMgr = GetShopManager();
            if (shopMgr == null) return;

            List<ShopItemData> itemsToDisplay = shopMgr.GetShopItems(currentShopData.shopName);

            bool isEmpty = (itemsToDisplay == null || itemsToDisplay.Count == 0);
            if (emptyStateUI != null)
            {
                emptyStateUI.SetActive(isEmpty);

                TMPro.TextMeshProUGUI textUI = emptyStateUI.GetComponent<TMPro.TextMeshProUGUI>();
                if (textUI != null)
                {
                    textUI.text = LocalizationText.Get(
                        LocalizationTables.UI,
                        "ui.shop.no_items",
                        "판매 중인 아이템이 없습니다.",
                        "No items on sale.");
                }
            }

            for (int i = 0; i < shopSlots.Length; i++)
            {
                if (shopSlots[i] == null) continue;

                if (container != null)
                {
                    container.Inject(shopSlots[i]);
                }

                if (itemsToDisplay != null && i < itemsToDisplay.Count)
                {
                    shopSlots[i].gameObject.SetActive(true);
                    shopSlots[i].Setup(itemsToDisplay[i], currentShopData.shopName);
                }
                else
                {
                    shopSlots[i].gameObject.SetActive(false);
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
            LocalizationText.LanguageChanged -= OnLanguageChanged;

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
        }

        private void OnLanguageChanged()
        {
            if (currentShopData == null)
            {
                return;
            }

            RefreshShopUI();
            RefreshPlayerInventoryUI();
        }
        private void RefreshShopUI()
        {
            PopulateShop();
            UpdateRerollUI();
        }

        private void UpdateRerollUI()
        {
            ShopManager shopMgr = GetShopManager();
            
            if (shopMgr != null)
            {
                bool isNormalShop = currentShopData != null;
                if (shopRerollButton != null)
                {
                    shopRerollButton.gameObject.SetActive(isNormalShop);
                }
                
                if (isNormalShop && shopRerollCostText != null)
                {
                    shopRerollCostText.text = $"{shopMgr.CurrentRerollCost}";
                }

                if (shopAdvancedRerollButton != null)
                {
                    shopAdvancedRerollButton.gameObject.SetActive(isNormalShop);
                }

                if (isNormalShop && shopAdvancedRerollCostText != null)
                {
                    shopAdvancedRerollCostText.text = $"{shopMgr.AdvancedRerollTokenCost}";
                }
            }
        }

        private void HandleReroll()
        {
            ShopManager shopMgr = GetShopManager();
            
            if (shopMgr == null || currentShopData == null) return;

            bool success = shopMgr.RerollShop(currentShopData.shopName, false);
            Debug.Log($"[ShopUI] RerollShop 결과: {success}");

            if (success)
            {
                RefreshShopUI();
            }
        }

        private void HandleAdvancedReroll()
        {
            ShopManager shopMgr = GetShopManager();

            if (shopMgr == null || currentShopData == null) return;

            bool success = shopMgr.RerollShopAdvanced(currentShopData.shopName, shopMgr.AdvancedRerollTokenCost);

            if (success)
            {
                RefreshShopUI();
            }
        }
    }
}
