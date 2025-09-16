using UnityEngine;
using Nytherion.Core.Enums;
using Nytherion.UI.Controllers;
using VContainer;
using VContainer.Unity;

namespace Nytherion.GamePlay.Characters.NPC
{
    public class GachaNPC : MonoBehaviour, IInteractable
    {
        public InteractableType Type => InteractableType.GachaNPC;
        public bool IsInteractable { get; set; } = true;

        private GachaUIController gachaUIController;

        [Inject]
        public void Construct(GachaUIController gachaUIController)
        {
            this.gachaUIController = gachaUIController;
        }

        private void Start()
        {
            if (gachaUIController == null)
            {
                var lifetimeScope = LifetimeScope.Find<GameSceneLifetimeScope>();
                if (lifetimeScope != null)
                {
                    if (lifetimeScope.Container.TryResolve<GachaUIController>(out var controller))
                    {
                        gachaUIController = controller;
                    }
                    else
                    {
                        Debug.LogError("[GachaNPC] Container에서 GachaUIController를 해결할 수 없음.");
                    }
                }
                else
                {
                    Debug.LogError("[GachaNPC] GameSceneLifetimeScope를 찾을 수 없음.");
                }
            }
        }

        public void Interact()
        {
            if (!IsInteractable) return;

            if (gachaUIController != null)
            {
                gachaUIController.Toggle();
            }
            else
            {
                Debug.LogError("[GachaNPC] gachaUIController가 null입니다!");
            }
        }
    }
}