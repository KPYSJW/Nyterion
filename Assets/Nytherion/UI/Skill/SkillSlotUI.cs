using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System;
using Nytherion.Data.ScriptableObjects.Skill;
using Nytherion.UI.Components;

namespace Nytherion.UI.Skill
{
    public class SkillSlotUI : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler, IPointerEnterHandler, IPointerExitHandler
    {
        public SkillSlotType slotType;
        public int slotIndex;

        [SerializeField] private Image skillIcon; 

        private SkillData currentSkill;

        public event Action<SkillSlotUI> OnDoubleClick;
        public event Action<SkillSlotUI, SkillSlotUI> OnDropSkill;

        private Transform iconOriginalParent;

        public void Setup(SkillData skill)
        {
            currentSkill = skill;

            if (skillIcon == null) return;

            if (skill != null)
            {
                skillIcon.sprite = skill.icon;
                skillIcon.enabled = true;
            }
            else
            {
                skillIcon.sprite = null;
                skillIcon.enabled = false;
            }
        }
        public void OnPointerEnter(PointerEventData eventData)
        {
            if (currentSkill != null && TooltipPanel.Instance != null)
            {
                TooltipPanel.Instance.ShowTooltip(currentSkill);
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (TooltipPanel.Instance != null)
            {
                TooltipPanel.Instance.HideTooltip();
            }
        }
        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.clickCount == 2 && currentSkill != null)
            {
                OnDoubleClick?.Invoke(this);
            }
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (currentSkill == null) return;

            if (TooltipPanel.Instance != null)
            {
                TooltipPanel.Instance.HideTooltip();
            }

            iconOriginalParent = skillIcon.transform.parent;

            Canvas canvas = GetComponentInParent<Canvas>();
            skillIcon.transform.SetParent(canvas.transform);
            skillIcon.transform.SetAsLastSibling(); 

            skillIcon.raycastTarget = false;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (currentSkill != null)
            {
                skillIcon.transform.position = eventData.position;
            }
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            skillIcon.transform.SetParent(iconOriginalParent);

            skillIcon.transform.localPosition = Vector3.zero;

            skillIcon.raycastTarget = true;
        }

        public void OnDrop(PointerEventData eventData)
        {
            var draggedSlot = eventData.pointerDrag.GetComponent<SkillSlotUI>();

            if (draggedSlot != null && draggedSlot != this)
            {
                OnDropSkill?.Invoke(draggedSlot, this);
            }
        }

        public SkillData GetSkill() => currentSkill;
    }
}