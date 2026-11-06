using System;
using Nytherion.Data.ScriptableObjects.Items;
using Nytherion.Data.ScriptableObjects.Skill;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Nytherion.UI.Inventory
{
    /// <summary>기존 퀵슬롯의 씬 참조를 유지하며 장착 스킬을 표시하는 HUD 슬롯입니다.</summary>
    public class QuickSlotUI : BaseSlotUI, IDropHandler
    {
        public int SlotIndex { get; private set; }
        public SkillData CurrentSkill { get; private set; }
        [SerializeField] private Image skillIconBackground;
        [Tooltip("슬롯 이미지에서 아이콘이 들어갈 내부 영역입니다. 좌측 하단 기준의 정규화 좌표입니다.")]
        [SerializeField] private Rect skillIconArea = new Rect(0f, 0f, 1f, 1f);
        [Tooltip("아이콘 배경의 크기입니다. Canvas 배율 적용 전 UI 단위입니다.")]
        [SerializeField] private Vector2 skillBackgroundSize = new Vector2(96f, 96f);
        public Vector2 SkillBackgroundSize => skillBackgroundSize;

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
            UpdateIconBackground();
            ApplyIconLayout();
        }

        private void UpdateIconBackground()
        {
            if (iconImage.transform == transform) return;
            if (skillIconBackground == null)
            {
                // 기존 씬과 프리팹의 참조를 유지하며 아이콘 바로 뒤에 배경을 추가합니다.
                GameObject background = new GameObject("SkillIconBackground", typeof(RectTransform), typeof(Image));
                background.layer = iconImage.gameObject.layer;
                background.transform.SetParent(iconImage.transform.parent, false);
                background.transform.SetSiblingIndex(iconImage.transform.GetSiblingIndex());
                skillIconBackground = background.GetComponent<Image>();
                skillIconBackground.raycastTarget = false;
            }
            skillIconBackground.color = new Color32(43, 33, 28, 255);
            skillIconBackground.enabled = iconImage.enabled;
        }

        protected override void ApplyIconLayout()
        {
            if (iconImage == null || iconImage.transform == transform) return;
            RectTransform rect = iconImage.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            RectTransform slotRect = transform as RectTransform;
            Vector2 slotSize = slotRect != null ? slotRect.rect.size : Vector2.one * 96f;
            rect.anchoredPosition = Vector2.Scale(skillIconArea.center - Vector2.one * 0.5f, slotSize);
            Vector2 iconSize = Vector2.Scale(skillIconArea.size, slotSize);
            if (rect.sizeDelta != iconSize)
                rect.sizeDelta = iconSize;
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
            if (skillIconBackground != null)
            {
                RectTransform backgroundRect = skillIconBackground.rectTransform;
                backgroundRect.anchorMin = rect.anchorMin;
                backgroundRect.anchorMax = rect.anchorMax;
                backgroundRect.pivot = rect.pivot;
                backgroundRect.anchoredPosition = rect.anchoredPosition;
                backgroundRect.sizeDelta = skillBackgroundSize;
                backgroundRect.localScale = rect.localScale;
                backgroundRect.localRotation = rect.localRotation;
            }
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
