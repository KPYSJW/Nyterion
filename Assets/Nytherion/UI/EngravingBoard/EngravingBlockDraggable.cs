using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using Nytherion.GamePlay.Engravings;

namespace Nytherion.UI.EngravingBoard
{
    public class EngravingBlockDraggable : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public EngravingBlock blockData;
        public Image iconImage;
        public GameObject cellPrefab;
        private Transform homeParent;
        private int homeSiblingIndex;
        private Canvas canvas;
        private RectTransform rectTransform;
        private CanvasGroup canvasGroup;

        private PlayerAction playerAction;

        private Vector2Int? lastValidGridPosition;
        private bool isPlaced = false;
        private bool isBeingDragged = false;
        private void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
            canvas = GetComponentInParent<Canvas>();
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }

            playerAction = new PlayerAction();
        }

        private void OnEnable()
        {
            playerAction.EngravingUI.Enable();
            playerAction.EngravingUI.RotateBlock.performed += OnRotate;
        }
        private void OnDisable()
        {
            playerAction.EngravingUI.RotateBlock.performed -= OnRotate;
            playerAction.EngravingUI.Disable();
        }
        private void OnRotate(InputAction.CallbackContext context)
        {
            if (isBeingDragged)
            {
                RotateAndRebuildVisuals();
            }
        }
        public void OnBeginDrag(PointerEventData eventData)
        {
            homeParent = transform.parent;
            homeSiblingIndex = transform.GetSiblingIndex();

            if (isPlaced)
            {
                EngravingGridUI.Instance.RemoveBlockFromGrid(blockData.BlockId);
                isPlaced = false;
            }

            transform.SetParent(canvas.transform, true);
            canvasGroup.blocksRaycasts = false;
            lastValidGridPosition = null;
            isBeingDragged = true;
        }
        public void OnDrag(PointerEventData eventData)
        {
            rectTransform.position = eventData.position;
        }
        private void Update()
        {
            if (!isBeingDragged) return;

            UpdatePlacementPreview();
        }
        public void OnEndDrag(PointerEventData eventData)
        {
            isBeingDragged = false;

            canvasGroup.blocksRaycasts = true;
            EngravingGridUI.Instance.ClearPreview();

            if (lastValidGridPosition.HasValue && EngravingGridUI.Instance.TryPlaceBlockAt(blockData, lastValidGridPosition.Value))
            {
                transform.SetParent(EngravingGridUI.Instance.placedBlocksContainer, true);
                Vector2 finalPosition = EngravingGridUI.Instance.GetLocalPositionFromGridCell(lastValidGridPosition.Value);
                rectTransform.anchoredPosition = finalPosition;
                isPlaced = true;

                if (homeParent != null && homeParent.parent == EngravingGridUI.Instance.blockStorageParent)
                {
                    Destroy(homeParent.gameObject);
                }
                return;
            }

            if (homeParent != null && homeParent.parent == EngravingGridUI.Instance.blockStorageParent)
            {
                transform.SetParent(homeParent, false);
                rectTransform.anchoredPosition = Vector2.zero;
                isPlaced = false;
            }
            else
            {
                CreateNewStorageSlot();
                isPlaced = false;
            }
        }
        private void UpdatePlacementPreview()
        {
            Vector2Int? gridPos = EngravingGridUI.Instance.CurrentGridPos;

            if (gridPos.HasValue && EngravingGridUI.Instance.CanPlacePreview(blockData, gridPos.Value))
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
        public void BuildVisualFromShape()
        {
            foreach (Transform child in transform)
            {
                Destroy(child.gameObject);
            }

            GridLayoutGroup gridLayout = EngravingGridUI.Instance.gridRoot.GetComponent<GridLayoutGroup>();
            Vector2 cellSize = gridLayout.cellSize;
            Vector2 spacing = gridLayout.spacing;

            foreach (Vector2Int offset in blockData.Shape)
            {
                GameObject cell = Instantiate(cellPrefab, transform);
                RectTransform rt = cell.GetComponent<RectTransform>();
                rt.sizeDelta = cellSize;
                rt.anchoredPosition = new Vector2(offset.x * (cellSize.x + spacing.x), -offset.y * (cellSize.y + spacing.y));
                Image img = cell.GetComponent<Image>();
                img.color = (offset == Vector2Int.zero) ? Color.red : Color.white;
            }
        }
        public void RotateAndRebuildVisuals()
        {
            blockData.Rotate();
            BuildVisualFromShape();

            Debug.Log("블록이 회전되었고, 중심점이 재조정되었습니다.");
        }

        private void CreateNewStorageSlot()
        {
            Transform storageParent = EngravingGridUI.Instance.blockStorageParent;
            GameObject slotObj = Object.Instantiate(EngravingGridUI.Instance.storageSlotPrefab, storageParent);

            transform.SetParent(slotObj.transform, false);
            RectTransform rt = GetComponent<RectTransform>();
            rt.anchoredPosition = Vector2.zero;

            homeParent = slotObj.transform;
            homeSiblingIndex = 0;

            Debug.Log("블럭이 보관소 슬롯으로 복귀되었습니다.");
        }


    }
}