// ScriptsArchive/MinimapTileGenerator.cs

using Nytherion.GamePlay.Dungeon;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.UI;
using Zenject; // Zenject 네임스페이스 추가

public class MinimapTileGenerator : MonoBehaviour
{
    [Header("UI")]
    public RawImage mapImage;

    [Header("아이콘 스타일")]
    [Range(1, 10)]
    public int iconPixelRadius = 2;
    public Color floorColor = new Color(0.15f, 0.15f, 0.15f);
    public Color wallColor = new Color(0.4f, 0.4f, 0.4f);
    public Color obstacleColor = new Color(0.54f, 0.27f, 0.07f);
    public Color portalColor = Color.magenta;
    public Color backgroundColor = Color.clear;

    [Header("뷰 설정")]
    public float fixedViewHeightInTiles = 30f;

    [Header("동적 아이콘 프리팹")]
    public RectTransform playerIconPrefab;
    public RectTransform enemyIconPrefab;

    private Texture2D minimapTexture;
    private Vector2Int mapOffset;
    private int mapWidth;
    private int mapHeight;
    private bool isInitialized = false;
    private RoomFirstDungeonGenerator.Room lastPlayerRoom = null;
    private Dictionary<RoomFirstDungeonGenerator.Room, HashSet<Vector2Int>> roomFloorData;
    private List<RoomFirstDungeonGenerator.Room> AllDungeonRooms = new List<RoomFirstDungeonGenerator.Room>();

    private RectTransform playerIconInstance;
    private List<RectTransform> enemyIconInstances = new List<RectTransform>();
    private List<GameObject> activeEnemiesRef;

    // --- 의존성 주입 ---
    private DungeonManager _dungeonManager;

    [Inject]
    public void Construct(DungeonManager dungeonManager)
    {
        _dungeonManager = dungeonManager;
    }

    public void InitializeMap(TilemapVisualizer visualizer, List<RoomFirstDungeonGenerator.PlacedObstacleData> obstacles, HashSet<Vector2Int> portals, Dictionary<RoomFirstDungeonGenerator.Room, HashSet<Vector2Int>> roomFloorData, List<RoomFirstDungeonGenerator.Room> allRooms)
    {
        this.roomFloorData = roomFloorData;
        this.AllDungeonRooms = allRooms;
       
        if (mapImage == null)
        {
            Debug.LogError("1");
            return;
        }
        if (visualizer == null)
        {
            Debug.LogError("2");
            return;
        }
        if ( visualizer.floorTilemap == null)
        {
            Debug.LogError("3");
            return;
        }
        Tilemap floorTilemap = visualizer.floorTilemap;
        Tilemap wallTilemap = visualizer.wallTilemap;

        floorTilemap.CompressBounds();
        wallTilemap.CompressBounds();

        BoundsInt contentBounds = floorTilemap.cellBounds;
        contentBounds.xMin = Mathf.Min(contentBounds.xMin, wallTilemap.cellBounds.xMin);
        contentBounds.yMin = Mathf.Min(contentBounds.yMin, wallTilemap.cellBounds.yMin);
        contentBounds.xMax = Mathf.Max(contentBounds.xMax, wallTilemap.cellBounds.xMax);
        contentBounds.yMax = Mathf.Max(contentBounds.yMax, wallTilemap.cellBounds.yMax);

        if (contentBounds.size.x == 0 || contentBounds.size.y == 0) return;

        int padding = 30;
        BoundsInt textureBounds = contentBounds;
        textureBounds.xMin -= padding;
        textureBounds.yMin -= padding;
        textureBounds.xMax += padding;
        textureBounds.yMax += padding;

        mapOffset = (Vector2Int)textureBounds.min;
        mapWidth = textureBounds.size.x;
        mapHeight = textureBounds.size.y;

        minimapTexture = new Texture2D(mapWidth, mapHeight);
        minimapTexture.filterMode = FilterMode.Point;
        Color[] baseLayerPixels = new Color[mapWidth * mapHeight];

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

        foreach (var obstacleData in obstacles)
        {
            Vector2Int gridPos = Vector2Int.RoundToInt(obstacleData.worldPosition);
            DrawStaticIcon(baseLayerPixels, gridPos.x - mapOffset.x, gridPos.y - mapOffset.y, obstacleColor, 1);
        }

        foreach (var portalPos in portals)
        {
            DrawStaticIcon(baseLayerPixels, portalPos.x - mapOffset.x, portalPos.y - mapOffset.y, portalColor, 1);
        }

        minimapTexture.SetPixels(baseLayerPixels);
        minimapTexture.Apply();
        mapImage.texture = minimapTexture;

        InitializeDynamicIcons();
        isInitialized = true;
    }

    private void InitializeDynamicIcons()
    {
        if (playerIconInstance != null) Destroy(playerIconInstance.gameObject);
        foreach (var icon in enemyIconInstances) if (icon != null) Destroy(icon.gameObject);
        enemyIconInstances.Clear();

        if (playerIconPrefab != null && mapImage != null)
        {
            playerIconInstance = Instantiate(playerIconPrefab, mapImage.transform);
            playerIconInstance.gameObject.SetActive(true);
        }

        if (enemyIconPrefab != null && _dungeonManager != null)
        {
            activeEnemiesRef = _dungeonManager.activeEnemies;
        }
    }

