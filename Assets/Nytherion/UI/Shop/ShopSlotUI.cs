using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Nytherion.Data.ScriptableObjects.Shop;
using Nytherion.UI.Controllers;
using Zenject;

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

        [Inject]
        public void Construct(ShopUI shopUI)
        {
            this.shopUI = shopUI;
        }

        public void Setup(ShopItemData shopItem)
        {
            CurrentItem = shopItem;

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
                stockText.text = CurrentItem.isUnlimited ? "" : $"X {CurrentItem.stock}";
                if (IsSoldOut())
                {
                    ApplySoldOutVisual();
                }
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

