using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using Nytherion.GamePlay.Engravings;
using Nytherion.Core;

namespace Nytherion.UI.EngravingBoard
{
    public class EngravingBlockDraggable : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public EngravingBlock blockData;
        public GameObject cellPrefab;
        private Transform originalParent;
        private Canvas canvas;
        private RectTransform rectTransform;
        private CanvasGroup canvasGroup;
        private PlayerAction playerAction;

        private Vector2Int? lastValidGridPosition;
        public bool isPlaced = false;
        private bool isBeingDragged = false;

        private void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
            canvas = GetComponentInParent<Canvas>().rootCanvas;
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();

            playerAction = new PlayerAction();
        }

        private void OnEnable()
        {
            playerAction.EngravingUI.Enable();
            playerAction.EngravingUI.RotateBlock.performed += OnRotate;
        }

        private void OnDisable()
        {
            if (playerAction != null) playerAction.EngravingUI.Disable();
        }

        private void OnRotate(InputAction.CallbackContext context)
        {
            if (isBeingDragged) RotateAndRebuildVisuals();
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (blockData == null) return;
            isBeingDragged = true;

            if (isPlaced)
            {
                EngravingManager.Instance.PickUpFromGrid(blockData);
            }
            else
            {
                EngravingManager.Instance.PickUpFromStorage(blockData);
            }

            transform.SetParent(EngravingGridUI.Instance.rootCanvas.transform, true);
            canvasGroup.blocksRaycasts = false;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (isBeingDragged) rectTransform.position = eventData.position;
        }

        private void Update()
        {
            if (!isBeingDragged) return;

            // 배치 가능 여부 미리보기 표시
            Vector2Int? gridPos = EngravingGridUI.Instance.CurrentGridPos;
            if (gridPos.HasValue && EngravingManager.Instance.CanPlaceBlock(blockData, gridPos.Value.y, gridPos.Value.x))
            {
                EngravingGridUI.Instance.ShowPlacementPreview(blockData, gridPos.Value);
                lastValidGridPosition = gridPos.Value;
            }
            else
            {
                EngravingGridUI.Instance.ClearPreview();
                lastValidGridPosition = null;
            }
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!isBeingDragged) return;
            isBeingDragged = false;

            canvasGroup.blocksRaycasts = true;
            EngravingGridUI.Instance.ClearPreview();

            bool placedSuccessfully = false;
            if (lastValidGridPosition.HasValue)
            {
                if (EngravingManager.Instance.TryPlaceBlock(blockData, lastValidGridPosition.Value))
                {
                    placedSuccessfully = true;
                }
            }

            if (!placedSuccessfully)
            {
                EngravingManager.Instance.ReturnToStorage(blockData);
            }

            Destroy(gameObject);
        }

        private void CreateNewStorageSlot()
        {
            GameObject slotObj = Instantiate(EngravingGridUI.Instance.storageSlotPrefab, EngravingGridUI.Instance.blockStorageParent);
            transform.SetParent(slotObj.transform, false);

            // 시각적 중심 보정
            var gridLayout = EngravingGridUI.Instance.gridRoot.GetComponent<GridLayoutGroup>();
            Vector2 cellSize = gridLayout.cellSize;
            Vector2 spacing = gridLayout.spacing;
            Vector2 visualCenterOffset = blockData.GetVisualCenterPixelOffset(cellSize, spacing);
            rectTransform.anchoredPosition = -visualCenterOffset;
        }
        public void BuildVisualFromShape()
        {
            foreach (Transform child in transform) Destroy(child.gameObject);

            if (EngravingGridUI.Instance == null || EngravingGridUI.Instance.gridRoot == null) return;

            var gridLayout = EngravingGridUI.Instance.gridRoot.GetComponent<GridLayoutGroup>();
            Vector2 cellSize = gridLayout.cellSize;
            Vector2 spacing = gridLayout.spacing;

            foreach (Vector2Int offset in blockData.Shape)
            {
                GameObject cell = Instantiate(cellPrefab, transform);
                var rt = cell.GetComponent<RectTransform>();
                rt.sizeDelta = cellSize;
                rt.anchoredPosition = new Vector2(offset.x * (cellSize.x + spacing.x), -offset.y * (cellSize.y + spacing.y));
                var img = cell.GetComponent<Image>();
                img.color = (offset == Vector2Int.zero) ? Color.red : Color.white;
            }
        }

        public void RotateAndRebuildVisuals()
        {
            blockData.Rotate();
            BuildVisualFromShape();
        }
    }
}