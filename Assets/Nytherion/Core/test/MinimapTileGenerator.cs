using Nytherion.GamePlay.Dungeon;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.UI;
using VContainer;
using Nytherion.GamePlay.Characters.Enemy;

namespace Nytherion.UI.Map
{
    /// <summary>
    /// 던전 구조를 기반으로 미니맵 UI를 생성하고 동적 아이콘(플레이어, 적)을 관리합니다.
    /// </summary>
    public class MinimapTileGenerator : MonoBehaviour
    {
        [Header("UI 참조")]
        [Tooltip("미니맵 텍스처를 표시할 RawImage UI 컴포넌트")]
        public RawImage mapImage;

        [Header("정적 아이콘 스타일")]
        [Tooltip("장애물, 포탈 등 고정된 아이콘의 픽셀 크기")]
        [Range(1, 10)]
        public int iconPixelRadius = 2;
        [Tooltip("바닥 타일의 색상")]
        public Color floorColor = new Color(0.15f, 0.15f, 0.15f);
        [Tooltip("벽 타일의 색상")]
        public Color wallColor = new Color(0.4f, 0.4f, 0.4f);
        [Tooltip("장애물 아이콘의 색상")]
        public Color obstacleColor = new Color(0.54f, 0.27f, 0.07f);
        [Tooltip("포탈 아이콘의 색상")]
        public Color portalColor = Color.magenta;
        [Tooltip("맵의 배경색")]
        public Color backgroundColor = Color.clear;

        [Header("뷰 설정")]
        [Tooltip("미니맵에 표시될 방 주변의 여백 크기 (타일 단위)")]
        public float viewPaddingInTiles = 10f; 

        [Header("동적 아이콘 프리팹")]
        [Tooltip("플레이어 아이콘으로 사용할 UI 프리팹")]
        public RectTransform playerIconPrefab;
        [Tooltip("적 아이콘으로 사용할 UI 프리팹")]
        public RectTransform enemyIconPrefab;

        // 미니맵 생성에 필요한 내부 변수들
        private Texture2D minimapTexture;
        private Vector2Int mapOffset; // 텍스처의 (0,0)에 해당하는 월드 타일 좌표
        private int mapWidth, mapHeight; // 텍스처의 전체 너비와 높이
        private bool isInitialized = false;

        // 플레이어의 현재 방과 방 데이터를 저장하기 위한 변수
        private RoomFirstDungeonGenerator.Room lastPlayerRoom = null;
        private Dictionary<RoomFirstDungeonGenerator.Room, HashSet<Vector2Int>> roomFloorData;

        // 동적 아이콘 인스턴스 및 풀
        private RectTransform playerIconInstance;
        private readonly List<RectTransform> enemyIconPool = new List<RectTransform>();

        // 의존성 주입
        private DungeonManager _dungeonManager;

        [Inject]
        public void Construct(DungeonManager dungeonManager = null)
        {
            _dungeonManager = dungeonManager;
            if (dungeonManager == null)
            {
                Debug.LogWarning("[MinimapTileGenerator] DungeonManager가 주입되지 않았습니다. 미니맵 기능이 작동하지 않습니다.");
            }
        }

        /// <summary>
        /// 던전 데이터를 기반으로 미니맵을 초기화하고 생성합니다.
        /// </summary>
        public void InitializeMap(TilemapVisualizer visualizer, List<RoomFirstDungeonGenerator.PlacedObstacleData> obstacles, HashSet<Vector2Int> portals, Dictionary<RoomFirstDungeonGenerator.Room, HashSet<Vector2Int>> roomFloorData, List<RoomFirstDungeonGenerator.Room> allRooms)
        {
            if (!ValidateInitializationParameters(visualizer)) return;

            this.roomFloorData = roomFloorData;

            // 전체 맵의 경계를 계산합니다.
            CalculateMapBounds(visualizer.floorTilemap, visualizer.wallTilemap);
            if (mapWidth == 0 || mapHeight == 0)
            {
                Debug.LogWarning("[Minimap] Map size is zero. Minimap generation aborted.");
                return;
            }

            // 맵 경계를 기반으로 텍스처를 생성하고 그립니다.
            CreateMinimapTexture(visualizer.floorTilemap, visualizer.wallTilemap, obstacles, portals);

            // 플레이어, 적 등 동적 아이콘을 초기화합니다.
            InitializeDynamicIcons();
            isInitialized = true;
        }

