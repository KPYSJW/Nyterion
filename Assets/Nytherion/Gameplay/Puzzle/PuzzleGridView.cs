using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using Nytherion.Core.Enums;
using Nytherion.Core.Managers;
using Nytherion.Core.Data;

namespace Nytherion.GamePlay.Puzzle
{
    public class PuzzleGridView : MonoBehaviour
    {
        [Header("Grid Settings")]
        [SerializeField] private GridLayoutGroup gridLayout;
        [SerializeField] private RectTransform gridContainer;

        [Header("Prefabs")]
        [SerializeField] private GameObject tilePrefab;

        private PuzzleManager puzzleManager;
        private PuzzleTileController[,] tileGrid;
        private PuzzleTileView[,] tileViewGrid;
        private PuzzleLevelData currentLevelData;

        private List<Vector2Int> currentPath = new List<Vector2Int>();
        private PuzzleColor currentPathColor = PuzzleColor.Red;
        private bool isDrawingPath = false;

        [Inject]
        public void Construct(PuzzleManager puzzleManager)
        {
            this.puzzleManager = puzzleManager;
        }

        private void Start()
        {
            if (puzzleManager != null)
            {
                puzzleManager.OnPuzzleStateChanged += OnPuzzleStateChanged;
                puzzleManager.OnPathCompleted += OnPathCompleted;
                puzzleManager.OnPathCleared += OnPathCleared;
            }
        }

        private void OnDestroy()
        {
            if (puzzleManager != null)
            {
                puzzleManager.OnPuzzleStateChanged -= OnPuzzleStateChanged;
                puzzleManager.OnPathCompleted -= OnPathCompleted;
                puzzleManager.OnPathCleared -= OnPathCleared;
            }
        }

        public void InitializeGrid(PuzzleLevelData levelData)
        {
            currentLevelData = levelData;
            ClearGrid();
            CreateGrid();
            SetupSensors();
            UpdateAllTileVisuals();
        }

        private void ClearGrid()
        {
            if (tileViewGrid != null)
            {
                for (int x = 0; x < tileViewGrid.GetLength(0); x++)
                {
                    for (int y = 0; y < tileViewGrid.GetLength(1); y++)
                    {
                        if (tileViewGrid[x, y] != null)
                        {
                            DestroyImmediate(tileViewGrid[x, y].gameObject);
                        }
                    }
                }
            }

            foreach (Transform child in gridContainer)
            {
                DestroyImmediate(child.gameObject);
            }
        }

        private void CreateGrid()
        {
            int width = currentLevelData.gridWidth;
            int height = currentLevelData.gridHeight;

            tileGrid = new PuzzleTileController[width, height];
            tileViewGrid = new PuzzleTileView[width, height];

            gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            gridLayout.constraintCount = width;

            for (int y = height - 1; y >= 0; y--)
            {
                for (int x = 0; x < width; x++)
                {
                    CreateTile(x, y);
                }
            }
        }

        private void CreateTile(int x, int y)
        {
            GameObject tileObject = Instantiate(tilePrefab, gridContainer);
            PuzzleTileView tileView = tileObject.GetComponent<PuzzleTileView>();

            if (tileView == null)
            {
                tileView = tileObject.AddComponent<PuzzleTileView>();
            }

            PuzzleTileController tileController = new PuzzleTileController();
            tileController.Initialize(x, y);

            tileGrid[x, y] = tileController;
            tileViewGrid[x, y] = tileView;

            tileView.Initialize(tileController);
        }

        private void SetupSensors()
        {
            foreach (var sensorPair in currentLevelData.sensorPairs)
            {
                Vector2Int start = sensorPair.startPosition;
                Vector2Int end = sensorPair.endPosition;

                if (IsValidPosition(start))
                {
                    tileGrid[start.x, start.y].SetAsSensor(sensorPair.color);
                }

                if (IsValidPosition(end))
                {
                    tileGrid[end.x, end.y].SetAsSensor(sensorPair.color);
                }
            }
        }

        private bool IsValidPosition(Vector2Int pos)
        {
            return pos.x >= 0 && pos.x < currentLevelData.gridWidth &&
                   pos.y >= 0 && pos.y < currentLevelData.gridHeight;
        }

        private void UpdateAllTileVisuals()
        {
            if (tileViewGrid == null) return;

            for (int x = 0; x < tileViewGrid.GetLength(0); x++)
            {
                for (int y = 0; y < tileViewGrid.GetLength(1); y++)
                {
                    tileViewGrid[x, y]?.UpdateVisuals();
                }
            }
        }

        public void StartPathDrawing(Vector2Int startPos, PuzzleColor color)
        {
            if (!IsValidPosition(startPos) || !tileGrid[startPos.x, startPos.y].IsSensor)
                return;

            currentPath.Clear();
            currentPath.Add(startPos);
            currentPathColor = color;
            isDrawingPath = true;

            Debug.Log($"[PuzzleGridView] Started drawing path from {startPos} with color {color}");
        }

        public void AddToPath(Vector2Int pos)
        {
            if (!isDrawingPath || !IsValidPosition(pos))
                return;

            if (currentPath.Contains(pos))
                return;

            currentPath.Add(pos);
            tileGrid[pos.x, pos.y].SetAsPath(currentPathColor);
            tileViewGrid[pos.x, pos.y].UpdateVisuals();
        }

        public void CompletePath()
        {
            if (!isDrawingPath || currentPath.Count < 2)
            {
                CancelPath();
                return;
            }

            bool success = puzzleManager.TryCompletePath(currentPathColor, new List<Vector2Int>(currentPath));

            if (!success)
            {
                CancelPath();
            }

            isDrawingPath = false;
        }

        public void CancelPath()
        {
            if (currentPath.Count > 0)
            {
                foreach (Vector2Int pos in currentPath)
                {
                    if (IsValidPosition(pos))
                    {
                        tileGrid[pos.x, pos.y].ClearPath();
                        tileViewGrid[pos.x, pos.y].UpdateVisuals();
                    }
                }
            }

            currentPath.Clear();
            isDrawingPath = false;
        }

        private void OnPuzzleStateChanged(PuzzleState newState)
        {
            Debug.Log($"[PuzzleGridView] Puzzle state changed to: {newState}");

            if (newState == PuzzleState.Failed || newState == PuzzleState.Completed)
            {
                CancelPath();
            }
        }

        private void OnPathCompleted(PuzzleColor color, List<Vector2Int> pathPositions)
        {
            Debug.Log($"[PuzzleGridView] Path completed for color {color}");
        }

        private void OnPathCleared(PuzzleColor color)
        {
            Debug.Log($"[PuzzleGridView] Path cleared for color {color}");
            UpdateAllTileVisuals();
        }
    }
}