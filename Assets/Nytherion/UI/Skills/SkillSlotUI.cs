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
        private Canvas dragCanvas;
        private bool isDragging;
        private int iconOriginalSiblingIndex;
        private Vector2 iconOriginalAnchorMin;
        private Vector2 iconOriginalAnchorMax;
        private Vector2 iconOriginalSizeDelta;
        private Vector3 iconOriginalAnchoredPosition;
        private Vector3 iconOriginalScale;
        private Quaternion iconOriginalRotation;
        private bool iconOriginalRaycastTarget;

        private void OnDisable()
        {
            CancelDrag();
        }

        /// <summary>
        /// 슬롯에 표시될 스킬 데이터와 매니저를 초기화
        /// </summary>
        public void Setup(SkillData skill, SkillDataManager manager = null)
        {
            currentSkill = skill;
            skillDataManager = manager;
            PixelPerfectSlotFrame.Apply(GetComponent<Image>());

            if (skillIcon == null) return;

            skillIcon.type = Image.Type.Simple;
            skillIcon.preserveAspect = true;
            skillIcon.raycastTarget = false;
            if (!isDragging)
            {
                RectTransform iconRect = skillIcon.rectTransform;
                iconRect.anchorMin = iconRect.anchorMax = new Vector2(0.5f, 0.5f);
                iconRect.sizeDelta = Vector2.one * (slotType == SkillSlotType.Storage ? 96f : 64f);
                iconRect.anchoredPosition = Vector2.zero;
                iconRect.localScale = Vector3.one;
            }

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
            if (currentSkill == null || skillIcon == null || isDragging) return;

            // 스킬 캔버스의 표시 순서를 유지해 드래그 아이콘이 패널 뒤로 가려지지 않게 한다.
            dragCanvas = GetComponentInParent<Canvas>();
            if (dragCanvas == null) return;

            // 드래그 시작 시 방해되지 않도록 툴팁을 숨긴다
            if (TooltipPanel.Instance != null)
            {
                TooltipPanel.Instance.HideTooltip();
            }

            // 아이콘이 다른 UI에 가려지지 않도록 최상단으로 이동
            RectTransform iconRect = skillIcon.rectTransform;
            iconOriginalParent = iconRect.parent;
            iconOriginalSiblingIndex = iconRect.GetSiblingIndex();
            iconOriginalAnchorMin = iconRect.anchorMin;
            iconOriginalAnchorMax = iconRect.anchorMax;
            iconOriginalSizeDelta = iconRect.sizeDelta;
            iconOriginalAnchoredPosition = iconRect.anchoredPosition3D;
            iconOriginalScale = iconRect.localScale;
            iconOriginalRotation = iconRect.localRotation;
            iconOriginalRaycastTarget = skillIcon.raycastTarget;
            isDragging = true;

            // 슬롯에 맞춰 늘어나는 앵커를 고정해 Canvas 크기로 아이콘이 확대되지 않도록 한다.
            iconRect.SetParent(dragCanvas.transform, false);
            iconRect.anchorMin = iconRect.anchorMax = new Vector2(0.5f, 0.5f);
            iconRect.sizeDelta = Vector2.one * 96f;
            iconRect.localScale = Vector3.one;
            iconRect.localRotation = Quaternion.identity;
            iconRect.SetAsLastSibling();

            // 드래그 중인 아이콘이 마우스 포인터의 Raycast를 막지 않도록 설정 (드롭 판정이 원활하게 이루어지도록)
            skillIcon.raycastTarget = false;
            UpdateDragIconPosition(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            if (!isDragging) return;
            UpdateDragIconPosition(eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            CancelDrag();
        }

        private void UpdateDragIconPosition(PointerEventData eventData)
        {
            Canvas rootCanvas = dragCanvas.rootCanvas;
            Camera eventCamera = rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null
                : rootCanvas.worldCamera != null ? rootCanvas.worldCamera : eventData.pressEventCamera;

            // 화면 좌표를 Canvas 평면의 월드 좌표로 변환해 카메라 모드와 UI 배율을 반영한다.
            if (RectTransformUtility.ScreenPointToWorldPointInRectangle(
                dragCanvas.transform as RectTransform, eventData.position, eventCamera, out Vector3 worldPosition))
            {
                skillIcon.rectTransform.position = worldPosition;
            }
        }

        internal void CancelDrag()
        {
            if (!isDragging) return;
            isDragging = false;

            if (skillIcon != null)
            {
                RectTransform iconRect = skillIcon.rectTransform;
                iconRect.SetParent(iconOriginalParent, false);
                iconRect.SetSiblingIndex(iconOriginalSiblingIndex);
                iconRect.anchorMin = iconOriginalAnchorMin;
                iconRect.anchorMax = iconOriginalAnchorMax;
                iconRect.sizeDelta = iconOriginalSizeDelta;
                iconRect.anchoredPosition3D = iconOriginalAnchoredPosition;
                iconRect.localScale = iconOriginalScale;
                iconRect.localRotation = iconOriginalRotation;
                skillIcon.raycastTarget = iconOriginalRaycastTarget;
            }

            iconOriginalParent = null;
            dragCanvas = null;
        }

        public void OnDrop(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            // 드롭된 객체가 SkillSlotUI 컴포넌트를 가지고 있는지 확인
            SkillSlotUI draggedSlot = eventData.pointerDrag != null ? eventData.pointerDrag.GetComponent<SkillSlotUI>() : null;

            // 자기 자신에게 드롭한 것이 아닌 경우 스왑 이벤트 발생
            if (draggedSlot != null && draggedSlot.isDragging && draggedSlot.currentSkill != null && draggedSlot != this)
            {
                OnDropSkill?.Invoke(draggedSlot, this);
            }
        }

        public SkillData GetSkill() => currentSkill;
    }
}