        private void LateUpdate()
        {
            if (!isInitialized || _dungeonManager == null || _dungeonManager.playerObject == null) return;

            // 플레이어가 새로운 방에 들어갔는지 확인하여 미니맵 뷰를 업데이트합니다.
            RoomFirstDungeonGenerator.Room currentPlayerRoom = _dungeonManager.FindCurrentPlayerRoom();
            if (currentPlayerRoom != null && currentPlayerRoom != lastPlayerRoom)
            {
                lastPlayerRoom = currentPlayerRoom;
                UpdateMinimapView(lastPlayerRoom);
            }

            // 플레이어와 적 아이콘의 위치를 매 프레임 업데이트합니다.
            UpdateDynamicIconsPosition();
        }

        /// <summary>
        /// 초기화에 필요한 파라미터들이 유효한지 검사합니다.
        /// </summary>
        private bool ValidateInitializationParameters(TilemapVisualizer visualizer)
        {
            if (mapImage == null)
            {
                Debug.LogError("[Minimap] RawImage for map is not assigned!");
                return false;
            }
            if (visualizer == null || visualizer.floorTilemap == null || visualizer.wallTilemap == null)
            {
                Debug.LogError("[Minimap] TilemapVisualizer or its tilemaps are not properly assigned!");
                return false;
            }
            return true;
        }

        /// <summary>
        /// 바닥과 벽 타일맵을 기반으로 전체 맵의 경계(크기 및 오프셋)를 계산합니다.
        /// </summary>
        private void CalculateMapBounds(Tilemap floorTilemap, Tilemap wallTilemap)
        {
            floorTilemap.CompressBounds();
            wallTilemap.CompressBounds();

            BoundsInt contentBounds = floorTilemap.cellBounds;
            contentBounds.xMin = Mathf.Min(contentBounds.xMin, wallTilemap.cellBounds.xMin);
            contentBounds.yMin = Mathf.Min(contentBounds.yMin, wallTilemap.cellBounds.yMin);
            contentBounds.xMax = Mathf.Max(contentBounds.xMax, wallTilemap.cellBounds.xMax);
            contentBounds.yMax = Mathf.Max(contentBounds.yMax, wallTilemap.cellBounds.yMax);

            if (contentBounds.size.x == 0 || contentBounds.size.y == 0)
            {
                mapWidth = 0;
                mapHeight = 0;
                return;
            }

            // 맵 경계에 여백(padding)을 추가하여 텍스처 크기를 결정합니다.
            int padding = 30;
            BoundsInt textureBounds = contentBounds;
            textureBounds.xMin -= padding;
            textureBounds.yMin -= padding;
            textureBounds.xMax += padding;
            textureBounds.yMax += padding;

            mapOffset = (Vector2Int)textureBounds.min;
            mapWidth = textureBounds.size.x;
            mapHeight = textureBounds.size.y;
        }

        /// <summary>
        /// 계산된 맵 경계를 바탕으로 미니맵 텍스처를 생성하고, 바닥, 벽, 장애물, 포탈을 그립니다.
        /// </summary>
        private void CreateMinimapTexture(Tilemap floorTilemap, Tilemap wallTilemap, List<RoomFirstDungeonGenerator.PlacedObstacleData> obstacles, HashSet<Vector2Int> portals)
        {
            minimapTexture = new Texture2D(mapWidth, mapHeight)
            {
                filterMode = FilterMode.Point // 픽셀이 뚜렷하게 보이도록 설정
            };

            Color[] baseLayerPixels = new Color[mapWidth * mapHeight];

            // 모든 픽셀을 순회하며 타일맵 정보를 바탕으로 색상을 칠합니다.
            for (int y = 0; y < mapHeight; y++)
            {
                for (int x = 0; x < mapWidth; x++)
                {
                    Vector3Int tilePos = new Vector3Int(mapOffset.x + x, mapOffset.y + y, 0);
                    int pixelIndex = y * mapWidth + x;

                    if (floorTilemap.HasTile(tilePos))
                        baseLayerPixels[pixelIndex] = floorColor;
                    else if (wallTilemap.HasTile(tilePos))
                        baseLayerPixels[pixelIndex] = wallColor;
                    else
                        baseLayerPixels[pixelIndex] = backgroundColor;
                }
            }

            // 장애물과 포탈 아이콘을 텍스처에 그립니다.
            foreach (RoomFirstDungeonGenerator.PlacedObstacleData obstacleData in obstacles)
            {
                Vector2Int gridPos = Vector2Int.RoundToInt(obstacleData.worldPosition);
                DrawStaticIcon(baseLayerPixels, gridPos.x - mapOffset.x, gridPos.y - mapOffset.y, obstacleColor, 1);
            }

            foreach (Vector2Int portalPos in portals)
            {
                DrawStaticIcon(baseLayerPixels, portalPos.x - mapOffset.x, portalPos.y - mapOffset.y, portalColor, 1);
            }

            minimapTexture.SetPixels(baseLayerPixels);
            minimapTexture.Apply();
            mapImage.texture = minimapTexture;
        }

