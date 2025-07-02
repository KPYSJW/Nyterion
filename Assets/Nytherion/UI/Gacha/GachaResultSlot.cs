using UnityEngine;
using UnityEngine.EventSystems;
using Nytherion.GamePlay.Engravings;

public class GachaResultSlot : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private EngravingBlock associatedBlock;

    public void Setup(EngravingBlock block)
    {
        this.associatedBlock = block;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (associatedBlock != null && EngravingTooltip.Instance != null)
        {
            EngravingTooltip.Instance.Show(associatedBlock);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (EngravingTooltip.Instance != null)
        {
            EngravingTooltip.Instance.Hide();
        }
    }
}