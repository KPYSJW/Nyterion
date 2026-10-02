using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Nytherion.GamePlay.Relics;
using Nytherion.Core.Managers;
using TMPro;
using VContainer;

namespace Nytherion.UI.RelicBoard
{
    public class RelicBlockDraggable : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        public const float StorageIconSize = 96f;
        public const float EquippedIconSize = 64f;

        public RelicBlock blockData;

        public bool isPlaced = false;
        public Vector2Int gridPosition;

        [SerializeField] private Image iconImage;
        private Image storageIcon;
        private TextMeshProUGUI missingIconLabel;

        private CanvasGroup canvasGroup;
        private RectTransform rectTransform;
        [SerializeField] private TextMeshProUGUI levelText;
        private bool isDragging = false;
        
        private InputManager inputManager;
        private RelicManager relicManager;
        private RelicGridUI relicGridUI;
        private RelicTooltip relicTooltip;
        
        [Inject]
        public void Construct(
            InputManager inputManager,
            RelicManager relicManager,
            RelicGridUI relicGridUI,
            RelicTooltip relicTooltip)
        {
            this.inputManager = inputManager;
            this.relicManager = relicManager;
            this.relicGridUI = relicGridUI;
            this.relicTooltip = relicTooltip;
        }
        private void Awake()
        {
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
            rectTransform = GetComponent<RectTransform>();
            Image dragTarget = GetComponent<Image>();
            if (dragTarget == null) dragTarget = gameObject.AddComponent<Image>();
            dragTarget.color = Color.clear;
            dragTarget.raycastTarget = true;
            if (levelText == null)
            {
                levelText = GetComponentInChildren<TextMeshProUGUI>();
            }

            if (levelText != null)
            {
                levelText.gameObject.SetActive(false);
            }
        }
        private void OnEnable()
        {
            if (inputManager != null)
            {
                inputManager.onRelicRotate += HandleRotation;
            }
        }

        private void OnDisable()
        {
            CancelDrag();
            if (inputManager != null)
            {
                inputManager.onRelicRotate -= HandleRotation;
            }
        }
        private void Update()
        {
            if (isDragging)
            {
                if (relicGridUI != null)
                {
                    relicGridUI.ShowPlacementPreview(blockData, relicGridUI.CurrentGridPos);
                }

                if (iconImage != null)
                {
                    iconImage.transform.rotation = Quaternion.identity;
                }
            }
        }
        private void HandleRotation()
        {
            if (isDragging)
            {
                if (relicManager != null)
                {
                    relicManager.RotateDraggedBlock();
                    rectTransform.Rotate(0, 0, 90);
                }
                if (relicGridUI != null)
                {
                    relicGridUI.ShowPlacementPreview(blockData, relicGridUI.CurrentGridPos);
                }
            }
        }
        public void OnBeginDrag(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;

            relicTooltip?.Hide();
            if (blockData == null || relicManager == null || relicGridUI == null || relicGridUI.rootCanvas == null) return;

            isDragging = true;

            if (isPlaced)
            {
                relicManager.StartDraggingFromGrid(blockData, gridPosition);
            }
            else
            {
                relicManager.StartDraggingFromStorage(blockData);
            }

            if (storageIcon != null) storageIcon.enabled = false;
            // 유물 캔버스의 표시 순서를 유지해 드래그 아이콘이 패널 뒤로 가려지지 않게 한다.
            transform.SetParent(relicGridUI.rootCanvas.transform, false);
            transform.SetAsLastSibling();
            rectTransform.anchorMin = rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.localScale = Vector3.one;
            rectTransform.sizeDelta = Vector2.one * StorageIconSize;
            iconImage.sprite = blockData.SourceData.Image;
            iconImage.enabled = iconImage.sprite != null;
            ApplyIconSize(iconImage, StorageIconSize);
            UpdateMissingIcon(StorageIconSize);
            canvasGroup.blocksRaycasts = false;
            rectTransform.rotation = Quaternion.Euler(0, 0, blockData.RotationState * 90);
            UpdateDragPosition(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left || !isDragging) return;

            UpdateDragPosition(eventData);
        }

        private void UpdateDragPosition(PointerEventData eventData)
        {
            Canvas canvas = relicGridUI.rootCanvas.rootCanvas;
            Camera eventCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null : canvas.worldCamera != null ? canvas.worldCamera : eventData.pressEventCamera;
            if (RectTransformUtility.ScreenPointToWorldPointInRectangle(
                canvas.transform as RectTransform, eventData.position, eventCamera, out Vector3 position))
            {
                rectTransform.position = position;
            }
        }

