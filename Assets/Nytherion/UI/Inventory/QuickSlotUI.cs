using System;
using Nytherion.Data.ScriptableObjects.Items;
using Nytherion.Data.ScriptableObjects.Skill;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Nytherion.UI.Inventory
{
    /// <summary>기존 퀵슬롯의 씬 참조를 유지하며 장착 스킬을 표시하는 HUD 슬롯입니다.</summary>
    public class QuickSlotUI : BaseSlotUI, IDropHandler
    {
        public int SlotIndex { get; private set; }
        public SkillData CurrentSkill { get; private set; }

        public void Initialize(int index)
        {
            SlotIndex = index;
            ApplyIconLayout();
        }

        protected override void Awake()
        {
            // HUD는 아이템 수량 및 드래그 상태를 사용하지 않습니다.
            currentItem = null;
            currentCount = 0;
            countText = null;
            SetSkill(CurrentSkill);
        }

        public void SetSkill(SkillData skill)
        {
            CurrentSkill = skill;
            if (iconImage == null) return;
            iconImage.sprite = skill != null ? skill.icon : null;
            iconImage.enabled = iconImage.sprite != null;
            iconImage.color = Color.white;
            iconImage.preserveAspect = true;
            iconImage.raycastTarget = false;
            ApplyIconLayout();
        }

        protected override void ApplyIconLayout()
        {
            if (iconImage == null || iconImage.transform == transform) return;
            RectTransform rect = iconImage.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            if (rect.sizeDelta != new Vector2(80f, 80f))
                rect.sizeDelta = new Vector2(80f, 80f);
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
        }

#if UNITY_EDITOR
        // 프리팹 로드 중 OnValidate에서는 RectTransform 변경 메시지를 발생시키지 않습니다.
        protected override void OnValidate() { }
#endif

        // HUD는 장착 데이터의 표시만 담당하며 소모품 이동이나 사용을 받지 않습니다.
        public override bool CanReceiveItem(ItemData item) => false;
        public override void SetItem(ItemData item, int count, Action<ItemData, int> onUseCallback = null) { }
        public override void ClearSlot() => SetSkill(null);
        public override void UseItem() { }
        public void OnDrop(PointerEventData eventData) { }
        public override void OnBeginDrag(PointerEventData eventData) { }
        public override void OnDrag(PointerEventData eventData) { }
        public override void OnEndDrag(PointerEventData eventData) { }
    }
}
