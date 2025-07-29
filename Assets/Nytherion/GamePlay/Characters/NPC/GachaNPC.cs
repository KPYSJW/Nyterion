using UnityEngine;
using Nytherion.Core.Enums;
using Nytherion.UI.Controllers;
using Zenject;

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
        public void Interact()
        {
            if (!IsInteractable) return;
            gachaUIController.Toggle();
        }
    }
}