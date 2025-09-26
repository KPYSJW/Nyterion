using System.Collections.Generic;
using UnityEngine;
using VContainer;
using Nytherion.Core.Enums;
using Nytherion.Core.Managers;
using Nytherion.Data.ScriptableObjects.Puzzles;

namespace Nytherion.GamePlay.Puzzles
{
    public class FlowPuzzleManager : MonoBehaviour, IFlowPuzzleManager
    {
        [Header("퍼즐 설정")]
        [SerializeField] private PuzzleData currentPuzzleData;
        [SerializeField] private FlowTileView tilePrefab;
        [SerializeField] private Transform gridParent;
        [SerializeField] private float tileSpacing = 1.1f;

        private FlowTileController[,] _tileGrid;
        private FlowTileView[,] _tileViews;
        private PuzzleManager _puzzleManager;
        private EventManager _eventManager;

        // 드래그 상태 관리
        private bool _isDragging = false;
        private BlockColor _currentDragColor;
        private FlowTileController _dragStartTile;
        private List<FlowTileController> _currentPath = new List<FlowTileController>();

        [Inject]
        public void Construct(PuzzleManager puzzleManager, EventManager eventManager)
        {
            _puzzleManager = puzzleManager;
            _eventManager = eventManager;
        }

        public void InitializePuzzle(PuzzleData puzzleData)
        {
            currentPuzzleData = puzzleData;
            CreateGrid();
            SetupSensors();

            // 퍼즐 매니저에 등록
            if (_puzzleManager != null)
            {
                _puzzleManager.RegisterPuzzle(puzzleData.puzzleId, puzzleData.puzzleType, puzzleData.maxAttempts);
            }
        }