    void LateUpdate()
    {
        if (!isInitialized || _dungeonManager == null) return;

        RoomFirstDungeonGenerator.Room currentPlayerRoom = _dungeonManager.FindCurrentPlayerRoom();
        if (currentPlayerRoom != null && currentPlayerRoom != lastPlayerRoom)
        {
            lastPlayerRoom = currentPlayerRoom;
            UpdateMinimapView(lastPlayerRoom);
        }
        else if (lastPlayerRoom == null && AllDungeonRooms != null && AllDungeonRooms.Count > 0)
        {
            lastPlayerRoom = AllDungeonRooms[0];
            UpdateMinimapView(lastPlayerRoom);
        }

        UpdateDynamicIconsPosition();
    }

    private void UpdateDynamicIconsPosition()
    {
        if (playerIconInstance != null && _dungeonManager.playerObject != null)
        {
            playerIconInstance.anchoredPosition = WorldToMinimapPosition(_dungeonManager.playerObject.transform.position);
        }
        UpdateEnemyIcons();
    }

    // ... (WorldToMinimapPosition, UpdateEnemyIcons, UpdateMinimapView, DrawStaticIcon 메서드는 변경 없음) ...
    private Vector2 WorldToMinimapPosition(Vector3 worldPosition)
    {
        if (mapWidth == 0 || mapHeight == 0) return Vector2.zero;

        // 월드 좌표를 전체 맵 텍스처 기준의 [0, 1] UV 좌표로 변환
        float u = (worldPosition.x - mapOffset.x) / mapWidth;
        float v = (worldPosition.y - mapOffset.y) / mapHeight;

        // 현재 보이는 미니맵 영역(uvRect)을 기준으로 다시 [0, 1] 좌표로 변환
        Rect uvRect = mapImage.uvRect;
        float finalU = (u - uvRect.x) / uvRect.width;
        float finalV = (v - uvRect.y) / uvRect.height;

        // 미니맵 UI(RawImage)의 피벗을 고려하여 정확한 anchoredPosition 계산
        Rect mapRect = mapImage.rectTransform.rect;
        float finalX = finalU * mapRect.width;
        float finalY = finalV * mapRect.height;

        // 미니맵 UI의 피벗만큼 보정해 줘서 최종 위치를 구함
        Vector2 pivotOffset = new Vector2(mapRect.width * mapImage.rectTransform.pivot.x, mapRect.height * mapImage.rectTransform.pivot.y);

        return new Vector2(finalX, finalY) - pivotOffset;
    }

    private void UpdateEnemyIcons()
    {
        if (activeEnemiesRef == null || enemyIconPrefab == null || mapImage == null) return;

        // 활성화된 적 수에 맞춰 아이콘 풀 조절
        while (enemyIconInstances.Count < activeEnemiesRef.Count)
        {
            RectTransform newIcon = Instantiate(enemyIconPrefab, mapImage.transform);
            enemyIconInstances.Add(newIcon);
        }

        // 활성화된 적에 맞춰 아이콘 업데이트
        for (int i = 0; i < enemyIconInstances.Count; i++)
        {
            // 리스트 범위 및 null 체크
            if (i < activeEnemiesRef.Count && activeEnemiesRef[i] != null && activeEnemiesRef[i].activeInHierarchy)
            {
                enemyIconInstances[i].gameObject.SetActive(true);
                enemyIconInstances[i].anchoredPosition = WorldToMinimapPosition(activeEnemiesRef[i].transform.position);
            }
            else
            {
                enemyIconInstances[i].gameObject.SetActive(false);
            }
        }
    }

    private void UpdateMinimapView(RoomFirstDungeonGenerator.Room room)
    {
        if (roomFloorData == null || !roomFloorData.TryGetValue(room, out var tilesInRoom) || tilesInRoom.Count == 0)
        {
            return;
        }

        Vector2Int minPos = tilesInRoom.First();
        Vector2Int maxPos = tilesInRoom.First();
        foreach (var tile in tilesInRoom)
        {
            minPos.x = Mathf.Min(minPos.x, tile.x);
            minPos.y = Mathf.Min(minPos.y, tile.y);
            maxPos.x = Mathf.Max(maxPos.x, tile.x);
            maxPos.y = Mathf.Max(maxPos.y, tile.y);
        }
        Vector2 visualCenter = ((Vector2)minPos + (Vector2)maxPos) / 2.0f;

        float uiAspectRatio = mapImage.rectTransform.rect.width / mapImage.rectTransform.rect.height;
        float viewHeightInTiles = fixedViewHeightInTiles;
        float viewWidthInTiles = viewHeightInTiles * uiAspectRatio;

        // UV 좌표 계산
        float uvWidth = viewWidthInTiles / mapWidth;
        float uvHeight = viewHeightInTiles / mapHeight;
        float centerX_uv = (visualCenter.x - mapOffset.x + 0.5f) / mapWidth;
        float centerY_uv = (visualCenter.y - mapOffset.y + 0.5f) / mapHeight;

        float startX = centerX_uv - (uvWidth / 2f);
        float startY = centerY_uv - (uvHeight / 2f);

        // [수정] 계산된 UV 좌표가 텍스처 범위를 벗어나지 않도록 보정
        startX = Mathf.Clamp(startX, 0f, 1f - uvWidth);
        startY = Mathf.Clamp(startY, 0f, 1f - uvHeight);

        mapImage.uvRect = new Rect(startX, startY, uvWidth, uvHeight);
    }

    private void DrawStaticIcon(Color[] pixelBuffer, int centerX, int centerY, Color color, int? optionalRadius = null)
    {
        int radius = optionalRadius ?? iconPixelRadius;
        for (int x = -radius; x <= radius; x++)
        {
            for (int y = -radius; y <= radius; y++)
            {
                int texX = centerX + x;
                int texY = centerY + y;
                if (texX >= 0 && texX < mapWidth && texY >= 0 && texY < mapHeight)
                {
                    pixelBuffer[texY * mapWidth + texX] = color;
                }
            }
        }
    }
}