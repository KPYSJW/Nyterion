using Nytherion.Core.Utils;
using Nytherion.Core.Managers;
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
        [SerializeField] private RectTransform levelIconPanel;
        [SerializeField] private Sprite levelIcon;
        [SerializeField] private Sprite levelEmptyIcon;
        [SerializeField] private TextMeshProUGUI statsText;
        [SerializeField] private TextMeshProUGUI descriptionText;

        private SkillData currentSkill;
        private SkillSlotUI owner;
        private int level;
        private readonly Image[] levelIcons = new Image[SkillState.MaxLevel];
        private bool localeSubscribed;
        private Nytherion.Core.Managers.PlayerManager player;
        public void BindPlayer(Nytherion.Core.Managers.PlayerManager owner) => player = owner;

        private void Awake()
        {
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
            InitializeLevelIcons();
            Hide();
        }

        private void InitializeLevelIcons()
        {
            if (levelIconPanel == null) return;

            const float iconSize = 32f;
            const float spacing = 4f;
            float width = SkillState.MaxLevel * iconSize + (SkillState.MaxLevel - 1) * spacing;
            for (int i = 0; i < levelIcons.Length; i++)
            {
                GameObject iconObject = new GameObject($"LevelIcon_{i + 1}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                iconObject.layer = levelIconPanel.gameObject.layer;
                iconObject.transform.SetParent(levelIconPanel, false);
                RectTransform iconRect = (RectTransform)iconObject.transform;
                iconRect.anchorMin = iconRect.anchorMax = new Vector2(0.5f, 0.5f);
                iconRect.pivot = new Vector2(0.5f, 0.5f);
                iconRect.sizeDelta = Vector2.one * iconSize;
                iconRect.anchoredPosition = new Vector2(-width * 0.5f + iconSize * 0.5f + i * (iconSize + spacing), 0f);
                levelIcons[i] = iconObject.GetComponent<Image>();
                levelIcons[i].raycastTarget = false;
                levelIcons[i].preserveAspect = true;
            }
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

        public void Show(SkillSlotUI source, SkillData skill, int skillLevel)
        {
            if (skill == null) return;
            owner = source;
            currentSkill = skill;
            level = skillLevel;
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
            float damageMultiplier = player != null && player.currentPlayerData != null ? Mathf.Max(0f, player.currentPlayerData.skillDamageMultiplier) : 1f;
            float cooldownMultiplier = player != null && player.currentPlayerData != null ? Mathf.Max(0f, player.currentPlayerData.skillCooldownMultiplier) : 1f;
            float cooldown = (currentSkill is ProjectileGateSkillData gateData ? gateData.GetCooldown(level) : currentSkill.coolDown) * cooldownMultiplier;
            nameText.text = currentSkill.DisplayName;
            skillIcon.sprite = currentSkill.icon;
            skillIcon.gameObject.SetActive(currentSkill.icon != null);
            for (int i = 0; i < levelIcons.Length; i++)
            {
                if (levelIcons[i] != null) levelIcons[i].sprite = i < level ? levelIcon : levelEmptyIcon;
            }
            statsText.text = LocalizationText.Get(LocalizationTables.UI, "ui.skill_tooltip.stats",
                "피해  {0}\n쿨타임  {1}초",
                "Damage  {0}\nCooldown  {1}s",
                currentSkill.damage * damageMultiplier, cooldown);
            if (currentSkill is MeteorStrikeSkillData meteor)
                statsText.text = LocalizationText.Get(LocalizationTables.UI, "ui.skill_tooltip.meteor_strike_stats",
                    "피해  {0}\n쿨타임  {1}초\n낙하 간격  {2}초\n조준 대상  화면 안의 모든 적",
                    "Damage  {0}\nCooldown  {1}s\nStrike interval  {2}s\nTargets  All enemies in view",
                    meteor.GetDamage(level) * damageMultiplier, cooldown, meteor.strikeInterval);
            if (currentSkill is PortalGateSkillData portal)
                statsText.text = LocalizationText.Get(LocalizationTables.UI, "ui.skill_tooltip.portal_screen_stats",
                    "쿨타임  {0}초\n지속시간  {1}초\n이용 횟수  {2}회 (공유)\n생성 위치  카메라 화면 가장자리",
                    "Cooldown  {0}s\nDuration  {1}s\nShared trips  {2}\nPlacement  Camera edge",
                    cooldown, portal.lifetime, portal.maxTraversals);
            if (currentSkill is DecoySkillData decoy)
                statsText.text = LocalizationText.Get(LocalizationTables.UI, "ui.skill_tooltip.decoy_stats",
                    "내구도  {0}\n지속시간  {1}초\n유인 반경  {2}\n쿨타임  {3}초",
                    "Durability  {0}\nDuration  {1}s\nTaunt radius  {2}\nCooldown  {3}s",
                    decoy.GetDurability(level), decoy.GetLifetime(level), decoy.tauntRadius, cooldown);
            if (currentSkill is BubbleShieldSkillData shield)
                statsText.text = LocalizationText.Get(LocalizationTables.UI, "ui.skill_tooltip.bubble_shield_stats",
                    "실드량  {0}\n지속시간  {1}초\n쿨타임  {2}초",
                    "Shield  {0}\nDuration  {1}s\nCooldown  {2}s",
                    shield.GetShield(level), shield.duration, cooldown);
            descriptionText.text = currentSkill.Description;

            // 내용이 길어지면 능력치와 설명이 겹치지 않도록 아래로 확장한다.
            RectTransform rect = (RectTransform)transform;
            RectTransform statsRect = statsText.rectTransform;
            RectTransform descriptionRect = descriptionText.rectTransform;
            float statsHeight = Mathf.Max(120f, statsText.GetPreferredValues(statsText.text, statsRect.rect.width, 0f).y);
            float descriptionHeight = Mathf.Max(240f,
                descriptionText.GetPreferredValues(descriptionText.text, descriptionRect.rect.width, 0f).y);
            statsRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, statsHeight);
            descriptionRect.anchoredPosition = new Vector2(descriptionRect.anchoredPosition.x, -175f - statsHeight - 15f);
            descriptionRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, descriptionHeight);
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,
                Mathf.Max(600f, 175f + statsHeight + 15f + descriptionHeight + 50f));
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
