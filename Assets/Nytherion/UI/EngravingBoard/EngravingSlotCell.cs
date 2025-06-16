using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace Nytherion.UI.EngravingBoard
{
    public class EngravingSlotCell : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Image engravingIcon;

        public Vector2Int GridPosition { get; private set; }

        public void Initialize(Vector2Int position)
        {
            GridPosition = position;
            ClearCell();
        }
        public void OnPointerEnter(PointerEventData eventData)
        {
            if (EngravingGridUI.Instance != null)
            {
                EngravingGridUI.Instance.OnCellPointerEnter(this);
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (EngravingGridUI.Instance != null)
            {
                EngravingGridUI.Instance.OnCellPointerExit(this);
            }
        }
        public void SetEngraving(Sprite icon)
        {
            engravingIcon.enabled = true;
            engravingIcon.sprite = icon;
        }

        public void ClearCell()
        {
            engravingIcon.enabled = false;
            engravingIcon.sprite = null;
        }

        public void Highlight(bool active)
        {
            backgroundImage.color = active ? Color.yellow : Color.white;
        }
    }
}
