using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Nytherion.Data.ScriptableObjects.Items;
using Nytherion.Data.Shop;

namespace Nytherion.UI.Shop
{
    public class ShopSlotUI : MonoBehaviour
    {
        [SerializeField] private Image iconImage;
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI priceText;
        [SerializeField] private Button buyButton;

        private ShopItemData currentItem;

        public void Setup(ShopItemData shopItem)
        {
            currentItem = shopItem;

            if (currentItem != null && currentItem.item != null)
            {
                iconImage.sprite = currentItem.item.icon;
                nameText.text = currentItem.item.itemName;
                priceText.text = $"{currentItem.price} Gold";
                buyButton.onClick.RemoveAllListeners();
                buyButton.onClick.AddListener(OnBuyButtonClicked);
                gameObject.SetActive(true);
            }
            else
            {
                gameObject.SetActive(false);
            }
        }

        private void OnBuyButtonClicked()
        {
            if (ShopUI.Instance != null)
            {
                ShopUI.Instance.BuyItem(currentItem);
            }
        }
    }
}

