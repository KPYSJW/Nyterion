using UnityEngine;
using Nytherion.Core.Enums;

namespace Nytherion.GamePlay.Characters.NPC
{
    public class EngravingAltar : MonoBehaviour, IInteractable
    {
        public InteractableType Type => InteractableType.EngravingAltar;
        public void Interact()
        {
        }
    }
}