        /// <summary>
        /// 플레이어와 적 아이콘 인스턴스를 생성하고 초기화합니다.
        /// </summary>
        private void InitializeDynamicIcons()
        {
            // 기존 아이콘이 있다면 모두 제거
            if (playerIconInstance != null) Destroy(playerIconInstance.gameObject);
            foreach (RectTransform icon in enemyIconPool)
            {
                if (icon != null) Destroy(icon.gameObject);
            }
            enemyIconPool.Clear();

            // 플레이어 아이콘 생성
            if (playerIconPrefab != null && mapImage != null)
            {
                playerIconInstance = Instantiate(playerIconPrefab, mapImage.transform);
                playerIconInstance.gameObject.SetActive(true);
            }
        }

        /// <summary>
        /// 플레이어와 모든 적 아이콘의 UI 위치를 업데이트합니다.
        /// </summary>
        private void UpdateDynamicIconsPosition()
        {
            if (playerIconInstance != null && _dungeonManager.playerObject != null)
            {
                playerIconInstance.anchoredPosition = WorldToMinimapPosition(_dungeonManager.playerObject.transform.position);
            }
            UpdateEnemyIcons();
        }

        /// <summary>
        /// 적 아이콘들을 관리하고 위치를 업데이트합니다. 오브젝트 풀링을 사용하여 효율을 높입니다.
        /// </summary>
        private void UpdateEnemyIcons()
        {
            if (_dungeonManager == null || enemyIconPrefab == null || mapImage == null) return;

            // DungeonManager로부터 현재 활성화된 모든 적 리스트를 가져옵니다.
            List<EnemyBase> allEnemies = _dungeonManager.AllActiveEnemies;

            // 실제로 화면에 보여야 할 활성화된 적의 수를 셉니다.
            int activeEnemyCount = allEnemies.Count(enemy => enemy != null && enemy.gameObject.activeInHierarchy);

            // 필요한 만큼 적 아이콘을 풀에서 생성하거나 가져옵니다.
            while (enemyIconPool.Count < activeEnemyCount)
            {
                RectTransform newIcon = Instantiate(enemyIconPrefab, mapImage.transform);
                newIcon.gameObject.SetActive(false);
                enemyIconPool.Add(newIcon);
            }

            int iconIndex = 0;
            // 활성화된 적들의 위치에 아이콘을 배치합니다.
            foreach (EnemyBase enemy in allEnemies)
            {
                if (enemy != null && enemy.gameObject.activeInHierarchy)
                {
                    if (iconIndex < enemyIconPool.Count)
                    {
                        RectTransform icon = enemyIconPool[iconIndex];
                        icon.gameObject.SetActive(true);
                        icon.anchoredPosition = WorldToMinimapPosition(enemy.transform.position);
                        iconIndex++;
                    }
                }
            }

            // 사용하지 않는 아이콘들은 비활성화합니다.
            for (int i = iconIndex; i < enemyIconPool.Count; i++)
            {
                enemyIconPool[i].gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// 플레이어가 있는 방을 중심으로 미니맵 뷰(보여지는 영역)를 업데이트합니다.
        /// </summary>
        /// <summary>
        /// 플레이어가 있는 방을 중심으로 미니맵 뷰(보여지는 영역)를 업데이트합니다.
        /// 방의 크기에 맞춰 뷰의 크기가 동적으로 조절됩니다.
        /// </summary>
        private void UpdateMinimapView(RoomFirstDungeonGenerator.Room room)
        {
            if (roomFloorData == null || !roomFloorData.TryGetValue(room, out HashSet<Vector2Int> tilesInRoom) || tilesInRoom.Count == 0)
            {
                return;
            }

            // 1. 방의 경계와 시각적 중심점을 계산합니다.
            Vector2Int minPos = tilesInRoom.First();
            Vector2Int maxPos = tilesInRoom.First();
            foreach (Vector2Int tile in tilesInRoom)
            {
                minPos.x = Mathf.Min(minPos.x, tile.x);
                minPos.y = Mathf.Min(minPos.y, tile.y);
                maxPos.x = Mathf.Max(maxPos.x, tile.x);
                maxPos.y = Mathf.Max(maxPos.y, tile.y);
            }
            Vector2 visualCenter = ((Vector2)minPos + (Vector2)maxPos) / 2.0f;
            float roomWidth = maxPos.x - minPos.x + 1;
            float roomHeight = maxPos.y - minPos.y + 1;

            // 2. 방이 잘리지 않도록 미니맵 뷰의 크기를 동적으로 계산합니다.
            float uiAspectRatio = mapImage.rectTransform.rect.width / mapImage.rectTransform.rect.height;

            // 방의 가로 길이를 화면 비율에 맞게 보여주기 위해 필요한 세로 길이를 계산하고,
            // 실제 방의 세로 길이와 비교하여 더 큰 값을 기준으로 뷰 크기를 정합니다.
            float requiredHeightForWidth = roomWidth / uiAspectRatio;
            float viewHeightInTiles = Mathf.Max(roomHeight, requiredHeightForWidth) + viewPaddingInTiles; // 여백 추가
            float viewWidthInTiles = viewHeightInTiles * uiAspectRatio;

            // 3. RawImage의 uvRect를 조절하여 줌인/줌아웃 및 스크롤 효과를 구현합니다.
            float uvWidth = viewWidthInTiles / mapWidth;
            float uvHeight = viewHeightInTiles / mapHeight;
            float centerX_uv = (visualCenter.x - mapOffset.x + 0.5f) / mapWidth;
            float centerY_uv = (visualCenter.y - mapOffset.y + 0.5f) / mapHeight;

            float startX = Mathf.Clamp(centerX_uv - (uvWidth / 2f), 0f, 1f - uvWidth);
            float startY = Mathf.Clamp(centerY_uv - (uvHeight / 2f), 0f, 1f - uvHeight);

            mapImage.uvRect = new Rect(startX, startY, uvWidth, uvHeight);
        }

        /// <summary>
        /// 월드 좌표를 미니맵 UI의 로컬 좌표로 변환합니다.
        /// </summary>
        private Vector2 WorldToMinimapPosition(Vector3 worldPosition)
        {
            if (mapWidth == 0 || mapHeight == 0) return Vector2.zero;

            // 월드 좌표를 전체 텍스처의 UV 좌표(0~1)로 변환
            float u = (worldPosition.x - mapOffset.x) / mapWidth;
            float v = (worldPosition.y - mapOffset.y) / mapHeight;

            // 현재 보여지는 뷰(uvRect)를 기준으로 상대적인 UV 좌표 계산
            Rect uvRect = mapImage.uvRect;
            float finalU = (u - uvRect.x) / uvRect.width;
            float finalV = (v - uvRect.y) / uvRect.height;

            // 상대 UV 좌표를 RawImage의 실제 픽셀 좌표로 변환
            Rect mapRect = mapImage.rectTransform.rect;
            float finalX = finalU * mapRect.width;
            float finalY = finalV * mapRect.height;

            // RawImage의 피벗을 고려하여 최종 좌표 보정
            Vector2 pivotOffset = new Vector2(mapRect.width * mapImage.rectTransform.pivot.x, mapRect.height * mapImage.rectTransform.pivot.y);

            return new Vector2(finalX, finalY) - pivotOffset;
        }

        /// <summary>
        /// 텍스처의 픽셀 버퍼에 지정된 색상으로 사각형 아이콘을 그립니다.
        /// </summary>
        private void DrawStaticIcon(Color[] pixelBuffer, int centerX, int centerY, Color color, int? optionalRadius = null)
        {
            int radius = optionalRadius ?? iconPixelRadius;
            for (int x = -radius; x <= radius; x++)
            {
                for (int y = -radius; y <= radius; y++)
                {
                    int texX = centerX + x;
                    int texY = centerY + y;
                    // 텍스처 범위를 벗어나지 않는지 확인
                    if (texX >= 0 && texX < mapWidth && texY >= 0 && texY < mapHeight)
                    {
                        pixelBuffer[texY * mapWidth + texX] = color;
                    }
                }
            }
        }
    }
}