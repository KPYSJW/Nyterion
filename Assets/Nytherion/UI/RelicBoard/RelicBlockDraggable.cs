using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Nytherion.GamePlay.Relics;
using Nytherion.Core.Managers;
using TMPro;
using VContainer;

namespace Nytherion.UI.RelicBoard
{
    public class RelicBlockDraggable : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerEnterHandler, IPointerExitHandler
    {
        public RelicBlock blockData;

        public bool isPlaced = false;
        public Vector2Int gridPosition;

        [SerializeField] private Image iconImage;

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
            relicTooltip?.Hide();
            if (blockData == null || relicManager == null) return;

            isDragging = true;

            if (isPlaced)
            {
                relicManager.StartDraggingFromGrid(blockData, gridPosition);
            }
            else
            {
                relicManager.StartDraggingFromStorage(blockData);
            }

            transform.SetParent(relicGridUI.rootCanvas.transform, true);
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

            if (relicGridUI != null)
            {
                relicGridUI.ShowPlacementPreview(null, null);
            }

            if (relicManager == null)
            {
                Destroy(gameObject);
                return;
            }

            Vector2Int? dropGridPosition = relicGridUI.CurrentGridPos;

            relicManager.EndDrag(dropGridPosition);

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
                levelText.gameObject.SetActive(false);
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (isDragging || relicTooltip == null) return;

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
