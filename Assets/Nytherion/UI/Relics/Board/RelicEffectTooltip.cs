using System;
using System.Collections.Generic;
using System.Globalization;
using Nytherion.Data.ScriptableObjects.Relics;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Nytherion.UI.RelicBoard
{
    /// <summary>하이라키에서 편집한 패널과 텍스트 배치를 그대로 사용하는 효과 현황 툴팁.</summary>
    [RequireComponent(typeof(RectTransform), typeof(CanvasGroup))]
    public sealed class RelicEffectTooltip : MonoBehaviour
    {
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private Image titleIcon;
        [SerializeField, Min(0f)] private float titleIconSpacing = 12f;
        [SerializeField] private TextMeshProUGUI descriptionText;
        [SerializeField] private Image requirementIconTemplate;
        [SerializeField, Min(1f)] private float requirementIconSize = 64f;
        [SerializeField, Min(0f)] private float requirementIconSpacing = 8f;
        [SerializeField] private Vector2 cursorOffset = new Vector2(15f, 15f);

        private readonly Vector3[] corners = new Vector3[4];
        private readonly List<Image> requirementIconViews = new List<Image>();
        private IReadOnlyList<Sprite> requirementIcons;
        private bool requirementLayoutDirty;
        private RectTransform panelRect;
        private Canvas canvas;
        private bool visible;
        private Color defaultTitleColor;

        private void Awake()
        {
            panelRect = (RectTransform)transform;
            canvas = GetComponentInParent<Canvas>().rootCanvas;
            defaultTitleColor = titleText.color;
            Hide();
        }

        private void LateUpdate()
        {
            if (!visible) return;
            UpdateTitleIconPosition();
            UpdateRequirementIconPositions();
            UpdatePosition();
        }

        private void OnDisable()
        {
            Hide();
        }

        public void Show(string title, string description, Color? titleColor = null, Sprite icon = null,
            IReadOnlyList<Sprite> requirementIcons = null)
        {
            gameObject.SetActive(true);
            titleText.text = title;
            titleText.color = titleColor ?? defaultTitleColor;
            if (titleIcon != null)
            {
                titleIcon.sprite = icon;
                titleIcon.preserveAspect = true;
                titleIcon.enabled = icon != null;
            }
            descriptionText.text = FormatRequirementText(description, requirementIcons);
            BindRequirementIcons(requirementIcons);
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.alpha = 1f;
            visible = true;
            UpdateTitleIconPosition();
            UpdateRequirementIconPositions();
            UpdatePosition();
        }

        public void Hide()
        {
            visible = false;
            if (canvasGroup != null) canvasGroup.alpha = 0f;
            foreach (Image image in requirementIconViews) image.enabled = false;
        }

        private void BindRequirementIcons(IReadOnlyList<Sprite> icons)
        {
            requirementIcons = icons;
            requirementLayoutDirty = true;
            int count = requirementIconTemplate != null ? icons?.Count ?? 0 : 0;
            while (requirementIconViews.Count < count)
            {
                Image image = Instantiate(requirementIconTemplate, requirementIconTemplate.transform.parent, false);
                image.name = "RequirementIcon_" + requirementIconViews.Count;
                image.gameObject.SetActive(true);
                image.raycastTarget = false;
                requirementIconViews.Add(image);
            }
            for (int i = 0; i < requirementIconViews.Count; i++)
            {
                requirementIconViews[i].sprite = i < count ? icons[i] : null;
                requirementIconViews[i].enabled = false;
            }
        }

        private string FormatRequirementText(string description, IReadOnlyList<Sprite> icons)
        {
            if (requirementIconTemplate == null || icons == null || string.IsNullOrEmpty(description))
                return description;

            string indent = (requirementIconSize + requirementIconSpacing).ToString(CultureInfo.InvariantCulture);
            string lineHeight = Mathf.Max(requirementIconSize + requirementIconSpacing * 2f,
                descriptionText.fontSize * 1.2f).ToString(CultureInfo.InvariantCulture);
            for (int i = 0; i < icons.Count; i++)
            {
                if (icons[i] == null) continue;
                string openingTag = "<link=\"" + RelicTranscendenceData.RequirementLinkPrefix + i + "\">";
                int start = description.IndexOf(openingTag, StringComparison.Ordinal);
                if (start < 0) continue;
                int closing = description.IndexOf("</link>", start, StringComparison.Ordinal);
                if (closing < 0) continue;
                int end = closing + "</link>".Length;
                if (end < description.Length && description[end] == '\r') end++;
                if (end < description.Length && description[end] == '\n') end++;
                string row = description.Substring(start, end - start);
                // 원래 글자 시작 위치를 아이콘에 내주고, 줄바꿈된 조건도 같은 들여쓰기를 사용한다.
                string formatted = "<line-height=" + lineHeight + "><indent=" + indent + ">" +
                                   row + "</indent></line-height>";
                description = description.Remove(start, end - start).Insert(start, formatted);
            }
            return description;
        }

        private void UpdateRequirementIconPositions()
        {
            if (requirementIcons == null || requirementIconViews.Count == 0) return;
            if (!requirementLayoutDirty && !descriptionText.havePropertiesChanged) return;
            descriptionText.ForceMeshUpdate();
            requirementLayoutDirty = false;
            TMP_TextInfo textInfo = descriptionText.textInfo;
            for (int i = 0; i < requirementIconViews.Count; i++)
            {
                Image image = requirementIconViews[i];
                image.enabled = false;
                if (image.sprite == null || i >= requirementIcons.Count) continue;
                string linkId = RelicTranscendenceData.RequirementLinkPrefix + i;
                for (int linkIndex = 0; linkIndex < textInfo.linkCount; linkIndex++)
                {
                    TMP_LinkInfo link = textInfo.linkInfo[linkIndex];
                    if (link.GetLinkID() != linkId) continue;
                    int first = link.linkTextfirstCharacterIndex;
                    int end = Mathf.Min(first + link.linkTextLength, textInfo.characterCount);
                    Vector2 minimum = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
                    Vector2 maximum = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
                    int lineNumber = -1;
                    for (int characterIndex = first; characterIndex < end; characterIndex++)
                    {
                        TMP_CharacterInfo character = textInfo.characterInfo[characterIndex];
                        if (!character.isVisible) continue;
                        if (lineNumber < 0) lineNumber = character.lineNumber;
                        if (character.lineNumber != lineNumber) break;
                        minimum = Vector2.Min(minimum, character.bottomLeft);
                        maximum = Vector2.Max(maximum, character.topRight);
                    }
                    if (lineNumber < 0) break;
                    // 줄바꿈과 언어 변경 후에도 해당 조건의 첫 줄 왼쪽에 맞춘다.
                    float size = requirementIconSize;
                    RectTransform rect = image.rectTransform;
                    rect.sizeDelta = Vector2.one * size;
                    Vector3 edge = rect.TransformPoint(new Vector3(rect.rect.xMax, rect.rect.center.y, 0f));
                    Vector3 target = descriptionText.rectTransform.TransformPoint(
                        new Vector3(minimum.x - requirementIconSpacing, (minimum.y + maximum.y) * 0.5f, 0f));
                    rect.position += target - edge;
                    image.enabled = true;
                    break;
                }
            }
        }

        private void UpdateTitleIconPosition()
        {
            if (titleIcon == null || !titleIcon.isActiveAndEnabled) return;

            if (titleText.havePropertiesChanged) titleText.ForceMeshUpdate();
            if (titleText.textInfo.characterCount == 0) return;

            // 편집한 제목 배치는 유지하고, 실제 글자 왼쪽에 아이콘의 오른쪽 중앙을 맞춘다.
            Bounds textBounds = titleText.textBounds;
            Vector3 textEdge = titleText.rectTransform.TransformPoint(
                new Vector3(textBounds.min.x - titleIconSpacing, textBounds.center.y, 0f));
            RectTransform iconRect = titleIcon.rectTransform;
            Vector3 iconEdge = iconRect.TransformPoint(
                new Vector3(iconRect.rect.xMax, iconRect.rect.center.y, 0f));
            iconRect.position += textEdge - iconEdge;
        }

        private void UpdatePosition()
        {
            RectTransform canvasRect = (RectTransform)canvas.transform;
            Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect, Input.mousePosition, camera, out Vector2 pointer)) return;

            // 루트의 마우스 추적 위치만 바꾸고 크기·피벗·자식 배치는 유지한다.
            panelRect.GetWorldCorners(corners);
            Vector2 pivot = canvasRect.InverseTransformPoint(panelRect.position);
            Vector2 minimum = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            Vector2 maximum = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            foreach (Vector3 corner in corners)
            {
                Vector2 point = (Vector2)canvasRect.InverseTransformPoint(corner) - pivot;
                minimum = Vector2.Min(minimum, point);
                maximum = Vector2.Max(maximum, point);
            }

            Rect bounds = canvasRect.rect;
            float x = pointer.x + cursorOffset.x - minimum.x;
            float y = pointer.y - cursorOffset.y - maximum.y;
            if (x + maximum.x > bounds.xMax) x = pointer.x - cursorOffset.x - maximum.x;
            if (y + minimum.y < bounds.yMin) y = pointer.y + cursorOffset.y - minimum.y;
            x = maximum.x - minimum.x > bounds.width ? bounds.xMin - minimum.x :
                Mathf.Clamp(x, bounds.xMin - minimum.x, bounds.xMax - maximum.x);
            y = maximum.y - minimum.y > bounds.height ? bounds.yMax - maximum.y :
                Mathf.Clamp(y, bounds.yMin - minimum.y, bounds.yMax - maximum.y);
            panelRect.position = canvasRect.TransformPoint(new Vector2(x, y));
        }
    }
}
