using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Nytherion.Data.ScriptableObjects.Shop;
using Nytherion.UI.Controllers;
using Nytherion.Core.Managers;
using VContainer;
using UnityEngine.EventSystems;
using Nytherion.UI.Components;
using System.Collections.Generic;
using Nytherion.GamePlay.Relics;

namespace Nytherion.UI.Shop
{
    public class ShopSlotUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        [SerializeField] private Image iconImage;
        [SerializeField] private TextMeshProUGUI priceText;
        //[SerializeField] private TextMeshProUGUI descriptionText;
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
                if (iconImage != null)
                {
                    iconImage.sprite = CurrentItem.item.icon;
                }

                int displayPrice = CurrentItem.price;

                // 쿠폰 조각 (CouponPiece) 유물 효과 적용: 상점 상품 가격 15% 할인
                RelicManager relicManager = UnityEngine.Object.FindObjectOfType<RelicManager>();
                if (relicManager != null)
                {
                    foreach (KeyValuePair<string, Vector2Int> pair in relicManager.GetPlacedBlocks())
                    {
                        RelicBlock block = relicManager.GetBlockAt(pair.Value.y, pair.Value.x);
                        if (block != null && block.RelicId == "CouponPiece" && !block.SourceData.isDisabled)
                        {
                            displayPrice = Mathf.RoundToInt(displayPrice * 0.85f);
                            break;
                        }
                    }
                }

                if (priceText != null)
                {
                    priceText.text = $"{displayPrice}";
                }

                if (buyButton != null)
                {
                    buyButton.onClick.RemoveAllListeners();
                    buyButton.onClick.AddListener(OnBuyButtonClicked);
                }
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

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left && eventData.clickCount == 2)
            {
                if (CurrentItem != null && !IsSoldOut())
                {
                    OnBuyButtonClicked();
                }
            }
        }

        public void UpdateStockUI()
        {
            if (CurrentItem != null)
            {
                // ShopManager에서 최신 재고 정보 가져오기
                ShopManager shopManager = FindObjectOfType<ShopManager>();
                if (shopManager != null && !string.IsNullOrEmpty(currentShopName))
                {
                    List<ShopItemData> shopItems = shopManager.GetShopItems(currentShopName);
                    if (shopItems != null)
                    {
                        ShopItemData updatedItem = shopItems.Find(item => item.shopItemId == CurrentItem.shopItemId);
                        if (updatedItem != null)
                        {
                            // 실제 ShopManager의 재고로 업데이트
                            CurrentItem.stock = updatedItem.stock;
                            Debug.Log($"[ShopSlotUI] '{CurrentItem.item.itemName}' 재고 업데이트: {CurrentItem.stock}");
                        }
                    }
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

        public void OnPointerEnter(PointerEventData eventData)
        {
            if(CurrentItem != null && CurrentItem.item != null)
            {
                if(TooltipPanel.Instance != null)
                {
                    TooltipPanel.Instance.ShowTooltip(CurrentItem.item);
                }
            }
        }
        public void OnPointerExit(PointerEventData eventData)
        {
            if(TooltipPanel.Instance != null)
            {
                TooltipPanel.Instance.HideTooltip();
            }
        }
        private void OnDisable()
        {
            if(TooltipPanel.Instance != null && TooltipPanel.Instance.gameObject.activeSelf)
            {
                TooltipPanel.Instance.HideTooltip();
            }
        }
    }
}

