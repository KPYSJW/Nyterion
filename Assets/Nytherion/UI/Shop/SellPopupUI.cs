using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Nytherion.Data.ScriptableObjects.Items;
using Nytherion.UI.Controllers;

namespace Nytherion.UI.Shop
{
    public class SellPopupUI : MonoBehaviour
    {
        [SerializeField] private Image iconImage;
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI priceText;
        [SerializeField] private TextMeshProUGUI amountText;
        [SerializeField] private Slider amountSlider;
        [SerializeField] private Button confirmButton;
        [SerializeField] private Button cancelButton;

        private ShopUI shopUI;
        private ItemData currentItem;
        private int currentAmount = 1;
        private int unitPrice = 0;

        private void Awake()
        {
            if (confirmButton != null) confirmButton.onClick.AddListener(OnConfirmClicked);
            if (cancelButton != null) cancelButton.onClick.AddListener(ClosePopup);
            if (amountSlider != null) amountSlider.onValueChanged.AddListener(OnSliderValueChanged);

            gameObject.SetActive(false); 
        }

        public void Setup(ShopUI shop, ItemData item, int maxAmount, int sellPricePerUnit)
        {
            shopUI = shop;
            currentItem = item;
            unitPrice = sellPricePerUnit;

            iconImage.sprite = item.icon;
            nameText.text = item.itemName;

            amountSlider.minValue = 1;
            amountSlider.maxValue = maxAmount;
            amountSlider.value = 1;

            UpdateUI(1);
            gameObject.SetActive(true);
        }

        private void OnSliderValueChanged(float value)
        {
            UpdateUI(Mathf.RoundToInt(value));
        }

        private void UpdateUI(int amount)
        {
            currentAmount = amount;
            if (amountText != null) amountText.text = $"X {currentAmount.ToString()}";
            if (priceText != null) priceText.text = $"{unitPrice * currentAmount} G";
        }

        private void OnConfirmClicked()
        {
            if (shopUI != null && currentItem != null)
            {
                shopUI.QuickSellItem(currentItem, currentAmount);
            }
            ClosePopup();
        }

        private void ClosePopup()
        {
            gameObject.SetActive(false);
        }
    }
}