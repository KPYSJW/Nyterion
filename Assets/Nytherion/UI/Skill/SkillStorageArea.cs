using UnityEngine;
using UnityEngine.EventSystems;
using System;

namespace Nytherion.UI.Skill
{
    public class SkillStorageArea : MonoBehaviour, IDropHandler
    {
        public event Action<SkillSlotUI> OnDropToStorage;

        public void OnDrop(PointerEventData eventData)
        {
            if (eventData.pointerDrag != null)
            {
                var draggedSlot = eventData.pointerDrag.GetComponent<SkillSlotUI>();

                if (draggedSlot != null && draggedSlot.slotType == SkillSlotType.Equipped)
                {
                    OnDropToStorage?.Invoke(draggedSlot);
                }
            }
        }
    }
}