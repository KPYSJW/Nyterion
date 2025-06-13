using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Collections.Generic;
using Nytherion.GamePlay.Engravings;

namespace Nytherion.UI.EngravingBoard
{
    public class EngravingBlockDraggable : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public EngravingBlock blockData;
        public Image iconImage;
        public GameObject cellPrefab;
        public float cellSize = 96f;
        private Transform homeParent;
        private int homeSiblingIndex;
        private Canvas canvas;
        private RectTransform rectTransform;
        private CanvasGroup canvasGroup;

        private Vector2 startPosition;
        private Vector2Int? lastValidGridPosition;
        private bool isPlaced = false;
        private void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
            canvas = GetComponentInParent<Canvas>();
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
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
        }
        public void OnDrag(PointerEventData eventData)
        {
            rectTransform.position = eventData.position;
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

        public void OnEndDrag(PointerEventData eventData)
        {
            canvasGroup.blocksRaycasts = true;
            EngravingGridUI.Instance.ClearPreview();

            if (lastValidGridPosition.HasValue)
            {
                bool placed = EngravingGridUI.Instance.TryPlaceBlockAt(blockData, lastValidGridPosition.Value);
                if (placed)
                {
                    transform.SetParent(EngravingGridUI.Instance.placedBlocksContainer, true);
                    Vector2 finalPosition = EngravingGridUI.Instance.GetLocalPositionFromGridCell(lastValidGridPosition.Value);
                    rectTransform.anchoredPosition = finalPosition;
                    isPlaced = true;
                    return;
                }
            }

            Debug.Log("배치 실패 - 원래 위치로 복귀합니다.");
            transform.SetParent(homeParent, true);
            transform.SetSiblingIndex(homeSiblingIndex);
        }
        public void BuildVisualFromShape()
        {
            foreach (Transform child in transform)
            {
                Destroy(child.gameObject);
            }

            GridLayoutGroup gridLayout = EngravingGridUI.Instance.gridRoot.GetComponent<GridLayoutGroup>();
            Vector2 gridCellSize = gridLayout.cellSize;
            Vector2 gridSpacing = gridLayout.spacing;

            foreach (Vector2Int offset in blockData.Shape)
            {
                GameObject cell = Instantiate(cellPrefab, transform);
                RectTransform rt = cell.GetComponent<RectTransform>();

                rt.sizeDelta = gridCellSize;

                rt.anchoredPosition = new Vector2(
                    offset.x * (gridCellSize.x + gridSpacing.x),
                    -offset.y * (gridCellSize.y + gridSpacing.y)
                );

                Image img = cell.GetComponent<Image>();
                img.color = (offset == Vector2Int.zero) ? Color.red : Color.white;
            }
        }
    }
}