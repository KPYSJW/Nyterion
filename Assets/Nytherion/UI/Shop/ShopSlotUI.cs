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
using Nytherion.Data.ScriptableObjects.Weapons;
using Nytherion.Data.ScriptableObjects.Items;
using Nytherion.Core.Enums;

namespace Nytherion.UI.Shop
{
    public class ShopSlotUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        [SerializeField] private Image iconImage;
        [SerializeField] private GameObject pricePanel;
        [SerializeField] private TextMeshProUGUI priceText;
        //[SerializeField] private TextMeshProUGUI descriptionText;
        [SerializeField] private CanvasGroup canvasGroup;

        [Header("Rarity Slot Visuals")]
        [SerializeField] private Sprite defaultSlotSprite;
        [SerializeField] private Sprite commonSlotSprite;
        [SerializeField] private Sprite uncommonSlotSprite;
        [SerializeField] private Sprite rareSlotSprite;
        [SerializeField] private Sprite epicSlotSprite;
        [SerializeField] private Sprite legendarySlotSprite;

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
                    iconImage.raycastTarget = false;
                    Sprite displaySprite = CurrentItem.item.icon;
                    if (CurrentItem.item is WeaponData weaponData && weaponData.weaponSprite != null)
                    {
                        displaySprite = weaponData.weaponSprite;
                    }
                    iconImage.sprite = displaySprite;
                }

                // 장비 등급에 따른 슬롯 배경 이미지 변경
                Rarity targetRarity = Rarity.Common;
                if (CurrentItem.item is EquipmentData equipmentData)
                {
                    targetRarity = equipmentData.rarity;
                }

                Image targetSlotImage = GetComponent<Image>();
                if (targetSlotImage != null)
                {
                    Sprite chosenSlotSprite = targetRarity switch
                    {
                        Rarity.Common => commonSlotSprite,
                        Rarity.Uncommon => uncommonSlotSprite,
                        Rarity.Rare => rareSlotSprite,
                        Rarity.Epic => epicSlotSprite,
                        Rarity.Legendary => legendarySlotSprite,
                        _ => commonSlotSprite
                    };

                    if (chosenSlotSprite != null)
                    {
                        targetSlotImage.sprite = chosenSlotSprite;
                    }
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

                if (pricePanel != null)
                {
                    pricePanel.SetActive(true);
                }

                if (priceText != null)
                {
                    priceText.text = $"{displayPrice}";
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
            if (eventData.button == PointerEventData.InputButton.Left && eventData.clickCount >= 2)
            {
                if (CurrentItem != null && !IsSoldOut())
                {
                    OnBuyButtonClicked();
                }
                else
                {
                    Debug.LogWarning($"[ShopSlotUI] 더블클릭 구매 불가 - Item: {(CurrentItem != null ? CurrentItem.item?.itemName : "Null")}, IsSoldOut: {IsSoldOut()}");
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
                        }
                    }
                }

                // 매진 상태 확인 및 시각적 업데이트
                if (IsSoldOut())
                {
                    ApplySoldOutVisual();
                }
                else
                {
                    ResetVisual();
                }
            }
        }

        public void SetInteractable(bool interactable)
        {
            if (canvasGroup != null)
            {
                canvasGroup.interactable = interactable;
                canvasGroup.blocksRaycasts = interactable;
            }
        }

        private void OnBuyButtonClicked()
        {
            if (shopUI == null)
            {
                shopUI = GetComponentInParent<ShopUI>();
                if (shopUI == null)
                {
                    shopUI = FindObjectOfType<ShopUI>();
                }
            }

            if (shopUI != null)
            {
                shopUI.BuyItem(this);
            }
            else
            {
                Debug.LogError("[ShopSlotUI] shopUI 참조를 찾을 수 없어 구매를 실행하지 못했습니다.");
            }
        }
        private void ApplySoldOutVisual()
        {
            if (iconImage != null)
            {
                iconImage.enabled = false;
                iconImage.sprite = null;
            }

            if (pricePanel != null)
            {
                pricePanel.SetActive(false);
            }

            if (priceText != null)
            {
                priceText.text = "";
            }

            Image targetSlotImage = GetComponent<Image>();
            if (targetSlotImage != null && defaultSlotSprite != null)
            {
                targetSlotImage.sprite = defaultSlotSprite;
            }

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = true;
            }
        }
        private bool IsSoldOut()
        {
            return CurrentItem == null || (!CurrentItem.isUnlimited && CurrentItem.stock <= 0);
        }
        private void ResetVisual()
        {
            if (iconImage != null)
            {
                iconImage.enabled = true;
            }

            if (pricePanel != null)
            {
                pricePanel.SetActive(true);
            }

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
                canvasGroup.interactable = true;
                canvasGroup.blocksRaycasts = true;
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (CurrentItem != null && CurrentItem.item != null && !IsSoldOut())
            {
                if (TooltipPanel.Instance != null)
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

