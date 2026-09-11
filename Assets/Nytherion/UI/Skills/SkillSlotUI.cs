using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System;
using Nytherion.Data.ScriptableObjects.Skill;
using Nytherion.UI.Components;
using Nytherion.Core.Managers;

namespace Nytherion.UI.Skill
{
    /// <summary>
    /// 인벤토리나 스킬창에서 개별 스킬 슬롯의 UI와 사용자 상호작용을 처리하는 클래스
    /// </summary>
    public class SkillSlotUI : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler, IPointerEnterHandler, IPointerExitHandler
    {
        [Tooltip("슬롯이 장착 슬롯인지, 보관함 슬롯인지 구분")]
        public SkillSlotType slotType;
        [Tooltip("슬롯의 고유 인덱스 번호")]
        public int slotIndex;

        [SerializeField] private Image skillIcon; 

        private SkillData currentSkill;
        private SkillDataManager skillDataManager;

        // 슬롯 상호작용 이벤트
        public event Action<SkillSlotUI> OnDoubleClick;
        public event Action<SkillSlotUI, SkillSlotUI> OnDropSkill;

        // 드래그 시 아이콘의 원래 부모를 기억하기 위한 변수
        private Transform iconOriginalParent;

        /// <summary>
        /// 슬롯에 표시될 스킬 데이터와 매니저를 초기화
        /// </summary>
        public void Setup(SkillData skill, SkillDataManager manager = null)
        {
            currentSkill = skill;
            skillDataManager = manager;

            if (skillIcon == null) return;

            // 스킬 데이터가 있으면 아이콘 표시
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

        // --- 마우스 호버 이벤트 (툴팁 표시/숨김) ---
        public void OnPointerEnter(PointerEventData eventData)
        {
            if (currentSkill != null && TooltipPanel.Instance != null)
            {
                int level = 1;
                int exp = 0;
                int reqExp = 1;

                if (skillDataManager != null && skillDataManager.skillStates.TryGetValue(currentSkill.skillID, out var state))
                {
                    level = state.level;
                    exp = state.exp;
                    reqExp = state.GetRequiredExp(level);
                }

                TooltipPanel.Instance.ShowTooltip(currentSkill, level, exp, reqExp);
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (TooltipPanel.Instance != null)
            {
                TooltipPanel.Instance.HideTooltip();
            }
        }

        // --- 클릭 이벤트 ---
        public void OnPointerClick(PointerEventData eventData)
        {
            // 더블 클릭 감지
            if (eventData.clickCount == 2 && currentSkill != null)
            {
                OnDoubleClick?.Invoke(this);
            }
        }

        // --- 드래그 앤 드롭 이벤트 ---

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            if (currentSkill == null) return;

            // 드래그 시작 시 방해되지 않도록 툴팁을 숨긴다
            if (TooltipPanel.Instance != null)
            {
                TooltipPanel.Instance.HideTooltip();
            }

            // 아이콘이 다른 UI에 가려지지 않도록 최상단으로 이동
            iconOriginalParent = skillIcon.transform.parent;

            Canvas canvas = GetComponentInParent<Canvas>();
            skillIcon.transform.SetParent(canvas.transform);
            skillIcon.transform.SetAsLastSibling(); 

            // 드래그 중인 아이콘이 마우스 포인터의 Raycast를 막지 않도록 설정 (드롭 판정이 원활하게 이루어지도록)
            skillIcon.raycastTarget = false;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            // 드래그 중 아이콘 위치를 마우스 위치로 이동
            if (currentSkill != null)
            {
                skillIcon.transform.position = eventData.position;
            }
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            // 드래그 종료 시 아이콘을 원래 부모로 복귀시키고 위치 초기화
            skillIcon.transform.SetParent(iconOriginalParent);
            skillIcon.transform.localPosition = Vector3.zero;

            // 다시 마우스 클릭을 받을 수 있도록 설정
            skillIcon.raycastTarget = true;
        }

        public void OnDrop(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            // 드롭된 객체가 SkillSlotUI 컴포넌트를 가지고 있는지 확인
            SkillSlotUI draggedSlot = eventData.pointerDrag != null ? eventData.pointerDrag.GetComponent<SkillSlotUI>() : null;

            // 자기 자신에게 드롭한 것이 아닌 경우 스왑 이벤트 발생
            if (draggedSlot != null && draggedSlot != this)
            {
                OnDropSkill?.Invoke(draggedSlot, this);
            }
        }

        public SkillData GetSkill() => currentSkill;
    }
}