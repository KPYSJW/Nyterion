using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Nytherion.Data.ScriptableObjects.Items;
using Nytherion.Core;
using UnityEngine.EventSystems;
using Nytherion.UI.Inventory;

namespace Nytherion.UI.Shop
{
    public class SellSlotUI : MonoBehaviour, IDropHandler
    {
        public static SellSlotUI Instance { get; private set; }
        [Header("References")]
        [SerializeField] private Image itemIcon;
        [SerializeField] private TMP_InputField amountInput;
        [SerializeField] private Button sellButton;
        [SerializeField] private Button increaseButton;
        [SerializeField] private Button decreaseButton;
        [SerializeField] private GameObject contentPanel;
        [SerializeField] private TMP_Text priceText;
        private ItemData currentItem;
        private int currentAmount = 1;
        private int sellPrice;
        private int maxAmount = 1;
        public event System.Action<ItemData, int> OnItemSold;
        public const float SELL_PRICE_RATIO = 0.7f;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);

            sellButton.onClick.AddListener(OnSellButtonClicked);
            increaseButton.onClick.AddListener(() => ChangeAmount(1));
            decreaseButton.onClick.AddListener(() => ChangeAmount(-1));

            amountInput.onEndEdit.AddListener(OnAmountInputChanged);

            ClearSlot();
        }

        public void SetItem(ItemData item, int amount = 1)
        {
            if (item == null)
            {
                ClearSlot();
                return;
            }

            currentItem = item;
            currentAmount = Mathf.Clamp(amount, 1, InventoryManager.Instance.GetItemCount(item));
            maxAmount = InventoryManager.Instance.GetItemCount(item);
            sellPrice = CalculateSellPrice(item);

            itemIcon.sprite = item.icon;
            itemIcon.enabled = true;
            amountInput.text = currentAmount.ToString();

            sellButton.interactable = true;

            UpdatePriceDisplay();
        }

        public void UpdateAmount(int newAmount)
        {
            if (currentItem == null) return;

            maxAmount = InventoryManager.Instance.GetItemCount(currentItem);

            currentAmount = Mathf.Clamp(newAmount, 1, maxAmount);

            amountInput.text = currentAmount.ToString();

            sellButton.interactable = currentAmount > 0;
        }

        private void ChangeAmount(int change)
        {
            if (currentItem == null) return;

            int newAmount = Mathf.Clamp(currentAmount + change, 1, maxAmount);
            currentAmount = newAmount;
            amountInput.text = currentAmount.ToString();
            UpdatePriceDisplay();

            decreaseButton.interactable = currentAmount > 1;
            increaseButton.interactable = currentAmount < maxAmount;
        }

        public void OnAmountInputChanged(string input)
        {
            if (!int.TryParse(input, out int parsed))
            {
                parsed = 1;
            }

            int maxAmount = InventoryManager.Instance.GetItemCount(currentItem);
            parsed = Mathf.Clamp(parsed, 1, maxAmount);

            currentAmount = parsed;
            amountInput.text = currentAmount.ToString();
            sellButton.interactable = currentAmount > 0;
            UpdatePriceDisplay();
        }
        private void UpdatePriceDisplay()
        {
            if (currentItem == null)
            {
                priceText.text = "0";
                return;
            }

            int totalPrice = CalculateSellPrice(currentItem) * currentAmount;
            priceText.text = $"{totalPrice}";
        }
        private int CalculateSellPrice(ItemData item)
        {
            return Mathf.RoundToInt(item.baseValue * SELL_PRICE_RATIO);
        }

        private void OnSellButtonClicked()
        {
            if (currentItem == null || currentAmount <= 0) return;

            OnItemSold?.Invoke(currentItem, currentAmount);
            ClearSlot();
        }

        public void ClearSlot()
        {
            currentItem = null;
            currentAmount = 0;
            sellPrice = 0;

            itemIcon.sprite = null;
            itemIcon.enabled = false;
            amountInput.text = "0";
            priceText.text = "0";
            contentPanel.SetActive(true);

            sellButton.interactable = false;
        }

        public bool CanAcceptItem(ItemData item)
        {
            return true;
        }
        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }
        public void OnDrop(PointerEventData eventData)
        {
            InventorySlotUI droppedSlot = eventData.pointerDrag?.GetComponent<InventorySlotUI>();

            if (droppedSlot != null && !droppedSlot.IsEmpty)
            {
                SetItem(droppedSlot.CurrentItem, droppedSlot.CurrentAmount);
            }
        }
    }
}