using System.Globalization;
using Nytherion.Core.Managers;
using Nytherion.Data.ScriptableObjects.Skill;
using Nytherion.GamePlay.Skills;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace Nytherion.UI.Test
{
    /// <summary>ContentPanel2에서 연쇄 점화의 다음 시전에 적용할 임시 값을 조절합니다.</summary>
    [DisallowMultipleComponent]
    public class ChainIgnitionDebugUI : MonoBehaviour
    {
        [SerializeField] private ChainIgnitionSkillData sourceData;
        private PlayerManager player;
        private DebugPanelUI debugPanel;
        private ChainIgnitionDebugSettings settings;
        private ChainIgnitionSkill testSkill;
        private bool ownsSettings;
        private bool initialized;
        private float nextStatusTime;
        private TMP_Text effectiveText;
        private TMP_Text messageText;
        private TMP_FontAsset font;
        private Material fontMaterial;
        private Transform rows;

        private struct NumberControl
        {
            public string name, label;
            public float min, max;
            public bool integer;
            public NumberControl(string name, string label, float min, float max, bool integer = false)
            {
                this.name = name; this.label = label; this.min = min; this.max = max; this.integer = integer;
            }
        }

        private static readonly NumberControl[] Numbers =
        {
            new NumberControl("ProjectileCount", "투사체 수 (각 파동의 방향 수)", 1, 8, true),
            new NumberControl("SizeMultiplier", "전체 크기 배율 (이펙트 + 피해 범위)", 0.1f, 4f),
            new NumberControl("RangeMultiplier", "퍼지는 거리 배율 (크기 증가분 50% 연동)", 0.1f, 4f),
            new NumberControl("FirstRingRadius", "1차 폭발 거리", 0.01f, 10f),
            new NumberControl("LastRingRadius", "3차 폭발 거리 (2차는 중간 거리)", 0.01f, 20f),
            new NumberControl("HorizontalRadius", "피해 범위 가로 반경", 0.01f, 5f),
            new NumberControl("VerticalRadius", "피해 범위 세로 반경 (타원)", 0.01f, 5f),
            new NumberControl("VisualScale", "이펙트만 크기", 0.1f, 5f),
            new NumberControl("CenterX", "원 전체 중심 보정 X", -3f, 3f),
            new NumberControl("CenterY", "원 전체 중심 보정 Y", -3f, 3f),
            new NumberControl("VisualX", "이미지만 보정 X", -2f, 2f),
            new NumberControl("VisualY", "이미지만 보정 Y", -2f, 2f),
            new NumberControl("SoundVolume", "폭발 효과음 볼륨", 0f, 1f)
        };

        public void Initialize(PlayerManager playerManager, DebugPanelUI panel)
        {
            if (initialized) return;
            player = playerManager;
            debugPanel = panel;
            if (sourceData == null || player == null)
            {
                Debug.LogWarning("[ChainIgnitionDebugUI] 연쇄 점화 데이터 또는 플레이어 참조가 없습니다.", this);
                return;
            }
            settings = player.GetComponent<ChainIgnitionDebugSettings>();
            if (settings == null)
            {
                settings = player.gameObject.AddComponent<ChainIgnitionDebugSettings>();
                settings.ResetToDefaults(sourceData);
                ownsSettings = true;
            }
            BuildControls(null);
            Transform root = transform.Find("ChainIgnitionControls");
            rows = root.Find("Scroll/Viewport/Rows");
            effectiveText = root.Find("EffectiveValues").GetComponent<TMP_Text>();
            messageText = root.Find("Message").GetComponent<TMP_Text>();
            BindToggle("UseTestSettings", value => settings.useTestSettings = value, false);
            BindToggle("OverrideProjectileCount", value => settings.overrideProjectileCount = value);
            BindToggle("UseEllipse", value => settings.useEllipse = value);
            BindToggle("ShowHitRanges", value => settings.showHitRanges = value);
            foreach (NumberControl number in Numbers)
            {
                Transform row = rows.Find(number.name);
                row.Find("Label").GetComponent<TMP_Text>().text = number.label;
                Slider slider = row.Find("Slider").GetComponent<Slider>();
                TMP_InputField input = row.Find("Value").GetComponent<TMP_InputField>();
                slider.onValueChanged.AddListener(value => SetNumber(number.name, value));
                input.onEndEdit.AddListener(text =>
                {
                    if (float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float value) &&
                        !float.IsNaN(value) && !float.IsInfinity(value))
                        SetNumber(number.name, Mathf.Clamp(value, number.min, number.max));
                    else RefreshControls();
                });
            }
            root.Find("Actions/Cast").GetComponent<Button>().onClick.AddListener(TryCast);
            root.Find("Actions/Reset").GetComponent<Button>().onClick.AddListener(ResetValues);
            initialized = true;
            RefreshControls();
        }

        private void BindToggle(string name, System.Action<bool> apply, bool enablesTest = true)
        {
            rows.Find(name).GetComponent<Toggle>().onValueChanged.AddListener(value =>
            {
                apply(value);
                if (enablesTest) settings.useTestSettings = true;
                RefreshControls();
            });
        }

        private void SetNumber(string name, float value)
        {
            settings.useTestSettings = true;
            switch (name)
            {
                case "ProjectileCount": settings.projectileCount = Mathf.RoundToInt(value); settings.overrideProjectileCount = true; break;
                case "SizeMultiplier": settings.sizeMultiplier = value; break;
                case "RangeMultiplier": settings.rangeMultiplier = Mathf.Max(0.01f, value - ChainIgnitionSkillData.GetSizeRangeBonus(settings.sizeMultiplier)); break;
                case "FirstRingRadius": settings.firstRingRadius = value; settings.lastRingRadius = Mathf.Max(value, settings.lastRingRadius); break;
                case "LastRingRadius": settings.lastRingRadius = Mathf.Max(settings.firstRingRadius, value); break;
                case "HorizontalRadius": settings.horizontalRadius = value; break;
                case "VerticalRadius": settings.verticalRadius = value; break;
                case "VisualScale": settings.visualScale = value; break;
                case "CenterX": settings.centerOffset.x = value; break;
                case "CenterY": settings.centerOffset.y = value; break;
                case "VisualX": settings.visualOffset.x = value; break;
                case "VisualY": settings.visualOffset.y = value; break;
                case "SoundVolume": settings.soundVolume = value; break;
            }
            RefreshControls();
        }

        private float GetNumber(string name)
        {
            switch (name)
            {
                case "ProjectileCount": return settings.projectileCount;
                case "SizeMultiplier": return settings.sizeMultiplier;
                case "RangeMultiplier": return ChainIgnitionSkillData.GetSpreadRangeMultiplier(settings.sizeMultiplier, settings.rangeMultiplier);
                case "FirstRingRadius": return settings.firstRingRadius;
                case "LastRingRadius": return settings.lastRingRadius;
                case "HorizontalRadius": return settings.horizontalRadius;
                case "VerticalRadius": return settings.verticalRadius;
                case "VisualScale": return settings.visualScale;
                case "CenterX": return settings.centerOffset.x;
                case "CenterY": return settings.centerOffset.y;
                case "VisualX": return settings.visualOffset.x;
                case "VisualY": return settings.visualOffset.y;
                default: return settings.soundVolume;
            }
        }

        private void RefreshControls()
        {
            if (rows == null || settings == null) return;
            rows.Find("UseTestSettings").GetComponent<Toggle>().SetIsOnWithoutNotify(settings.useTestSettings);
            rows.Find("OverrideProjectileCount").GetComponent<Toggle>().SetIsOnWithoutNotify(settings.overrideProjectileCount);
            rows.Find("UseEllipse").GetComponent<Toggle>().SetIsOnWithoutNotify(settings.useEllipse);
            rows.Find("ShowHitRanges").GetComponent<Toggle>().SetIsOnWithoutNotify(settings.showHitRanges);
            foreach (NumberControl number in Numbers)
            {
                Transform row = rows.Find(number.name);
                float value = GetNumber(number.name);
                Slider slider = row.Find("Slider").GetComponent<Slider>();
                if (number.name == "RangeMultiplier")
                {
                    float minimum = Mathf.Max(number.min, ChainIgnitionSkillData.GetSizeRangeBonus(settings.sizeMultiplier) + 0.01f);
                    slider.SetValueWithoutNotify(Mathf.Max(minimum, slider.value));
                    slider.minValue = minimum;
                }
                slider.SetValueWithoutNotify(value);
                row.Find("Value").GetComponent<TMP_InputField>().SetTextWithoutNotify(
                    value.ToString(number.integer ? "0" : "0.###", CultureInfo.InvariantCulture));
            }
            rows.Find("VerticalRadius/Slider").GetComponent<Slider>().interactable = settings.useEllipse;
            rows.Find("VerticalRadius/Value").GetComponent<TMP_InputField>().interactable = settings.useEllipse;
            RefreshEffectiveValues();
        }

        private void Update()
        {
            if (!initialized || Time.unscaledTime < nextStatusTime) return;
            nextStatusTime = Time.unscaledTime + 0.2f;
            RefreshEffectiveValues();
        }

        private void RefreshEffectiveValues()
        {
            if (player == null || settings == null || effectiveText == null) return;
            bool test = settings.useTestSettings;
            var stats = player.currentPlayerData;
            float size = (stats != null ? stats.projectileSizeMultiplier : 1f) * (test ? settings.sizeMultiplier : 1f);
            float distance = (stats != null ? stats.attackRangeMultiplier : 1f) * (test ? settings.rangeMultiplier : 1f);
            size = Mathf.Max(0.01f, size);
            distance = Mathf.Max(0.01f, distance);
            distance = ChainIgnitionSkillData.GetSpreadRangeMultiplier(size, distance);
            int level = player.GetSkillLevel(sourceData);
            int count = settings.GetProjectileCount(sourceData, level, stats != null ? stats.extraProjectiles : 0f);
            float first = test ? settings.firstRingRadius : sourceData.firstRingRadius;
            float last = Mathf.Max(first, test ? settings.lastRingRadius : sourceData.range);
            float x = Mathf.Max(0.01f, (test ? settings.horizontalRadius : sourceData.explosionRadius) * size);
            bool ellipse = test ? settings.useEllipse : sourceData.useEllipticalHitRange;
            float y = ellipse ? Mathf.Max(0.01f, (test ? settings.verticalRadius : sourceData.explosionVerticalRadius) * size) : x;
            effectiveText.text = $"다음 시전: {(test ? "테스트값" : "기본값")} / Lv.{level} / {count}방향 / 피해 {sourceData.GetDamage(level):0.##}\n" +
                $"실제 거리 {first * distance:0.##} / {(first + last) * 0.5f * distance:0.##} / {last * distance:0.##} / 피해 반경 {x:0.##} x {y:0.##}";
        }

        public void ResetValues()
        {
            if (settings == null || sourceData == null) return;
            settings.ResetToDefaults(sourceData);
            RefreshControls();
            messageText.text = "기본값 복원 완료. 레벨/유물에 따른 정상 계산을 사용합니다.";
        }

        public void TryCast()
        {
            if (!initialized || player == null || sourceData == null) return;
            GameObject selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            if (selected != null && selected.transform.IsChildOf(transform) && selected.GetComponent<TMP_InputField>() != null)
                EventSystem.current.SetSelectedGameObject(null);
            if (testSkill == null)
            {
                if (sourceData.skillPrefab == null) return;
                GameObject instance = Instantiate(sourceData.skillPrefab, player.transform);
                instance.name = "DebugChainIgnitionSkill";
                testSkill = instance.GetComponent<ChainIgnitionSkill>();
                if (testSkill == null) { Destroy(instance); return; }
                testSkill.skillData = sourceData;
                testSkill.caster = player.transform;
            }
            if (testSkill.TryUse())
            {
                messageText.text = "연쇄 점화 테스트 시전 완료. 마우스 방향으로 폭발합니다.";
                debugPanel?.Close();
            }
            else messageText.text = $"재사용 대기 중: {testSkill.GetRemainingCooldown():0.0}초";
        }

        private void OnDestroy()
        {
            if (testSkill != null) Destroy(testSkill.gameObject);
            if (ownsSettings && settings != null) Destroy(settings);
        }

        // 에디터에서도 같은 UI를 생성하여 ContentPanel2의 컨트롤을 직접 확인할 수 있게 합니다.
        public void BuildControls(TMP_Text textTemplate)
        {
            if (transform.Find("ChainIgnitionControls") != null)
            {
                EnsureScrollbar();
                transform.Find("ChainIgnitionControls/Help").GetComponent<TMP_Text>().text = HelpText;
                transform.Find("ChainIgnitionControls/Message").GetComponent<TMP_Text>().text = InitialMessage;
                return;
            }
            font = textTemplate != null ? textTemplate.font : TMP_Settings.defaultFontAsset;
            fontMaterial = textTemplate != null ? textTemplate.fontSharedMaterial : null;
            RectTransform root = Rect("ChainIgnitionControls", transform);
            Stretch(root, new Vector2(12, 12), new Vector2(-12, -12));
            Image background = root.gameObject.AddComponent<Image>();
            background.color = new Color(0.075f, 0.09f, 0.12f, 0.97f);

            TMP_Text title = Label("Title", root, "연쇄 점화 테스트", 26);
            Top(title.rectTransform, 12, 10, -12, 34);
            TMP_Text help = Label("Help", root, HelpText, 17);
            Top(help.rectTransform, 12, 47, -12, 45);
            RectTransform actions = Rect("Actions", root);
            Top(actions, 12, 96, -12, 36);
            Button cast = ActionButton("Cast", actions, "테스트 시전 (F11)");
            SetRect(cast.GetComponent<RectTransform>(), Vector2.zero, new Vector2(0.5f, 1), new Vector2(0, 0), new Vector2(-4, 0));
            Button reset = ActionButton("Reset", actions, "기본값 복원");
            SetRect(reset.GetComponent<RectTransform>(), new Vector2(0.5f, 0), Vector2.one, new Vector2(4, 0), Vector2.zero);
            TMP_Text effective = Label("EffectiveValues", root, "플레이 모드에서 다음 시전의 실제 범위를 표시합니다.", 18);
            Top(effective.rectTransform, 12, 140, -12, 48);
            TMP_Text message = Label("Message", root, InitialMessage, 16);
            Top(message.rectTransform, 12, 192, -12, 26);

            RectTransform scrollRect = Rect("Scroll", root);
            Stretch(scrollRect, new Vector2(12, 12), new Vector2(-12, -226));
            ScrollRect scroll = scrollRect.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 28;
            RectTransform viewport = Rect("Viewport", scrollRect);
            Stretch(viewport, Vector2.zero, Vector2.zero);
            viewport.gameObject.AddComponent<RectMask2D>();
            Image scrollTarget = viewport.gameObject.AddComponent<Image>();
            scrollTarget.color = new Color(0, 0, 0, 0.01f);
            RectTransform content = Rect("Rows", viewport);
            content.anchorMin = new Vector2(0, 1); content.anchorMax = Vector2.one;
            content.pivot = new Vector2(0.5f, 1); content.anchoredPosition = Vector2.zero; content.sizeDelta = Vector2.zero;
            VerticalLayoutGroup layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 6; layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
            ContentSizeFitter fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport; scroll.content = content;
            ToggleRow("UseTestSettings", content, "테스트 설정 사용");
            ToggleRow("OverrideProjectileCount", content, "투사체 수 고정 (끄면 레벨/유물로 계산)");
            foreach (NumberControl number in Numbers) NumberRow(number, content);
            ToggleRow("UseEllipse", content, "타원형 피해 범위 사용");
            ToggleRow("ShowHitRanges", content, "게임 화면에 실제 피해 범위 표시");
            EnsureScrollbar();
        }

        private const string HelpText = "크기 증가분 50%만큼 거리 증가 / 다음 시전 적용 / 종료 시 초기화\n유물 배율 적용 / 휠 스크롤 / F11: 마우스 방향 테스트 시전";
        private const string InitialMessage = "장착 없이 테스트 가능 / 투사체 수 고정을 끄면 레벨/유물 적용";

        private void EnsureScrollbar()
        {
            Transform countLabel = transform.Find("ChainIgnitionControls/Scroll/Viewport/Rows/OverrideProjectileCount/Label");
            if (countLabel != null) countLabel.GetComponent<TMP_Text>().text = "투사체 수 고정 (끄면 레벨/유물로 계산)";
            Transform scrollRoot = transform.Find("ChainIgnitionControls/Scroll");
            if (scrollRoot == null || scrollRoot.Find("Scrollbar") != null) return;
            RectTransform barRect = Rect("Scrollbar", scrollRoot);
            SetRect(barRect, new Vector2(1, 0), Vector2.one, new Vector2(-10, 0), Vector2.zero);
            Image background = barRect.gameObject.AddComponent<Image>(); background.color = new Color(0.17f, 0.22f, 0.28f);
            Scrollbar bar = barRect.gameObject.AddComponent<Scrollbar>();
            RectTransform handle = Rect("Handle", barRect); Stretch(handle, Vector2.zero, Vector2.zero);
            Image handleImage = handle.gameObject.AddComponent<Image>(); handleImage.color = new Color(0.35f, 0.65f, 0.72f);
            bar.handleRect = handle; bar.targetGraphic = handleImage; bar.direction = Scrollbar.Direction.BottomToTop;
            ScrollRect scroll = scrollRoot.GetComponent<ScrollRect>();
            scroll.viewport.offsetMax = new Vector2(-16, scroll.viewport.offsetMax.y);
            scroll.verticalScrollbar = bar; scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
        }

        private void NumberRow(NumberControl number, RectTransform parent)
        {
            RectTransform row = Rect(number.name, parent);
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = 48;
            TMP_Text label = Label("Label", row, number.label, 19);
            Top(label.rectTransform, 0, 0, -106, 22);
            RectTransform sliderRect = Rect("Slider", row);
            SetRect(sliderRect, Vector2.zero, Vector2.one, new Vector2(8, 4), new Vector2(-114, -27));
            Slider slider = sliderRect.gameObject.AddComponent<Slider>();
            slider.minValue = number.min; slider.maxValue = number.max; slider.wholeNumbers = number.integer;
            RectTransform track = Rect("Track", sliderRect);
            SetRect(track, new Vector2(0, 0.35f), new Vector2(1, 0.65f), Vector2.zero, Vector2.zero);
            track.gameObject.AddComponent<Image>().color = new Color(0.24f, 0.3f, 0.36f);
            RectTransform handleArea = Rect("HandleArea", sliderRect);
            Stretch(handleArea, Vector2.zero, Vector2.zero);
            RectTransform handle = Rect("Handle", handleArea);
            handle.anchorMin = Vector2.zero; handle.anchorMax = new Vector2(0, 1);
            handle.sizeDelta = new Vector2(16, 6);
            Image graphic = handle.gameObject.AddComponent<Image>(); graphic.color = new Color(0.35f, 0.88f, 0.94f);
            slider.handleRect = handle; slider.targetGraphic = graphic;
            RectTransform inputRect = Rect("Value", row);
            SetRect(inputRect, new Vector2(1, 0), Vector2.one, new Vector2(-96, 4), new Vector2(0, -4));
            Image inputImage = inputRect.gameObject.AddComponent<Image>(); inputImage.color = new Color(0.17f, 0.22f, 0.28f);
            TMP_InputField input = inputRect.gameObject.AddComponent<TMP_InputField>();
            TMP_Text inputText = Label("Text", inputRect, "0", 20);
            Stretch(inputText.rectTransform, new Vector2(6, 0), new Vector2(-6, 0));
            inputText.alignment = TextAlignmentOptions.MidlineRight;
            input.textViewport = inputRect; input.textComponent = inputText; input.targetGraphic = inputImage;
            input.contentType = number.integer ? TMP_InputField.ContentType.IntegerNumber : TMP_InputField.ContentType.DecimalNumber;
            input.lineType = TMP_InputField.LineType.SingleLine;
            input.text = "0";
        }

        private void ToggleRow(string name, RectTransform parent, string text)
        {
            RectTransform row = Rect(name, parent);
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = 30;
            Toggle toggle = row.gameObject.AddComponent<Toggle>();
            RectTransform box = Rect("Box", row);
            SetRect(box, Vector2.zero, new Vector2(0, 1), new Vector2(0, 3), new Vector2(24, -3));
            Image background = box.gameObject.AddComponent<Image>(); background.color = new Color(0.24f, 0.3f, 0.36f);
            RectTransform tick = Rect("Tick", box); Stretch(tick, new Vector2(5, 5), new Vector2(-5, -5));
            Image mark = tick.gameObject.AddComponent<Image>(); mark.color = new Color(0.35f, 0.88f, 0.94f);
            toggle.targetGraphic = background; toggle.graphic = mark;
            TMP_Text label = Label("Label", row, text, 19);
            Stretch(label.rectTransform, new Vector2(34, 0), Vector2.zero);
            Image clickTarget = row.gameObject.AddComponent<Image>(); clickTarget.color = new Color(0, 0, 0, 0.01f);
        }

        private Button ActionButton(string name, Transform parent, string text)
        {
            RectTransform rect = Rect(name, parent);
            Image image = rect.gameObject.AddComponent<Image>(); image.color = new Color(0.2f, 0.34f, 0.42f);
            Button button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image;
            TMP_Text label = Label("Label", rect, text, 20);
            Stretch(label.rectTransform, Vector2.zero, Vector2.zero); label.alignment = TextAlignmentOptions.Center;
            return button;
        }

        private TMP_Text Label(string name, Transform parent, string text, float size)
        {
            RectTransform rect = Rect(name, parent);
            TMP_Text label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = font;
            if (fontMaterial != null) label.fontSharedMaterial = fontMaterial;
            label.fontSize = size; label.color = Color.white; label.text = text;
            label.raycastTarget = false; label.alignment = TextAlignmentOptions.MidlineLeft;
            label.enableWordWrapping = true;
            return label;
        }

        private static RectTransform Rect(string name, Transform parent)
        {
            var child = new GameObject(name, typeof(RectTransform));
            child.layer = parent.gameObject.layer;
            child.transform.SetParent(parent, false);
            return child.GetComponent<RectTransform>();
        }

        private static void Top(RectTransform rect, float left, float top, float right, float height)
        {
            rect.anchorMin = new Vector2(0, 1); rect.anchorMax = Vector2.one; rect.pivot = new Vector2(0.5f, 1);
            rect.offsetMin = new Vector2(left, -top - height); rect.offsetMax = new Vector2(right, -top);
        }

        private static void Stretch(RectTransform rect, Vector2 min, Vector2 max) => SetRect(rect, Vector2.zero, Vector2.one, min, max);

        private static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 min, Vector2 max)
        {
            rect.anchorMin = anchorMin; rect.anchorMax = anchorMax; rect.offsetMin = min; rect.offsetMax = max;
        }
    }
}
