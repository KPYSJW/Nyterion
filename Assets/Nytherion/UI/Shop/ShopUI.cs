using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Nytherion.Data.Shop;
using Nytherion.Core;
using Nytherion.Data.ScriptableObjects.Items;
using Nytherion.UI.Inventory;
using TMPro;

namespace Nytherion.UI.Shop
{
    public class ShopUI : MonoBehaviour
    {
        public static ShopUI Instance;

        [Header("UI 패널")]
        [SerializeField] private GameObject shopPanel;

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

            playerInventorySlots = new List<InventorySlotUI>(playerInventoryParent.GetComponentsInChildren<InventorySlotUI>());
            foreach (InventorySlotUI slot in playerInventorySlots)
            {
                slot.OnSellItemAction += (baseSlot) => SellItem(baseSlot);
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
        }

        public bool IsShopOpen() => isShopOpen;

        public void OpenShop(ShopData data)
        {
            currentShopData = data;
            shopPanel.SetActive(true);
            isShopOpen = true;
            PopulateShop();
            RefreshPlayerInventoryUI();
            UpdateCurrencyUI(CurrencyType.Gold, CurrencyManager.Instance.GetCurrency(CurrencyType.Gold));
            if (InventoryUI.Instance != null)
            {
                InventoryUI.Instance?.OpenForShop();
            }
        }

        public void CloseShop()
        {
            shopPanel.SetActive(false);
            isShopOpen = false;
            if (InventoryUI.Instance != null)
            {
                InventoryUI.Instance?.CloseAllPanels();
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

        public void BuyItem(ShopItemData shopItem)
        {
            if (shopItem == null || shopItem.item == null) return;
            if (CurrencyManager.Instance.SpendCurrency(CurrencyType.Gold, shopItem.price))
            {
                if (!InventoryManager.Instance.AddItem(shopItem.item, 1))
                {
                    CurrencyManager.Instance.AddCurrency(CurrencyType.Gold, shopItem.price);
                    Debug.Log("인벤토리가 가득 찼습니다!");
                    // TODO: 유저에게 피드백 UI 표시
                }
                else
                {
                    Debug.Log($"{shopItem.item.itemName}을(를) 구매했습니다!");
                    // TODO: 구매 성공 피드백 UI  표시
                }
            }
            else
            {
                Debug.Log($"돈이 부족합니다!");
                // TODO: 돈이 부족한 피드백 UI 표시
            }
        }
        private void SellItem(BaseSlotUI slotToSell)
        {
            if (slotToSell.IsEmpty) return;

            ItemData item = slotToSell.CurrentItem;

            // 판매 가격은 아이템의 기본 가치의 절반으로 계산 (조정 가능)
            int sellPrice = Mathf.Max(1, Mathf.RoundToInt(item.baseValue * 0.5f));

            if (InventoryManager.Instance.RemoveItem(item, 1))
            {
                CurrencyManager.Instance.AddCurrency(CurrencyType.Gold, sellPrice);
                Debug.Log($"{item.itemName}을(를) {sellPrice} 골드에 판매했습니다!");
                // 인벤토리 UI는 OnInventoryUpdated 이벤트 덕분에 자동으로 갱신됩니다.
            }
        }
        private void RefreshPlayerInventoryUI()
        {
            if (!isShopOpen) return; // 상점이 닫혀있으면 갱신 안 함

            var items = InventoryManager.Instance.GetAllItems();
            int i = 0;

            foreach (var itemEntry in items)
            {
                if (i < playerInventorySlots.Count)
                {
                    playerInventorySlots[i].SetItem(itemEntry.Key, itemEntry.Value);
                    i++;
                }
            }
            for (; i < playerInventorySlots.Count; i++)
            {
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

    }
}

