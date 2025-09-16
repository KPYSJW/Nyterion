using UnityEngine;
using Nytherion.Data.ScriptableObjects.Shop;
using Nytherion.Core.Enums;
using Nytherion.UI.Controllers;
using VContainer;
using VContainer.Unity;

namespace Nytherion.GamePlay.Characters.NPC
{
    public class ShopDealer : MonoBehaviour, IInteractable
    {
        public InteractableType Type => InteractableType.ShopDealer;
        public bool IsInteractable { get; set; } = true;

        private GameSceneUIRefs gameSceneUIRefs;

        [Header("Shop Data")]
        public ShopData shopData;
        private ShopUI shopUI;
        
        [Inject]
        public void Construct(ShopUI shopUI)
        {
            this.shopUI = shopUI;
        }

        private void Start()
        {
            if (shopUI == null)
            {
                var lifetimeScope = LifetimeScope.Find<GameSceneLifetimeScope>();
                if (lifetimeScope != null)
                {
                    if (lifetimeScope.Container.TryResolve<ShopUI>(out var ui))
                    {
                        shopUI = ui;
                    }
                    else
                    {
                        Debug.LogError("[ShopDealer] Container에서 ShopUI를 해결할 수 없음.");
                    }
                }
                else
                {
                    Debug.LogError("[ShopDealer] GameSceneLifetimeScope를 찾을 수 없음.");
                }
            }
            else
            {
                Debug.Log("[ShopDealer] Start에서 shopUI가 이미 주입되어 있음.");
            }
        }

        public void Interact()
        {
            if (!IsInteractable) return;

            if (shopData == null)
            {
                Debug.LogError("ShopData가 할당되지 않았습니다!", this);
                return;
            }

            if (shopUI != null)
            {
                shopUI.OpenShop(shopData);
            }
            else
            {
                Debug.LogError($"[ShopDealer] shopUI가 null입니다!");
            }
        }
    }
}