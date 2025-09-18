using UnityEngine;
using UnityEngine.EventSystems;
using VContainer;
using Nytherion.Core.Enums;

namespace Nytherion.GamePlay.Puzzle
{
    public class PuzzleInputHandler : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler
    {
        private PuzzleGridView puzzleGridView;
        private Camera currentCamera;
        private bool isDragging = false;
        private PuzzleColor selectedColor = PuzzleColor.Red;

        [Inject]
        public void Construct(PuzzleGridView puzzleGridView)
        {
            this.puzzleGridView = puzzleGridView;
        }

        private void Start()
        {
            currentCamera = Camera.main;
            if (currentCamera == null)
            {
                currentCamera = FindObjectOfType<Camera>();
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            Vector2Int? gridPos = GetGridPositionFromScreenPoint(eventData.position);
            if (gridPos.HasValue)
            {
                puzzleGridView?.StartPathDrawing(gridPos.Value, selectedColor);
                isDragging = true;
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (isDragging)
            {
                Vector2Int? gridPos = GetGridPositionFromScreenPoint(eventData.position);
                if (gridPos.HasValue)
                {
                    puzzleGridView?.AddToPath(gridPos.Value);
                }
            }
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (isDragging)
            {
                puzzleGridView?.CompletePath();
                isDragging = false;
            }
        }

        private Vector2Int? GetGridPositionFromScreenPoint(Vector2 screenPoint)
        {
            if (currentCamera == null)
                return null;

            Ray ray = currentCamera.ScreenPointToRay(screenPoint);
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                PuzzleTileView tileView = hit.collider.GetComponent<PuzzleTileView>();
                if (tileView != null)
                {
                    return tileView.GridPosition;
                }
            }

            return null;
        }

        public void SetSelectedColor(PuzzleColor color)
        {
            selectedColor = color;
        }
    }
}