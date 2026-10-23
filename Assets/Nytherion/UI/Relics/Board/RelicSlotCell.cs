using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System;
using Nytherion.Core.Enums;

namespace Nytherion.UI.RelicBoard
{
    public class RelicSlotCell : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Image relicIcon;

        public Vector2Int GridPosition { get; private set; }
        public Image BackgroundImage => backgroundImage;
        private RelicSlotVisual slotVisual;

        private RelicSlotVisual SlotVisual
        {
            get
            {
                if (slotVisual == null && backgroundImage != null)
                    slotVisual = backgroundImage.GetComponent<RelicSlotVisual>();
                return slotVisual;
            }
        }
        
        public event Action<RelicSlotCell> OnCellPointerEnter;
        public event Action<RelicSlotCell> OnCellPointerExit;

        public void Initialize(Vector2Int position)
        {
            GridPosition = position;
            ClearCell();
        }
        public void OnPointerEnter(PointerEventData eventData)
        {
            OnCellPointerEnter?.Invoke(this);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            OnCellPointerExit?.Invoke(this);
        }
        public void SetRelic(Sprite icon)
        {
            relicIcon.enabled = true;
            relicIcon.sprite = icon;
        }

        public void ClearCell()
        {
            relicIcon.enabled = false;
            relicIcon.sprite = null;
            SlotVisual?.SetRarity(null);
        }

        public void SetRarity(Rarity rarity) => SlotVisual?.SetRarity(rarity);

        public void Highlight(bool active)
        {
            if (SlotVisual != null) SlotVisual.Highlight(active);
            else backgroundImage.color = active ? Color.yellow : Color.white;
        }
    }
}
