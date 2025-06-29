using UnityEngine;
using Nytherion.UI.EngravingBoard;

namespace Nytherion.GamePlay.Characters.NPC
{
    public class EngravingAltar : MonoBehaviour, IInteractable
    {
        public void Interact()
        {
            Debug.Log("EngravingAltar: 상호작용 신호 받음. UI 토글 시도.");
            if (EngravingUIController.Instance != null)
            {
                EngravingUIController.Instance.Toggle();
            }
        }
    }
}