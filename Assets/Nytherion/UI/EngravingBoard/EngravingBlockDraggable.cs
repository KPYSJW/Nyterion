using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Nytherion.GamePlay.Engravings;
using Nytherion.Core.Managers;
using TMPro;
using Zenject;

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
        
        private InputManager _inputManager;
        private EngravingManager _engravingManager;
        private EngravingGridUI _engravingGridUI;
        private EngravingTooltip _engravingTooltip;
        
        [Inject]
        public void Construct(InputManager inputManager, EngravingManager engravingManager, EngravingGridUI engravingGridUI, EngravingTooltip engravingTooltip)
        {
            _inputManager = inputManager;
            _engravingManager = engravingManager;
            _engravingGridUI = engravingGridUI;
            _engravingTooltip = engravingTooltip;
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
        }
        private void OnEnable()
        {
            if (_inputManager != null)
            {
                _inputManager.onEngravingRotate += HandleRotation;
            }
        }

        private void OnDisable()
        {
            if (_inputManager != null)
            {
                _inputManager.onEngravingRotate -= HandleRotation;
            }
        }
        private void Update()
        {
            if (isDragging)
            {
                if (_engravingGridUI != null)
                {
                    _engravingGridUI.ShowPlacementPreview(blockData, _engravingGridUI.CurrentGridPos);
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
                if (_engravingManager != null)
                {
                    _engravingManager.RotateDraggedBlock();
                    rectTransform.Rotate(0, 0, 90);
                }
                if (_engravingGridUI != null)
                {
                    _engravingGridUI.ShowPlacementPreview(blockData, _engravingGridUI.CurrentGridPos);
                }
            }
        }
        public void OnBeginDrag(PointerEventData eventData)
        {
            _engravingTooltip?.Hide();
            if (blockData == null || _engravingManager == null) return;

            isDragging = true;

            if (isPlaced)
            {
                _engravingManager.StartDraggingFromGrid(blockData, gridPosition);
            }
            else
            {
                _engravingManager.StartDraggingFromStorage(blockData);
            }

            transform.SetParent(_engravingGridUI.rootCanvas.transform, true);
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

            if (_engravingGridUI != null)
            {
                _engravingGridUI.ShowPlacementPreview(null, null);
            }

            if (_engravingManager == null)
            {
                Destroy(gameObject);
                return;
            }

            Vector2Int? dropGridPosition = _engravingGridUI.CurrentGridPos;

            _engravingManager.EndDrag(dropGridPosition);

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
            if (isDragging || _engravingTooltip == null) return;

            EngravingBlock liveBlockData = null;

            if (isPlaced)
            {
                liveBlockData = _engravingManager.GetBlockAt(gridPosition.y, gridPosition.x);
            }
            else
            {
                liveBlockData = _engravingManager.GetBlockByID(blockData.BlockId);
            }

            if (liveBlockData != null)
            {
                _engravingTooltip.Show(liveBlockData);
            }
        }
        public void OnPointerExit(PointerEventData eventData)
        {
            if (_engravingTooltip != null)
            {
                _engravingTooltip.Hide();
            }
        }
    }
}