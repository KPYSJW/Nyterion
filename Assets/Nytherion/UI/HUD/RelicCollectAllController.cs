using System;
using Nytherion.Core.Managers;
using Nytherion.GamePlay.Characters.Player;
using Nytherion.GamePlay.Relics;
using Nytherion.GamePlay.Characters.NPC;
using Nytherion.GamePlay.Skills;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer.Unity;

namespace Nytherion.UI.Components
{
    public sealed class RelicCollectAllController : IInitializable, ITickable, IDisposable
    {
        private readonly InputManager inputManager;
        private readonly Transform player;
        private readonly Sprite keyIcon;
        private readonly GameSceneUIRefs uiRefs;
        private readonly InteractionManager interactionManager;
        private GameObject canvasObject;
        private GameObject prompt;
        private TextMeshProUGUI countText;
        private int displayedCount = -1;
        private GameObject interactionPrompt;
        private TextMeshProUGUI interactionText;
        private GameObject singleDrawPrompt;
        private GameObject tenDrawPrompt;
        private RectTransform portalPrompt;
        private TextMeshProUGUI portalText;

        public RelicCollectAllController(InputManager inputManager, PlayerController playerController,
            GameSceneUIRefs uiRefs, InteractionManager interactionManager)
        {
            this.inputManager = inputManager;
            player = playerController.transform;
            keyIcon = uiRefs.RelicCollectAllKeyIcon;
            this.uiRefs = uiRefs;
            this.interactionManager = interactionManager;
        }

        public void Initialize()
        {
            CreatePrompt();
            inputManager.onCollectAllRelics += CollectAll;
        }

        public void Tick()
        {
            bool canInteract = inputManager.CanCollectAllRelics && player != null;
            int count = canInteract ? DroppedRelic.CountNearbyCollectible(player) : 0;
            if (displayedCount != count)
            {
                displayedCount = count;
                if (count > 0) countText.SetText("모두 획득 ({0})", count);
            }
            prompt.SetActive(count > 0);

            bool hasSelectedRelic = canInteract && RelicPickupDetector.HasSelectedRelic(player);
            string action = canInteract
                ? (hasSelectedRelic ? "획득" : interactionManager.GetInteractionPrompt())
                : null;
            interactionPrompt.SetActive(action != null);
            if (action != null && interactionText.text != action) interactionText.text = action;
            UpdatePortalPrompt(canInteract && !hasSelectedRelic ? interactionManager.GetPortalTarget() : null);

            bool canDraw = canInteract && GachaChest.FindAvailableChest(player) != null;
            singleDrawPrompt.SetActive(canDraw);
            tenDrawPrompt.SetActive(canDraw);

            int row = 0;
            PlacePrompt(interactionPrompt, ref row);
            PlacePrompt(prompt, ref row);
            PlacePrompt(singleDrawPrompt, ref row);
            PlacePrompt(tenDrawPrompt, ref row);
        }

        private void UpdatePortalPrompt(Transform target)
        {
            Camera camera = Camera.main;
            if (target == null || camera == null)
            {
                portalPrompt.gameObject.SetActive(false);
                return;
            }

            // 포털 아래에 키 이미지와 이동 안내를 함께 표시합니다.
            SpriteRenderer visual = target.GetComponentInChildren<SpriteRenderer>();
            Vector3 position = visual != null ? visual.bounds.center : target.position;
            position.y = visual != null ? visual.bounds.min.y : position.y - 0.75f;
            Vector3 screenPosition = camera.WorldToScreenPoint(position);
            if (screenPosition.z <= 0f || !camera.pixelRect.Contains(screenPosition))
            {
                portalPrompt.gameObject.SetActive(false);
                return;
            }

            screenPosition.y -= 28f * canvasObject.GetComponent<Canvas>().scaleFactor;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                (RectTransform)canvasObject.transform, screenPosition, null, out Vector2 localPosition);
            portalPrompt.anchoredPosition = localPosition;
            PortalGateEndpoint gate = target.GetComponent<PortalGateEndpoint>();
            string text = gate != null ? gate.InteractionPrompt : "다음 스테이지";
            if (portalText.text != text) portalText.text = text;
            portalPrompt.gameObject.SetActive(true);
        }