        public void CancelDrag()
        {
            if (!isDragging) return;
            isDragging = false;
            canvasGroup.blocksRaycasts = true;
            relicGridUI?.ClearPreview();
            relicManager?.EndDrag(null);
            Destroy(gameObject);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left || !isDragging) return;

            isDragging = false;
            canvasGroup.blocksRaycasts = true;

            if (relicGridUI != null)
            {
                relicGridUI.ShowPlacementPreview(null, null);
            }

            if (relicManager == null)
            {
                Destroy(gameObject);
                return;
            }

            // 마지막 호버 셀 대신 실제 드롭 대상만 인정해 패널 밖 드롭을 취소한다.
            relicGridUI.GetDropTarget(eventData, out Vector2Int? dropGridPosition, out int? dropStorageIndex);
            relicManager.EndDrag(dropGridPosition, dropStorageIndex);

            Destroy(gameObject);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Right ||
                !isPlaced ||
                isDragging ||
                blockData == null ||
                relicManager == null)
            {
                return;
            }

            relicTooltip?.Hide();
            relicManager.UnequipFromGrid(blockData, gridPosition);
        }

        public void BuildVisualFromShape()
        {
            Image displayIcon = storageIcon != null && !isPlaced ? storageIcon : iconImage;
            if (displayIcon != null)
            {
                if (blockData != null && blockData.SourceData != null)
                {
                    displayIcon.sprite = blockData.SourceData.Image;
                    displayIcon.enabled = (displayIcon.sprite != null);

                    if (displayIcon.sprite != null)
                    {
                        ApplyIconSize(displayIcon, isPlaced ? EquippedIconSize : StorageIconSize);
                    }
                }
                else
                {
                    displayIcon.enabled = false;
                }
            }

            if (levelText != null)
            {
                levelText.gameObject.SetActive(false);
            }
            UpdateMissingIcon(isPlaced ? EquippedIconSize : StorageIconSize);
        }

        private void UpdateMissingIcon(float size)
        {
            bool missing = blockData != null && blockData.SourceData != null && blockData.SourceData.Image == null;
            if (missing && missingIconLabel == null)
            {
                // 원본 아이콘 참조가 빠진 유물을 빈 슬롯과 구분하고 드래그·툴팁을 유지한다.
                GameObject label = new GameObject("MissingIcon", typeof(RectTransform), typeof(TextMeshProUGUI));
                label.transform.SetParent(transform, false);
                missingIconLabel = label.GetComponent<TextMeshProUGUI>();
                missingIconLabel.font = TMP_Settings.defaultFontAsset;
                missingIconLabel.text = "?";
                missingIconLabel.color = Color.white;
                missingIconLabel.alignment = TextAlignmentOptions.Center;
                missingIconLabel.raycastTarget = false;
            }
            if (missingIconLabel == null) return;
            missingIconLabel.gameObject.SetActive(missing);
            missingIconLabel.rectTransform.sizeDelta = Vector2.one * size;
            missingIconLabel.fontSize = size * 0.75f;
        }

        public void BindStorageIcon(Image image)
        {
            storageIcon = image;
            if (storageIcon != null && iconImage != null) iconImage.enabled = false;
        }

        private static void ApplyIconSize(Image image, float size)
        {
            image.rectTransform.anchorMin = image.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            image.rectTransform.sizeDelta = Vector2.one * size;
            image.rectTransform.anchoredPosition = Vector2.zero;
            image.rectTransform.localScale = Vector3.one;
            image.type = Image.Type.Simple;
            image.preserveAspect = true;
            image.raycastTarget = false;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (isDragging || relicTooltip == null || blockData == null || relicManager == null) return;

            RelicBlock liveBlockData = null;

            if (isPlaced)
            {
                liveBlockData = relicManager.GetBlockAt(gridPosition.y, gridPosition.x);
            }
            else
            {
                liveBlockData = relicManager.GetBlockByID(blockData.BlockId);
            }

            if (liveBlockData != null)
            {
                relicTooltip.Show(liveBlockData);
            }
        }
        public void OnPointerExit(PointerEventData eventData)
        {
            if (relicTooltip != null)
            {
                relicTooltip.Hide();
            }
        }
    }
}
