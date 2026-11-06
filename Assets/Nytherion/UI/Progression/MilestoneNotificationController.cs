using System;
using System.Collections.Generic;
using Nytherion.Core.Managers;
using Nytherion.Core.Utils;
using Nytherion.Data.ScriptableObjects.Progression;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer.Unity;

namespace Nytherion.UI.Progression
{
    public sealed class MilestoneNotificationController : IInitializable, ITickable, IDisposable
    {
        private const float DisplayDuration = 3f;
        private const float ProgressInterval = 8f;
        private const int MaxPendingCompletions = 8;

        private readonly IProgressionManager progressionManager;
        private readonly GameSceneUIRefs uiRefs;
        private readonly Dictionary<string, MilestoneData> milestones = new Dictionary<string, MilestoneData>();
        private readonly Dictionary<string, int> progressBuckets = new Dictionary<string, int>();
        private readonly Queue<MilestoneData> pendingCompletions = new Queue<MilestoneData>();
        private GameObject canvasObject;
        private GameObject panel;
        private TextMeshProUGUI titleText;
        private TextMeshProUGUI progressText;
        private RectTransform fillRect;
        private MilestoneData currentMilestone;
        private bool currentIsCompleted;
        private bool currentIsPreview;
        private float hideAt;
        private float nextProgressAt;

        public MilestoneNotificationController(IProgressionManager progressionManager, GameSceneUIRefs uiRefs)
        {
            this.progressionManager = progressionManager;
            this.uiRefs = uiRefs;
        }

        public void Initialize()
        {
            foreach (MilestoneData milestone in progressionManager.GetAllMilestones())
            {
                if (milestone != null && !string.IsNullOrEmpty(milestone.milestoneID))
                {
                    milestones[milestone.milestoneID] = milestone;
                }
            }

            RefreshProgressBuckets();
            CreateView();
            progressionManager.OnMilestoneProgressUpdated += OnProgressUpdated;
            progressionManager.OnMilestoneCompleted += OnCompleted;
            if (progressionManager is ProgressionManager manager)
                manager.OnProgressionDataLoaded += RefreshProgressBuckets;
            LocalizationText.LanguageChanged += RefreshTitle;
        }

        private void RefreshProgressBuckets()
        {
            foreach (KeyValuePair<string, MilestoneData> entry in milestones)
                progressBuckets[entry.Key] = GetProgressBucket(
                    progressionManager.GetCurrentProgress(entry.Key), entry.Value.targetValue);
        }

        public void Tick()
        {
            if (currentMilestone == null || Time.unscaledTime < hideAt) return;

            currentMilestone = null;
            panel.SetActive(false);
            if (pendingCompletions.Count > 0)
                ShowCompleted(pendingCompletions.Dequeue());
        }

        private void OnProgressUpdated(string id, int current, int target)
        {
            if (!milestones.TryGetValue(id, out MilestoneData milestone)) return;

            int bucket = GetProgressBucket(current, target);
            progressBuckets.TryGetValue(id, out int previousBucket);
            progressBuckets[id] = bucket;

            if (currentMilestone == milestone && !currentIsCompleted && !currentIsPreview)
            {
                UpdateProgress(current, target, false);
                return;
            }

            // 진행 중 알림은 25%, 50%, 75%에만 표시하고 완료 알림은 별도로 처리한다.
            if (bucket <= previousBucket || bucket <= 0 || bucket >= 4) return;
            if (currentMilestone != null || pendingCompletions.Count > 0 || Time.unscaledTime < nextProgressAt) return;
            nextProgressAt = Time.unscaledTime + ProgressInterval;
            Show(milestone, current, target, false);
        }

        private static int GetProgressBucket(int current, int target)
        {
            if (target <= 0) return 0;
            return (int)((long)Mathf.Clamp(current, 0, target) * 4 / target);
        }

        private void OnCompleted(string id)
        {
            if (!milestones.TryGetValue(id, out MilestoneData milestone)) return;

            if (currentMilestone == milestone)
            {
                if (!currentIsCompleted || currentIsPreview) ShowCompleted(milestone);
                return;
            }

            if (currentMilestone == null || !currentIsCompleted || currentIsPreview)
            {
                ShowCompleted(milestone);
                return;
            }

            if (pendingCompletions.Count < MaxPendingCompletions && !pendingCompletions.Contains(milestone))
                pendingCompletions.Enqueue(milestone);
        }

        private void ShowCompleted(MilestoneData milestone)
        {
            Show(milestone, milestone.targetValue, milestone.targetValue, true);
        }

