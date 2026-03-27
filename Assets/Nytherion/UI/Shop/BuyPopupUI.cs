using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Nytherion.Data.ScriptableObjects.Shop;
using Nytherion.UI.Controllers;

namespace Nytherion.UI.Shop
{
    public class BuyPopupUI : MonoBehaviour
    {
        [SerializeField] private Image iconImage;
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI priceText;
        [SerializeField] private TextMeshProUGUI amountText;
        [SerializeField] private Slider amountSlider; 
        [SerializeField] private Button confirmButton;
        [SerializeField] private Button cancelButton;

        private ShopUI shopUI;
        private ShopSlotUI currentSlot;
        private int currentAmount = 1;
        private int unitPrice = 0;

        private void Awake()
        {
            if (confirmButton != null) confirmButton.onClick.AddListener(OnConfirmClicked);
            if (cancelButton != null) cancelButton.onClick.AddListener(ClosePopup);
            if (amountSlider != null) amountSlider.onValueChanged.AddListener(OnSliderValueChanged);
            gameObject.SetActive(false);
        }

        public void Setup(ShopUI shop, ShopSlotUI slot, int maxAffordable)
        {
            shopUI = shop;
            currentSlot = slot;
            var shopItem = slot.CurrentItem;
            unitPrice = shopItem.price;

            int availableStock = shopItem.isUnlimited ? 99 : shopItem.stock;
            int maxAmount = Mathf.Clamp(availableStock, 1, maxAffordable);

            iconImage.sprite = shopItem.item.icon;
            nameText.text = shopItem.item.itemName;

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
            if (amountText != null) amountText.text = currentAmount.ToString();
            if (priceText != null) priceText.text = $"{unitPrice * currentAmount} G";
        }

        private void OnConfirmClicked()
        {
            if (shopUI != null && currentSlot != null)
            {
                shopUI.ConfirmPurchase(currentSlot, currentAmount);
            }
            ClosePopup();
        }

        private void ClosePopup()
        {
            gameObject.SetActive(false);
        }
    }
}