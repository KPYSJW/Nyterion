using Nytherion.Core.Utils;
using Nytherion.Data.ScriptableObjects.Skill;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;

namespace Nytherion.UI.Skill
{
    /// <summary>스킬창이 소유하는 전용 툴팁. 슬롯에서 참조를 전달받아 사용한다.</summary>
    public class SkillTooltip : MonoBehaviour
    {
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private Image skillIcon;
        [SerializeField] private TextMeshProUGUI progressText;
        [SerializeField] private TextMeshProUGUI statsText;
        [SerializeField] private TextMeshProUGUI descriptionText;

        private SkillData currentSkill;
        private SkillSlotUI owner;
        private int level, exp, requiredExp;
        private bool localeSubscribed;

        private void Awake()
        {
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
            Hide();
        }

        private void OnEnable()
        {
            LocalizationText.LanguageChanged += RefreshContent;
            if (LocalizationText.IsConfigured)
            {
                LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;
                localeSubscribed = true;
            }
        }

        private void OnDisable()
        {
            LocalizationText.LanguageChanged -= RefreshContent;
            if (localeSubscribed)
                LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;
            localeSubscribed = false;
            Hide();
        }

        public void Show(SkillSlotUI source, SkillData skill, int skillLevel, int skillExp, int skillRequiredExp)
        {
            if (skill == null) return;
            owner = source;
            currentSkill = skill;
            level = skillLevel;
            exp = skillExp;
            requiredExp = skillRequiredExp;
            RefreshContent();
            canvasGroup.alpha = 1f;
            UpdatePosition();
        }

        public void Hide(SkillSlotUI source = null)
        {
            if (source != null && owner != source) return;
            currentSkill = null;
            owner = null;
            if (canvasGroup != null) canvasGroup.alpha = 0f;
        }

        private void OnLocaleChanged(Locale _) => RefreshContent();

        private void RefreshContent()
        {
            if (currentSkill == null) return;
            nameText.text = currentSkill.DisplayName;
            skillIcon.sprite = currentSkill.icon;
            skillIcon.gameObject.SetActive(currentSkill.icon != null);
            progressText.text = LocalizationText.Get(LocalizationTables.UI, "ui.skill_tooltip.progress",
                "Lv.{0} / 경험치 {1}/{2}", "Lv.{0} / EXP {1}/{2}", level, exp, requiredExp);
            statsText.text = LocalizationText.Get(LocalizationTables.UI, "ui.skill_tooltip.stats",
                "피해  {0}\n쿨타임  {1}초",
                "Damage  {0}\nCooldown  {1}s",
                currentSkill.damage, currentSkill.coolDown);
            descriptionText.text = currentSkill.Description;
            RectTransform rect = (RectTransform)transform;
            LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,
                Mathf.Max(480f, LayoutUtility.GetPreferredHeight(rect)));
            LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
        }

        private void LateUpdate()
        {
            if (currentSkill != null) UpdatePosition();
        }

        private void UpdatePosition()
        {
            RectTransform rect = (RectTransform)transform;
            Canvas canvas = GetComponentInParent<Canvas>().rootCanvas;
            Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            RectTransform canvasRect = (RectTransform)canvas.transform;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, Input.mousePosition,
                camera, out Vector2 pointer)) return;

            // 작은 화면에서는 툴팁 전체를 축소하고 캔버스 경계 안에 배치한다.
            float scale = Mathf.Min(1f, Mathf.Min(canvasRect.rect.width / rect.rect.width,
                canvasRect.rect.height / rect.rect.height));
            rect.localScale = Vector3.one * scale;
            float width = rect.rect.width * scale;
            float height = rect.rect.height * scale;
            Rect bounds = canvasRect.rect;
            float x = pointer.x + 16f;
            float y = pointer.y - 16f;
            if (x + width > bounds.xMax) x = pointer.x - width - 16f;
            if (y - height < bounds.yMin) y = pointer.y + height + 16f;
            Vector2 position = new Vector2(Mathf.Clamp(x, bounds.xMin, bounds.xMax - width),
                Mathf.Clamp(y, bounds.yMin + height, bounds.yMax));
            rect.position = canvasRect.TransformPoint(position);
        }
    }
}
