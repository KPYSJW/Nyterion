using UnityEngine;
using Nytherion.UI.Gacha;


namespace Nytherion.GamePlay.Characters.NPC
{
    public class GachaNPC : MonoBehaviour, IInteractable
    {

        [Header("UI")]
        [SerializeField] private GachaUIController gachaUIController;

        public void Interact()
        {
           if(gachaUIController != null) gachaUIController.ToggleUI();
        }
    }
}
