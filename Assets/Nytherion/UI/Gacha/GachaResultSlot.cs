using UnityEngine;
using UnityEngine.EventSystems;
using Nytherion.GamePlay.Engravings;
using Nytherion.UI.EngravingBoard;
using Nytherion.Data.ScriptableObjects.Items;

namespace Nytherion.UI.Gacha
{
    public class GachaResultSlot : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private EngravingBlock associatedBlock;
        private ItemData associatedItem;

        public void Setup(EngravingBlock block)
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
            if (associatedBlock != null && EngravingTooltip.Instance != null)
            {
                EngravingTooltip.Instance.Show(associatedBlock);
            }
            else if(associatedItem != null)
            {

            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (EngravingTooltip.Instance != null)
            {
                EngravingTooltip.Instance.Hide();
            }
            else if (associatedItem != null)
            { 
            }
        }
    }
}