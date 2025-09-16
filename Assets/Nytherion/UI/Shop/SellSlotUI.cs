using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Nytherion.Data.ScriptableObjects.Items;
using Nytherion.Core.Managers;
using UnityEngine.EventSystems;
using Nytherion.UI.Inventory;
using VContainer;
using VContainer.Unity;

namespace Nytherion.UI.Shop
{
    public class SellSlotUI : MonoBehaviour, IDropHandler
    {
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
        private InventoryManager inventoryManager;

        [Inject]
        public void Construct(InventoryManager inventoryManager)
        {
            this.inventoryManager = inventoryManager;
        }

        private void Awake()
        {
            sellButton.onClick.AddListener(OnSellButtonClicked);
            increaseButton.onClick.AddListener(() => ChangeAmount(1));
            decreaseButton.onClick.AddListener(() => ChangeAmount(-1));

            amountInput.onEndEdit.AddListener(OnAmountInputChanged);

            ClearSlot();
        }

        private void Start()
        {
            if (inventoryManager == null)
            {
                var gameSceneScope = LifetimeScope.Find<GameSceneLifetimeScope>();
                if (gameSceneScope != null)
                {
                    if (gameSceneScope.Container.TryResolve<InventoryManager>(out var invManager))
                    {
                        inventoryManager = invManager;
                    }
                }

                if (inventoryManager == null)
                {
                    inventoryManager = FindObjectOfType<InventoryManager>();
                    if (inventoryManager == null)
                    {
                        Debug.LogError("[SellSlotUI] InventoryManager not found. SellSlot operations will be disabled.");
                    }
                }
            }
        }

        public void SetItem(ItemData item, int amount = 1)
        {
            if (item == null)
            {
                ClearSlot();
                return;
            }

            if (inventoryManager == null)
            {
                Debug.LogError("[SellSlotUI] InventoryManager is null. Cannot set item.");
                return;
            }

            currentItem = item;
            currentAmount = Mathf.Clamp(amount, 1, inventoryManager.GetItemCount(item));
            maxAmount = inventoryManager.GetItemCount(item);
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

            maxAmount = inventoryManager.GetItemCount(currentItem);

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

            int maxAmount = inventoryManager.GetItemCount(currentItem);
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
            if (currentItem == null || currentAmount <= 0)
            {
                Debug.LogWarning("[SellSlotUI] OnSellButtonClicked: currentItem is null or amount <= 0");
                return;
            }

            Debug.Log($"[SellSlotUI] OnSellButtonClicked: Selling {currentItem.itemName} x{currentAmount}, ID: {currentItem.ID}");

            if (OnItemSold != null)
            {
                Debug.Log($"[SellSlotUI] Instance ID: {GetInstanceID()}, OnItemSold event has {OnItemSold.GetInvocationList().Length} subscribers");
                OnItemSold.Invoke(currentItem, currentAmount);
            }
            else
            {
                Debug.LogError($"[SellSlotUI] Instance ID: {GetInstanceID()}, OnItemSold event has no subscribers!");
            }

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
        
        public void OnDrop(PointerEventData eventData)
        {
            InventorySlotUI droppedSlot = eventData.pointerDrag?.GetComponent<InventorySlotUI>();

            if (droppedSlot != null && !droppedSlot.IsEmpty)
            {
                if (inventoryManager == null)
                {
                    Debug.LogError("[SellSlotUI] InventoryManager is null in OnDrop. Cannot process drop operation.");
                    return;
                }

                Debug.Log($"[SellSlotUI] OnDrop: Dragged item {droppedSlot.CurrentItem?.itemName} x{droppedSlot.CurrentCount}, ID: {droppedSlot.CurrentItem?.ID}");
                SetItem(droppedSlot.CurrentItem, droppedSlot.CurrentCount);
            }
        }
    }
}