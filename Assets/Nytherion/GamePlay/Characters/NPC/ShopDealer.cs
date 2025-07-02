using UnityEngine;
using Nytherion.Data.ScriptableObjects.Shop;
using Nytherion.Core.Enums;
using Nytherion.UI.Controllers;

namespace Nytherion.GamePlay.Characters.NPC
{
    public class ShopDealer : MonoBehaviour, IInteractable
    {
        public InteractableType Type => InteractableType.ShopDealer;

        [Header("Shop Data")]
        [Tooltip("이 상점에서 판매할 상품 데이터")]
        public ShopData shopData;

        public void Interact()
        {
            if (shopData == null)
            {
                Debug.LogError("ShopData가 할당되지 않았습니다!", this);
                return;
            }

           if (ShopUI.Instance != null)
            {
                ShopUI.Instance.OpenShop(shopData);
            }
        }
    }
}