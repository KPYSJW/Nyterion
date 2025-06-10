using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Nytherion.Data.Shop;
using Nytherion.Core;
using Nytherion.Data.ScriptableObjects.Items;

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

        [Header("버튼")]
        [SerializeField] private Button closeButton;

        private ShopData currentShopData;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
            }

            shopPanel.SetActive(false);
            if (closeButton != null)
            {
                closeButton.onClick.AddListener(CloseShop);
            }
        }

        public void OpenShop(ShopData data)
        {
            currentShopData = data;
            if (currentShopData == null)
            {
                Debug.LogError("표시할 상점 데이터가 없습니다.", this);
                return;
            }
            shopPanel.SetActive(true);
            PopulateShop();
        }

        public void CloseShop()
        {
            shopPanel.SetActive(false);
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
                bool added = InventoryManager.Instance.AddItem(shopItem.item, 1);
                if (!added)
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
        public void SellItem(ItemData item, int price)
        {
                if (item == null) return;

                if(InventoryManager.Instance.RemoveItem(item, 1))
                {
                    int sellPrice = Mathf.RoundToInt(price * 0.5f);
                    CurrencyManager.Instance.AddCurrency(CurrencyType.Gold, sellPrice);
                    Debug.Log($"{item.itemName}을(를) {sellPrice} 골드에 판매했습니다!");
                    // TODO: 플레이어 인벤토리 UI 갱신 로직 필요
                }
                
        }

    }
}

