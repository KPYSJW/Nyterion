using UnityEngine;
using Nytherion.Core.Enums;
using Nytherion.UI.Controllers;

namespace Nytherion.GamePlay.Characters.NPC
{
    public class GachaNPC : MonoBehaviour, IInteractable
    {
        public InteractableType Type => InteractableType.GachaNPC;
        public bool IsInteractable { get; set; } = true;

        public void Interact()
        {
            if (!IsInteractable) return;
            if (GachaUIController.Instance != null)
            {
                GachaUIController.Instance.Toggle();
            }
        }

        [Header("Gizmo Settings")]
        [SerializeField] private float interactionRange = 2f;
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, interactionRange);
        }
    }
}