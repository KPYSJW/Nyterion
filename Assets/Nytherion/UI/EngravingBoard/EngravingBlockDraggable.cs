using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Nytherion.GamePlay.Engravings;
using Nytherion.Core.Managers;
using TMPro;

namespace Nytherion.UI.EngravingBoard
{
    public class EngravingBlockDraggable : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerEnterHandler, IPointerExitHandler
    {
        public EngravingBlock blockData;

        public bool isPlaced = false;
        public Vector2Int gridPosition;

        [SerializeField] private Image iconImage;

        private CanvasGroup canvasGroup;
        private RectTransform rectTransform;
        [SerializeField] private TextMeshProUGUI levelText;
        private bool isDragging = false;
        private void Awake()
        {
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
            rectTransform = GetComponent<RectTransform>();
            if (levelText == null)
            {
                levelText = GetComponentInChildren<TextMeshProUGUI>();
            }
        }
        private void OnEnable()
        {
            if (InputManager.Instance != null)
            {
                InputManager.Instance.onEngravingRotate += HandleRotation;
            }
        }

        private void OnDisable()
        {
            if (InputManager.Instance != null)
            {
                InputManager.Instance.onEngravingRotate -= HandleRotation;
            }
        }
        private void Update()
        {
            if (isDragging)
            {
                if (EngravingGridUI.Instance != null)
                {
                    EngravingGridUI.Instance.ShowPlacementPreview(blockData, EngravingGridUI.Instance.CurrentGridPos);
                }

                if (levelText != null)
                {
                    levelText.transform.rotation = Quaternion.identity;
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
                if (EngravingManager.Instance != null)
                {
                    EngravingManager.Instance.RotateDraggedBlock();
                    rectTransform.Rotate(0, 0, 90);
                }
                if (EngravingGridUI.Instance != null)
                {
                    EngravingGridUI.Instance.ShowPlacementPreview(blockData, EngravingGridUI.Instance.CurrentGridPos);
                }
            }
        }
        public void OnBeginDrag(PointerEventData eventData)
        {
            EngravingTooltip.Instance?.Hide();
            if (blockData == null || EngravingManager.Instance == null) return;

            isDragging = true;

            if (isPlaced)
            {
                EngravingManager.Instance.StartDraggingFromGrid(blockData, gridPosition);
            }
            else
            {
                EngravingManager.Instance.StartDraggingFromStorage(blockData);
            }

            transform.SetParent(EngravingGridUI.Instance.rootCanvas.transform, true);
            canvasGroup.blocksRaycasts = false;
            rectTransform.rotation = Quaternion.Euler(0, 0, blockData.RotationState * 90);
        }

        public void OnDrag(PointerEventData eventData)
        {
            rectTransform.position = eventData.position;
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            isDragging = false;
            canvasGroup.blocksRaycasts = true;

            if (EngravingGridUI.Instance != null)
            {
                EngravingGridUI.Instance.ShowPlacementPreview(null, null);
            }

            if (EngravingManager.Instance == null)
            {
                Destroy(gameObject);
                return;
            }

            Vector2Int? dropGridPosition = EngravingGridUI.Instance.CurrentGridPos;

            EngravingManager.Instance.EndDrag(dropGridPosition);

            Destroy(gameObject);
        }

        public void BuildVisualFromShape()
        {
            if (iconImage != null)
            {
                if (blockData != null && blockData.SourceData != null)
                {
                    iconImage.sprite = blockData.SourceData.Image;
                    iconImage.enabled = (iconImage.sprite != null);
                }
                else
                {
                    iconImage.enabled = false;
                }
            }

            if (levelText != null)
            {
                if (blockData != null)
                {
                    levelText.text = blockData.SourceData.level.ToString();
                    levelText.gameObject.SetActive(true);
                }
                else
                {
                    levelText.gameObject.SetActive(false);
                }
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (isDragging || EngravingTooltip.Instance == null) return;

            EngravingBlock liveBlockData = null;

            if (isPlaced)
            {
                liveBlockData = EngravingManager.Instance.GetBlockAt(gridPosition.y, gridPosition.x);
            }
            else
            {
                liveBlockData = EngravingManager.Instance.GetBlockByID(blockData.BlockId);
            }

            if (liveBlockData != null)
            {
                EngravingTooltip.Instance.Show(liveBlockData);
            }
        }
        public void OnPointerExit(PointerEventData eventData)
        {
            if (EngravingTooltip.Instance != null)
            {
                EngravingTooltip.Instance.Hide();
            }
        }
    }
}