        private static void PlacePrompt(GameObject instance, ref int row)
        {
            if (!instance.activeSelf) return;
            ((RectTransform)instance.transform).anchoredPosition = new Vector2(0f, 140f + row * 52f);
            row++;
        }

        private void CollectAll()
        {
            if (!inputManager.CanCollectAllRelics || player == null) return;
            DroppedRelic.BeginAutoCollectNearby(player);
            Tick();
        }

        private void CreatePrompt()
        {
            // 씬 스코프에서 생성·해제하는 표시 전용 HUD이며 마우스 입력을 받지 않습니다.
            canvasObject = new GameObject("RelicCollectAllHUD", typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
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

            prompt = CreatePromptRow("CollectAllPrompt", keyIcon, "", out countText);
            interactionPrompt = CreatePromptRow("InteractionPrompt", uiRefs.InteractionKeyIcon, "", out interactionText);
            singleDrawPrompt = CreatePromptRow("SingleDrawPrompt", uiRefs.GachaSingleKeyIcon, "1회 뽑기", out _);
            tenDrawPrompt = CreatePromptRow("TenDrawPrompt", uiRefs.GachaTenKeyIcon, "10회 뽑기", out _);

            GameObject portalIcon = CreatePromptRow("PortalPrompt", uiRefs.InteractionKeyIcon, "", out portalText);
            portalPrompt = (RectTransform)portalIcon.transform;
            portalPrompt.anchorMin = portalPrompt.anchorMax = new Vector2(0.5f, 0.5f);
            portalPrompt.pivot = new Vector2(0.5f, 0.5f);
            portalPrompt.sizeDelta = new Vector2(320f, 44f);
            portalText.rectTransform.offsetMax = new Vector2(312f, -4f);
        }

        private GameObject CreatePromptRow(string name, Sprite icon, string text, out TextMeshProUGUI label)
        {
            var instance = new GameObject(name, typeof(RectTransform));
            RectTransform rect = (RectTransform)instance.transform;
            rect.SetParent(canvasObject.transform, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 140f);
            rect.sizeDelta = new Vector2(240f, 44f);

            var keyObject = new GameObject("Key", typeof(RectTransform), typeof(Image));
            RectTransform keyRect = (RectTransform)keyObject.transform;
            keyRect.SetParent(instance.transform, false);
            keyRect.anchorMin = keyRect.anchorMax = new Vector2(0f, 0.5f);
            keyRect.anchoredPosition = new Vector2(26f, 0f);
            keyRect.sizeDelta = new Vector2(36f, 36f);
            Image keyImage = keyObject.GetComponent<Image>();
            keyImage.sprite = icon;
            keyImage.preserveAspect = true;
            keyImage.raycastTarget = false;
            label = CreateText(instance.transform, "Count", text, new Vector2(46f, 4f), new Vector2(232f, -4f));
            instance.SetActive(false);
            return instance;
        }

        private TextMeshProUGUI CreateText(Transform parent, string name, string text, Vector2 minimum, Vector2 maximum)
        {
            var labelObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            RectTransform rect = (RectTransform)labelObject.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = new Vector2(0f, 1f);
            rect.offsetMin = minimum;
            rect.offsetMax = maximum;
            TextMeshProUGUI label = labelObject.GetComponent<TextMeshProUGUI>();
            label.font = TMP_Settings.defaultFontAsset;
            label.fontSize = 22f;
            label.alignment = TextAlignmentOptions.Center;
            label.enableWordWrapping = false;
            label.raycastTarget = false;
            label.text = text;
            return label;
        }

        public void Dispose()
        {
            inputManager.onCollectAllRelics -= CollectAll;
            if (canvasObject != null) UnityEngine.Object.Destroy(canvasObject);
        }
    }
}
