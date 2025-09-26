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
        private SellSlotUI sellSlotUI;

        private IObjectResolver container;

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
            this.sellSlotUI = gameSceneuiRefs.SellSlotUI;
            this.closeButton = gameSceneuiRefs.ShopCloseButton;
            this.goldText = gameSceneuiRefs.ShopPlayerGoldText;
            this.shopSlotParent = gameSceneuiRefs.ShopSlotParent;
            this.shopSlotPrefab = gameSceneuiRefs.ShopSlotPrefab;
            this.inventorySlotParent = gameSceneuiRefs.InventorySlotParent;
            this.controlledCanvasGroup = gameSceneuiRefs.ShopCanvasGroup;
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

            // SellSlotUI는 GameSceneUIRefs에서 이미 설정됨
            if (sellSlotUI != null)
            {
            }
            else
            {
                Debug.LogError("[ShopUI] SellSlotUI is null from GameSceneUIRefs!");
            }
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

            // SellSlotUI 이벤트 구독 처리
            if (sellSlotUI != null)
            {
                sellSlotUI.OnItemSold -= HandleSellItem;
                sellSlotUI.OnItemSold += HandleSellItem;
            }
            else
            {
                Debug.LogError("[ShopUI] SellSlotUI is null, cannot subscribe to OnItemSold event");
            }

            // Lazy resolution으로 매니저들 가져오기
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

            if (sellSlotUI != null) sellSlotUI.ClearSlot();
        }

        public void BuyItem(ShopSlotUI slot)
        {
            var shopItem = slot.CurrentItem;
            if (shopItem == null || (!shopItem.isUnlimited && shopItem.stock <= 0))
            {
                Debug.Log($"[ShopUI] 구매 불가: 아이템이 없거나 재고가 부족합니다.");
                return;
            }

            // 구매 중복 방지를 위해 즉시 버튼 비활성화
            slot.SetInteractable(false);

            int amountToBuy = (shopItem.item is EquipmentData) ? 1 : 1;
            CurrencyDataManager currencyMgr = GetCurrencyDataManager();
            InventoryDataManager inventoryMgr = GetInventoryDataManager();

            if (currencyMgr == null || inventoryMgr == null)
            {
                Debug.LogError("[ShopUI] 필수 매니저가 없습니다.");
                slot.SetInteractable(true);
                return;
            }

            // 재고 및 가격 재확인
            if (!currencyMgr.HasCurrency(CurrencyType.Gold, shopItem.price * amountToBuy))
            {
                Debug.Log($"[ShopUI] 골드 부족: 필요 {shopItem.price * amountToBuy}, 보유 {currencyMgr.GetCurrency(CurrencyType.Gold)}");
                slot.SetInteractable(true);
                return;
            }

            Debug.Log($"[ShopUI] 구매 시도: {shopItem.item.itemName}, 가격: {shopItem.price * amountToBuy}, 보유 골드: {currencyMgr.GetCurrency(CurrencyType.Gold)}");

            if (currencyMgr.SpendCurrency(CurrencyType.Gold, shopItem.price * amountToBuy))
            {
                Debug.Log($"[ShopUI] 재화 차감 성공, 인벤토리에 아이템 추가 시도: {shopItem.item.itemName}");

                if (inventoryMgr.AddItem(shopItem.item, amountToBuy))
                {
                    Debug.Log($"[ShopUI] '{shopItem.item.itemName}' 구매 완료! 인벤토리 추가 성공 (ID: {shopItem.shopItemId})");

                    // 재고 감소 처리
                    if (!shopItem.isUnlimited)
                    {
                        ShopManager shopMgr = GetShopManager();
                        if (shopMgr != null)
                        {
                            shopMgr.RecordPurchase(currentShopData.shopName, shopItem.shopItemId);
                        }

                        // 즉시 UI 업데이트
                        slot.UpdateStockUI();
                    }
                    else
                    {
                        // 무제한 아이템도 버튼 다시 활성화
                        slot.SetInteractable(true);
                    }

                    // 무기 자동 장착 로직
                    if (shopItem.item is WeaponData weaponData)
                    {
                        var equipmentSlot = FindObjectOfType<EquipmentSlotUI>();
                        if (equipmentSlot != null && equipmentSlot.IsEmpty)
                        {
                            if (inventoryMgr.RemoveItem(shopItem.item.ID, 1))
                            {
                                equipmentSlot.SetItem(shopItem.item, 1);
                            }
                        }
                    }
                }
                else
                {
                    // 인벤토리 추가 실패 시 환불
                    currencyMgr.AddCurrency(CurrencyType.Gold, shopItem.price * amountToBuy);
                    Debug.LogWarning("[ShopUI] 인벤토리가 가득참. 구매가 취소되었습니다.");
                    slot.SetInteractable(true);
                }
            }
            else
            {
                Debug.LogWarning("[ShopUI] 골드 지출 실패.");
                slot.SetInteractable(true);
            }
        }

        private void HandleSellItem(ItemData item, int amount)
        {
            Debug.Log($"[ShopUI] HandleSellItem called: {item?.itemName} x{amount}, ID: {item?.ID}");

            InventoryDataManager inventoryMgr = GetInventoryDataManager();
            CurrencyDataManager currencyMgr = GetCurrencyDataManager();

            if (inventoryMgr == null || currencyMgr == null)
            {
                Debug.LogError("[ShopUI] 판매 처리에 필요한 매니저가 없습니다.");
                return;
            }

            // 현재 인벤토리에서 해당 아이템의 개수 확인
            int currentCount = inventoryMgr.GetItemCount(item.ID);
            Debug.Log($"[ShopUI] 인벤토리에 있는 '{item.itemName}' 개수: {currentCount}");

            if (currentCount < amount)
            {
                Debug.LogWarning($"[ShopUI] 판매 실패: 인벤토리에 충분한 아이템이 없습니다. 요청: {amount}, 보유: {currentCount}");
                return;
            }

            if (inventoryMgr.RemoveItem(item.ID, amount))
            {
                int totalPrice = Mathf.RoundToInt(item.baseValue * SELL_PRICE_RATIO) * amount;
                currencyMgr.AddCurrency(CurrencyType.Gold, totalPrice);
                Debug.Log($"[ShopUI] '{item.itemName}' {amount}개 판매 완료. 획득 골드: {totalPrice}");
            }
            else
            {
                Debug.LogWarning($"[ShopUI] '{item.itemName}' 판매 실패: 인벤토리에서 아이템 제거 실패");
            }
        }

        private void PopulateShop()
        {
            if (currentShopData == null) return;
            foreach (Transform child in shopSlotParent) Destroy(child.gameObject);

            ShopManager shopMgr = GetShopManager();
            if (shopMgr == null) return;

            var itemsToDisplay = shopMgr.GetShopItems(currentShopData.shopName);
            if (itemsToDisplay == null) return;

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
            // 이벤트 구독 해제
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

            if (sellSlotUI != null)
            {
                sellSlotUI.OnItemSold -= HandleSellItem;
            }
        }

        private void RefreshShopUI()
        {
            PopulateShop();
        }
    }
}