        private void CreateGrid()
        {
            if (currentPuzzleData == null) return;

            int width = currentPuzzleData.gridWidth;
            int height = currentPuzzleData.gridHeight;

            _tileGrid = new FlowTileController[width, height];
            _tileViews = new FlowTileView[width, height];

            // 기존 타일들 제거
            if (gridParent != null)
            {
                foreach (Transform child in gridParent)
                {
                    if (Application.isPlaying)
                        Destroy(child.gameObject);
                    else
                        DestroyImmediate(child.gameObject);
                }
            }

            // 새 타일들 생성
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    // 컨트롤러 생성
                    FlowTileController controller = new FlowTileController();
                    controller.Initialize(x, y);
                    _tileGrid[x, y] = controller;

                    // 뷰 생성
                    if (tilePrefab != null && gridParent != null)
                    {
                        FlowTileView tileView = Instantiate(tilePrefab, gridParent);
                        tileView.transform.localPosition = new Vector3(x * tileSpacing, -y * tileSpacing, 0);
                        tileView.Initialize(controller, this);
                        _tileViews[x, y] = tileView;
                    }
                }
            }
        }

        private void SetupSensors()
        {
            if (currentPuzzleData == null || currentPuzzleData.sensorPairs == null) return;

            foreach (var sensorPair in currentPuzzleData.sensorPairs)
            {
                // 시작 센서 설정
                if (IsValidPosition(sensorPair.startPosition.x, sensorPair.startPosition.y))
                {
                    var startTile = _tileGrid[sensorPair.startPosition.x, sensorPair.startPosition.y];
                    startTile.SetAsSensor(sensorPair.color);
                    _tileViews[sensorPair.startPosition.x, sensorPair.startPosition.y]?.UpdateVisuals();
                }

                // 끝 센서 설정
                if (IsValidPosition(sensorPair.endPosition.x, sensorPair.endPosition.y))
                {
                    var endTile = _tileGrid[sensorPair.endPosition.x, sensorPair.endPosition.y];
                    endTile.SetAsSensor(sensorPair.color);
                    _tileViews[sensorPair.endPosition.x, sensorPair.endPosition.y]?.UpdateVisuals();
                }
            }
        }

        private bool IsValidPosition(int x, int y)
        {
            return x >= 0 && x < currentPuzzleData.gridWidth &&
                   y >= 0 && y < currentPuzzleData.gridHeight;
        }

        // IFlowPuzzleManager 구현
        public void OnTileMouseDown(FlowTileController clickedTile)
        {
            if (clickedTile == null || _isDragging) return;

            // 센서 타일에서 시작해야 함
            if (clickedTile.currentType == TileType.Sensor)
            {
                _isDragging = true;
                _currentDragColor = clickedTile.pathColor;
                _dragStartTile = clickedTile;
                _currentPath.Clear();
                _currentPath.Add(clickedTile);

                Debug.Log($"[FlowPuzzleManager] 드래그 시작: 색상 {_currentDragColor}, 위치 ({clickedTile.gridX}, {clickedTile.gridY})");
            }
        }

        public void OnTileMouseEnter(FlowTileController enteredTile)
        {
            if (!_isDragging || enteredTile == null) return;

            // 인접한 타일인지 확인
            if (_currentPath.Count > 0)
            {
                FlowTileController lastTile = _currentPath[_currentPath.Count - 1];
                if (!lastTile.IsAdjacent(enteredTile)) return;
            }

            // 이미 경로에 있는 타일인지 확인 (뒤로 가는 경우 허용)
            int existingIndex = _currentPath.IndexOf(enteredTile);
            if (existingIndex >= 0)
            {
                // 뒤로 가는 경우: 해당 지점까지의 경로를 제거
                for (int i = _currentPath.Count - 1; i > existingIndex; i--)
                {
                    var tileToRemove = _currentPath[i];
                    if (tileToRemove.currentType == TileType.Path)
                    {
                        tileToRemove.ClearPath();
                        int x = tileToRemove.gridX;
                        int y = tileToRemove.gridY;
                        if (IsValidPosition(x, y))
                        {
                            _tileViews[x, y]?.UpdateVisuals();
                        }
                    }
                    _currentPath.RemoveAt(i);
                }
                return;
            }

            // 새로운 타일 추가
            if (enteredTile.currentType == TileType.Empty)
            {
                enteredTile.SetAsPath(_currentDragColor);
                _currentPath.Add(enteredTile);
                _tileViews[enteredTile.gridX, enteredTile.gridY]?.UpdateVisuals();
            }
            else if (enteredTile.currentType == TileType.Sensor &&
                     enteredTile.pathColor == _currentDragColor &&
                     enteredTile != _dragStartTile)
            {
                // 같은 색상의 다른 센서에 도달 - 퍼즐 완료 가능성 체크
                _currentPath.Add(enteredTile);
                CheckPuzzleCompletion();
            }
        }

        public void OnTileMouseUp(FlowTileController releasedTile)
        {
            if (!_isDragging) return;

            _isDragging = false;

            // 유효한 연결이 아니라면 경로 제거
            if (!IsValidConnection())
            {
                ClearCurrentPath();
            }

            Debug.Log($"[FlowPuzzleManager] 드래그 종료");
            _currentPath.Clear();
            _dragStartTile = null;
        }

        private bool IsValidConnection()
        {
            if (_currentPath.Count < 2) return false;

            FlowTileController start = _currentPath[0];
            FlowTileController end = _currentPath[_currentPath.Count - 1];

            // 시작과 끝이 모두 같은 색상의 센서여야 함
            return start.currentType == TileType.Sensor &&
                   end.currentType == TileType.Sensor &&
                   start.pathColor == end.pathColor &&
                   start != end;
        }

        private void ClearCurrentPath()
        {
            foreach (var tile in _currentPath)
            {
                if (tile.currentType == TileType.Path)
                {
                    tile.ClearPath();
                    _tileViews[tile.gridX, tile.gridY]?.UpdateVisuals();
                }
            }
        }

        private void CheckPuzzleCompletion()
        {
            if (currentPuzzleData == null) return;

            bool allConnected = true;
            foreach (var sensorPair in currentPuzzleData.sensorPairs)
            {
                if (!IsColorConnected(sensorPair.color))
                {
                    allConnected = false;
                    break;
                }
            }

            if (allConnected)
            {
                CompletePuzzle();
            }
        }

        private bool IsColorConnected(BlockColor color)
        {
            // 해당 색상의 센서 페어가 연결되었는지 확인하는 로직
            var sensorPair = currentPuzzleData.GetSensorPairByColor(color);
            if (sensorPair == null) return false;

            var startTile = _tileGrid[sensorPair.startPosition.x, sensorPair.startPosition.y];
            var endTile = _tileGrid[sensorPair.endPosition.x, sensorPair.endPosition.y];

            // 간단한 연결 확인 (실제로는 더 복잡한 경로 추적이 필요할 수 있음)
            return HasPathBetween(startTile, endTile, color);
        }

        private bool HasPathBetween(FlowTileController start, FlowTileController end, BlockColor color)
        {
            // 너비 우선 탐색을 통한 경로 확인
            Queue<FlowTileController> queue = new Queue<FlowTileController>();
            HashSet<FlowTileController> visited = new HashSet<FlowTileController>();

            queue.Enqueue(start);
            visited.Add(start);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();

                if (current == end) return true;

                // 인접한 타일들 확인
                var adjacentTiles = GetAdjacentTiles(current);
                foreach (var adjacent in adjacentTiles)
                {
                    if (visited.Contains(adjacent)) continue;

                    if ((adjacent.currentType == TileType.Path || adjacent.currentType == TileType.Sensor) &&
                        adjacent.pathColor == color)
                    {
                        queue.Enqueue(adjacent);
                        visited.Add(adjacent);
                    }
                }
            }

            return false;
        }

        private List<FlowTileController> GetAdjacentTiles(FlowTileController tile)
        {
            List<FlowTileController> adjacent = new List<FlowTileController>();
            int x = tile.gridX;
            int y = tile.gridY;

            // 상하좌우 확인
            int[] dx = {0, 0, -1, 1};
            int[] dy = {-1, 1, 0, 0};

            for (int i = 0; i < 4; i++)
            {
                int newX = x + dx[i];
                int newY = y + dy[i];

                if (IsValidPosition(newX, newY))
                {
                    adjacent.Add(_tileGrid[newX, newY]);
                }
            }

            return adjacent;
        }

        private void CompletePuzzle()
        {
            if (_puzzleManager != null)
            {
                _puzzleManager.CompletePuzzle(currentPuzzleData.puzzleId);
            }

            GiveRewards();
        }

        private void GiveRewards()
        {
            if (currentPuzzleData == null) return;

            _eventManager?.TriggerEvent("PuzzleRewardGiven", new Dictionary<string, object>
            {
                {"puzzleId", currentPuzzleData.puzzleId},
                {"goldReward", currentPuzzleData.goldReward},
                {"expReward", currentPuzzleData.expReward}
            });
        }

        /// <summary>
        /// 퍼즐 리셋 (디버그용)
        /// </summary>
        public void ResetPuzzle()
        {
            if (currentPuzzleData == null) return;

            // 모든 경로 타일 클리어
            for (int x = 0; x < currentPuzzleData.gridWidth; x++)
            {
                for (int y = 0; y < currentPuzzleData.gridHeight; y++)
                {
                    if (IsValidPosition(x, y))
                    {
                        var tile = _tileGrid[x, y];
                        if (tile.currentType == TileType.Path)
                        {
                            tile.ClearPath();
                            _tileViews[x, y]?.UpdateVisuals();
                        }
                    }
                }
            }

            _puzzleManager?.ResetPuzzle(currentPuzzleData.puzzleId);
            Debug.Log($"[FlowPuzzleManager] 퍼즐 리셋: {currentPuzzleData.puzzleId}");
        }
    }
}