using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System;

namespace Nytherion.UI.RelicBoard
{
    public class RelicSlotCell : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Image relicIcon;

        public Vector2Int GridPosition { get; private set; }
        
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
        }

        public void Highlight(bool active)
        {
            backgroundImage.color = active ? Color.yellow : Color.white;
        }
    }
}
