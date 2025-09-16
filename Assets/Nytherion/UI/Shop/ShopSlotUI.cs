using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Nytherion.Data.ScriptableObjects.Shop;
using Nytherion.UI.Controllers;
using Nytherion.Core.Managers;
using VContainer;

namespace Nytherion.UI.Shop
{
    public class ShopSlotUI : MonoBehaviour
    {
        [SerializeField] private Image iconImage;
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI priceText;
        [SerializeField] private TextMeshProUGUI descriptionText;
        [SerializeField] private TextMeshProUGUI stockText;
        [SerializeField] private Button buyButton;
        [SerializeField] private CanvasGroup canvasGroup;

        public ShopItemData CurrentItem { get; private set; }
        private ShopUI shopUI;
        private string currentShopName;

        [Inject]
        public void Construct(ShopUI shopUI)
        {
            this.shopUI = shopUI;
        }

        public void Setup(ShopItemData shopItem, string shopName = "")
        {
            CurrentItem = shopItem;
            currentShopName = shopName;

            if (CurrentItem != null && CurrentItem.item != null)
            {
                iconImage.sprite = CurrentItem.item.icon;
                nameText.text = CurrentItem.item.itemName;
                priceText.text = $"{CurrentItem.price} Gold";
                descriptionText.text = CurrentItem.item.description;
                stockText.text = CurrentItem.isUnlimited ? "" : $"X {CurrentItem.stock}";

                buyButton.onClick.RemoveAllListeners();
                buyButton.onClick.AddListener(OnBuyButtonClicked);
                gameObject.SetActive(true);

                if (IsSoldOut())
                {
                    ApplySoldOutVisual();
                }
                else
                {
                    ResetVisual();
                }
            }
            else
            {
                gameObject.SetActive(false);
            }
        }
        public void UpdateStockUI()
        {
            if (CurrentItem != null)
            {
                // ShopManager에서 최신 재고 정보 가져오기
                var shopManager = FindObjectOfType<ShopManager>();
                if (shopManager != null && !string.IsNullOrEmpty(currentShopName))
                {
                    var shopItems = shopManager.GetShopItems(currentShopName);
                    if (shopItems != null)
                    {
                        var updatedItem = shopItems.Find(item => item.shopItemId == CurrentItem.shopItemId);
                        if (updatedItem != null)
                        {
                            // 실제 ShopManager의 재고로 업데이트
                            CurrentItem.stock = updatedItem.stock;
                            Debug.Log($"[ShopSlotUI] '{CurrentItem.item.itemName}' 재고 업데이트: {CurrentItem.stock}");
                        }
                    }
                }

                // 재고 표시 업데이트
                if (stockText != null)
                {
                    stockText.text = CurrentItem.isUnlimited ? "" : $"X {CurrentItem.stock}";
                }

                // 매진 상태 확인 및 시각적 업데이트
                if (IsSoldOut())
                {
                    ApplySoldOutVisual();
                    Debug.Log($"[ShopSlotUI] '{CurrentItem.item.itemName}' 매진 처리");
                }
                else
                {
                    ResetVisual();
                }
            }
        }

        public void SetInteractable(bool interactable)
        {
            if (buyButton != null)
            {
                buyButton.interactable = interactable;
            }

            if (canvasGroup != null)
            {
                canvasGroup.interactable = interactable;
                canvasGroup.blocksRaycasts = interactable;
            }
        }

        private void OnBuyButtonClicked()
        {
            if (shopUI != null)
            {
                shopUI.BuyItem(this);
            }
        }
        private void ApplySoldOutVisual()
        {
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0.2f;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }
        }
        private bool IsSoldOut()
        {
            return !CurrentItem.isUnlimited && CurrentItem.stock <= 0;
        }
        private void ResetVisual()
        {
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
                canvasGroup.interactable = true;
                canvasGroup.blocksRaycasts = true;
            }
        }
    }
}

