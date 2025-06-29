using UnityEngine;
using Nytherion.UI.Gacha;

namespace Nytherion.GamePlay.Characters.NPC
{
    public class GachaNPC : MonoBehaviour, IInteractable
    {
        public bool IsInteractable { get; set; } = true;

        public void Interact()
        {
            if (!IsInteractable || GachaUIController.Instance == null) return;
            
            GachaUIController.Instance.Toggle();
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