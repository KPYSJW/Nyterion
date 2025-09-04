using UnityEngine;
using Nytherion.Data.ScriptableObjects.Shop;
using Nytherion.Core.Enums;
using Nytherion.UI.Controllers;
using Zenject;

namespace Nytherion.GamePlay.Characters.NPC
{
    public class ShopDealer : MonoBehaviour, IInteractable
    {
        public InteractableType Type => InteractableType.ShopDealer;

        [Header("Shop Data")]
        public ShopData shopData;
        private ShopUI shopUI;
        
        [Inject]
        public void Construct(ShopUI shopUI)
        {
            this.shopUI = shopUI;
        }

        public void Interact()
        {
            if (shopData == null)
            {
                Debug.LogError("ShopData가 할당되지 않았습니다!", this);
                return;
            }

            if (shopUI != null)
            {
                shopUI.OpenShop(shopData);
            }
        }
    }
}