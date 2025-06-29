using UnityEngine;
using Nytherion.UI.Shop;
using Nytherion.Data.Shop;

namespace Nytherion.GamePlay.Characters.NPC
{
    public class ShopDealer : MonoBehaviour, IInteractable
    {
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