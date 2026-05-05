using UnityEngine;
using UnityEngine.EventSystems;
using Nytherion.GamePlay.Relics;
using Nytherion.UI.RelicBoard;
using Nytherion.Data.ScriptableObjects.Items;

namespace Nytherion.UI.Gacha
{
    public class GachaResultSlot : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private RelicBlock associatedBlock;
        private ItemData associatedItem;

        public void Setup(RelicBlock block)
        {
            this.associatedBlock = block;
            this.associatedItem = null;
        }
        public void Setup(ItemData item)
        {
            this.associatedItem = item;
            this.associatedBlock = null;
        }
        public void OnPointerEnter(PointerEventData eventData)
        {
            if (associatedBlock != null && RelicTooltip.Instance != null)
            {
                RelicTooltip.Instance.Show(associatedBlock);
            }
            else if(associatedItem != null)
            {

            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (RelicTooltip.Instance != null)
            {
                RelicTooltip.Instance.Hide();
            }
            else if (associatedItem != null)
            { 
            }
        }
    }
}