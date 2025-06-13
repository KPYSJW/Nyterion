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

                    if (homeParent != null && homeParent != EngravingGridUI.Instance.blockStorageParent)
                    {
                        Destroy(homeParent.gameObject);
                    }

                    return;
                }
            }

            if (IsPointerOverStorageArea(eventData))
            {
                CreateNewStorageSlot();
                isPlaced = false;
                return;
            }

            Debug.Log("배치 실패 - 원래 위치로 복귀합니다.");
            transform.SetParent(homeParent, false);
            rectTransform.anchoredPosition = Vector2.zero;
            transform.SetSiblingIndex(homeSiblingIndex);
        }

        private bool IsPointerOverStorageArea(PointerEventData eventData)
        {
            RectTransform storageRect = EngravingGridUI.Instance.blockStorageParent;
            Vector2 localMousePos;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                storageRect,
                eventData.position,
                eventData.pressEventCamera,
                out localMousePos
            );

            return storageRect.rect.Contains(localMousePos);
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