        private void Show(MilestoneData milestone, int current, int target, bool completed, bool preview = false)
        {
            currentMilestone = milestone;
            currentIsCompleted = completed;
            currentIsPreview = preview;
            hideAt = Time.unscaledTime + DisplayDuration;
            RefreshTitle();
            UpdateProgress(current, target, completed);
            panel.SetActive(true);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public bool TryShowDebugPreview(bool completed)
        {
            if (panel == null || pendingCompletions.Count > 0 ||
                (currentMilestone != null && currentIsCompleted && !currentIsPreview) ||
                !milestones.TryGetValue("milestone_kill_100", out MilestoneData milestone))
                return false;

            int target = Mathf.Max(1, milestone.targetValue);
            Show(milestone, completed ? target : target / 2, target, completed, true);
            return true;
        }
#endif

        private void RefreshTitle()
        {
            if (currentMilestone != null && titleText != null)
                titleText.text = currentMilestone.DisplayTitle;
        }

        private void UpdateProgress(int current, int target, bool completed)
        {
            int maximum = Mathf.Max(0, target);
            int value = Mathf.Clamp(current, 0, maximum);
            progressText.text = $"{value}/{maximum}";
            float ratio = maximum > 0 ? (float)value / maximum : (completed ? 1f : 0f);
            fillRect.anchorMax = new Vector2(ratio, 1f);
        }

        private void CreateView()
        {
            // 화면 표시 전용이며 포인터 입력을 가로채지 않는다.
            canvasObject = new GameObject("MilestoneNotificationHUD", typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            CanvasGroup group = canvasObject.GetComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;

            panel = CreateImage("Panel", canvasObject.transform, new Color(0.07f, 0.08f, 0.12f, 0.9f)).gameObject;
            RectTransform panelRect = (RectTransform)panel.transform;
            panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 1f);
            panelRect.pivot = new Vector2(0.5f, 1f);
            panelRect.anchoredPosition = new Vector2(0f, -28f);
            panelRect.sizeDelta = new Vector2(340f, 70f);

            TMP_FontAsset font = uiRefs.ProgressionTitleText != null
                ? uiRefs.ProgressionTitleText.font : TMP_Settings.defaultFontAsset;
            titleText = CreateText("Title", panel.transform, font, 22f, new Vector2(12f, -34f), new Vector2(-12f, -4f));
            Image bar = CreateImage("ProgressBar", panel.transform, new Color(0.18f, 0.19f, 0.23f, 1f));
            RectTransform barRect = (RectTransform)bar.transform;
            barRect.anchorMin = Vector2.zero;
            barRect.anchorMax = new Vector2(1f, 0f);
            barRect.offsetMin = new Vector2(12f, 8f);
            barRect.offsetMax = new Vector2(-12f, 29f);
            Image fill = CreateImage("Fill", bar.transform, new Color(0.42f, 0.67f, 0.44f, 1f));
            fillRect = (RectTransform)fill.transform;
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = fillRect.offsetMax = Vector2.zero;
            progressText = CreateText("Progress", bar.transform, font, 17f, Vector2.zero, Vector2.zero, false);
            panel.SetActive(false);
        }

        private static Image CreateImage(string name, Transform parent, Color color)
        {
            GameObject instance = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            instance.transform.SetParent(parent, false);
            Image image = instance.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static TextMeshProUGUI CreateText(string name, Transform parent, TMP_FontAsset font, float size,
            Vector2 minimum, Vector2 maximum, bool fromTop = true)
        {
            GameObject instance = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            instance.transform.SetParent(parent, false);
            SetOffsets((RectTransform)instance.transform, minimum, maximum, fromTop);
            TextMeshProUGUI text = instance.GetComponent<TextMeshProUGUI>();
            text.font = font;
            text.fontSize = size;
            text.alignment = TextAlignmentOptions.Center;
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.raycastTarget = false;
            return text;
        }

        private static void SetOffsets(RectTransform rect, Vector2 minimum, Vector2 maximum, bool fromTop)
        {
            rect.anchorMin = fromTop ? new Vector2(0f, 1f) : Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = minimum;
            rect.offsetMax = maximum;
        }

        public void Dispose()
        {
            progressionManager.OnMilestoneProgressUpdated -= OnProgressUpdated;
            progressionManager.OnMilestoneCompleted -= OnCompleted;
            if (progressionManager is ProgressionManager manager)
                manager.OnProgressionDataLoaded -= RefreshProgressBuckets;
            LocalizationText.LanguageChanged -= RefreshTitle;
            if (canvasObject != null) UnityEngine.Object.Destroy(canvasObject);
        }
    }
}
