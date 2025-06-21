using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Nytherion.UI.EngravingBoard;

namespace Nytherion.GamePlay.Characters.NPC
{
    public class EngravingAltar : MonoBehaviour, IInteractable
    {
        public bool IsInteractable { get; set; } = true;

        public void Interact()
        {
            if (!IsInteractable) return;

            EngravingUIController.Instance.ToggleEngravingUI();
        }

        [Header("Gizmo Settings")]
        [SerializeField] private float interactionRange = 2f;
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, interactionRange);
        }
    }
}
