using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Nytherion.GamePlay.Engravings;
using Nytherion.Core;
using TMPro;

namespace Nytherion.UI.EngravingBoard
{
    public class EngravingBlockDraggable : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public EngravingBlock blockData;
        public GameObject cellPrefab;

        public bool isPlaced = false;
        public Vector2Int gridPosition;

        private CanvasGroup canvasGroup;
        private RectTransform rectTransform;
        private TextMeshProUGUI levelText;
        private void Awake()
        {
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
            rectTransform = GetComponent<RectTransform>();
            levelText = transform.GetComponentInChildren<TextMeshProUGUI>();
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (blockData == null || EngravingManager.Instance == null) return;

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
        }

        public void OnDrag(PointerEventData eventData)
        {
            rectTransform.position = eventData.position;
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            canvasGroup.blocksRaycasts = true;
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
            foreach (Transform child in transform) Destroy(child.gameObject);
            if (EngravingGridUI.Instance?.gridRoot == null) return;

            var gridLayout = EngravingGridUI.Instance.gridRoot.GetComponent<GridLayoutGroup>();
            GameObject cell = Instantiate(cellPrefab, transform);
            var rt = cell.GetComponent<RectTransform>();
            rt.sizeDelta = gridLayout.cellSize;
            rt.anchoredPosition = Vector2.zero;

            if (levelText != null && blockData != null)
            {
                levelText.text = blockData.SourceData.level.ToString();
            }
        }
    }
}