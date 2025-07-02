/* RoomFirstDungeonGenerator.cs (장애물 간격 유지 로직 추가)

    [역할]
    이 스크립트는 던전 생성의 모든 핵심 로직을 담당하는 메인 컨트롤 타워.

    [핵심 변경점]
    - (로직 개선) PlaceObstacles 메서드 내에서, 장애물을 하나 배치한 후
      그 주변의 여유 공간(1칸)까지 함께 다음 배치 후보에서 제외하도록 수정했습니다.
      이를 통해 장애물끼리 서로 붙어서 생성되는 것을 방지합니다.
*/
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;
using Random = UnityEngine.Random;

public class RoomFirstDungeonGenerator : AbstractDungeonGenertor
{
    /// <summary>
    /// 배치될 장애물의 최종 데이터를 담는 구조체
    /// </summary>
    public struct PlacedObstacleData
    {
        public GameObject prefab;
        public Vector2 worldPosition;
    }

    public enum RoomType { Normal, Start, Boss, Shop, Item }

    [Header("Dungeon Data")]
    [Tooltip("던전 생성을 위한 설정값이 담긴 ScriptableObject 입니다.")]
    [SerializeField]
    private DungeonData dungeonData;

    [Header("Dependencies")]
    [Tooltip("씬에 있는 미니맵 컨트롤러를 연결해주세요.")]
    [SerializeField] private WorldmapController worldmapController;

    public static event Action<Room> OnDungeonGenerated;

    public class Room
    {
        public Vector2Int gridPos;
        public Vector2Int size;
        public Vector2 center;
        public BoundsInt Bounds => new BoundsInt(Vector3Int.RoundToInt(center - (Vector2)size / 2), (Vector3Int)size);
        public RoomType type = RoomType.Normal;

        public Room(Vector2Int gridPos, Vector2Int size)
        {
            this.gridPos = gridPos;
            this.size = size;
        }
    }

    protected override void RunProceduralGeneration()
    {
        if (dungeonData == null)
        {
            Debug.LogError("DungeonData가 할당되지 않았습니다! Inspector에서 할당해주세요.");
            return;
        }
        DungeonManager.Instance.ClearDungeonData();

        // 1. 파라미터 준비
        int side = Mathf.CeilToInt(Mathf.Sqrt(dungeonData.desiredNumberOfRooms));
        Vector2Int gridSize = new Vector2Int(side * 2, side * 2);
        float roomSpacing = Mathf.Max(dungeonData.maxRoomSize.x, dungeonData.maxRoomSize.y) * 1.2f;
        const int placementIterations = 50;
        const int offset = 1;

        // 2. 던전 레이아웃 생성
        List<Vector2Int> selectedGridPositions = RetrySelectConnectedGridPositions(gridSize);
        if (selectedGridPositions.Count == 0) return;

        // 3. 방 데이터 생성 및 배치
        Dictionary<Vector2Int, Room> roomGrid = CreateRoomData(selectedGridPositions);
        PlaceAndAlignRooms(roomGrid, roomSpacing);
        ResolveOverlaps(roomGrid.Values.ToList(), placementIterations, roomSpacing);

        // 4. 특수 방 지정
        Dictionary<Vector2Int, int> gridConnectionCount = CalculateGridConnections(selectedGridPositions);
        Room startRoom = DesignateAllSpecialRooms(roomGrid, gridConnectionCount);

        // 5. 바닥, 벽, 포탈, 장애물 위치 계산
        var roomFloorData = new Dictionary<Room, HashSet<Vector2Int>>();
        HashSet<Vector2Int> totalFloorPositions = CreateAllRoomFloors(roomGrid, roomFloorData, offset);
        var connectionData = ConnectAdjacentRooms(roomGrid, roomFloorData, totalFloorPositions);
        HashSet<Vector2Int> portalPositions = connectionData.portalPositions;
        List<Tuple<Room, Room>> roomConnections = connectionData.connections;
        List<PlacedObstacleData> obstaclesToPlace = PlaceObstacles(roomFloorData, portalPositions, totalFloorPositions);

        // 6. 최종 타일맵 및 오브젝트 그리기
        var wallPositions = WallGenerator.FindWalls(totalFloorPositions);
        wallPositions.ExceptWith(portalPositions);
        tilemapVisualizer.PaintWallTiles(wallPositions);
        var normalFloorPositions = new HashSet<Vector2Int>(totalFloorPositions);
        var specialRooms = roomGrid.Values.Where(r => r.type != RoomType.Normal).ToList();
        foreach (var specialRoom in specialRooms)
        {
            if (roomFloorData.ContainsKey(specialRoom))
                normalFloorPositions.ExceptWith(roomFloorData[specialRoom]);
        }
        tilemapVisualizer.PaintFloorTiles(normalFloorPositions);
        tilemapVisualizer.PaintSpecialRoomFloors(specialRooms, dungeonData, roomFloorData);
        tilemapVisualizer.PaintPortals(portalPositions);
        tilemapVisualizer.InstantiateObstacles(obstaclesToPlace);

        DungeonManager.Instance.SetAllRooms(new List<Room>(roomGrid.Values));
        // 7. 미니맵 그리기 (플레이어 스폰 전에 먼저 그립니다)
        if (worldmapController != null)
        {
            worldmapController.DrawMap(roomGrid.Values, roomConnections, dungeonData);
        }
        else
        {
            Debug.LogWarning("MinimapController가 할당되지 않아 미니맵을 그릴 수 없습니다.");
        }

        // 8. 플레이어 스폰 위치 알림 (가장 마지막에 호출)
        if (startRoom != null)
        {
            // 이벤트에 Room 객체 전체를 전달합니다.
            OnDungeonGenerated?.Invoke(startRoom);
        }
    }

    #region Obstacle Placement

    /// <summary>
    /// 개별 크기와 간격을 고려하여 장애물을 배치하고, 생성할 프리팹과 월드 좌표 목록을 반환합니다.
    /// </summary>
    private List<PlacedObstacleData> PlaceObstacles(Dictionary<Room, HashSet<Vector2Int>> roomFloorData, HashSet<Vector2Int> portalPositions, HashSet<Vector2Int> allFloorTiles)
    {
        var obstaclesToPlace = new List<PlacedObstacleData>();
        var allWallPositions = WallGenerator.FindWalls(allFloorTiles);

        foreach (var roomData in roomFloorData)
        {
            if (roomData.Key.type != RoomType.Normal)
            {
                continue;
            }

            var candidatePositions = new HashSet<Vector2Int>(roomData.Value);

            int numberOfObstacles = Random.Range(dungeonData.minObstaclesPerRoom, dungeonData.maxObstaclesPerRoom + 1);

            for (int i = 0; i < numberOfObstacles && candidatePositions.Count > 1; i++)
            {
                if (dungeonData.obstacles.Length == 0) break;
                var selectedObstacleData = dungeonData.obstacles[Random.Range(0, dungeonData.obstacles.Length)];

                if (selectedObstacleData.prefab == null)
                {
                    Debug.LogWarning("DungeonData의 obstacles 배열에 비어있는(null) 프리팹이 있습니다. 건너뜁니다.");
                    continue;
                }

                Vector2Int obstacleSize = selectedObstacleData.size;

                int marginX = Mathf.CeilToInt((obstacleSize.x - 1) / 2.0f);
                int marginY = Mathf.CeilToInt((obstacleSize.y - 1) / 2.0f);

                var validPlacementSpots = new List<Vector2Int>();

                foreach (var potentialCenter in candidatePositions)
                {
                    bool isSafe = true;
                    for (int x = -marginX; x <= marginX; x++)
                    {
                        for (int y = -marginY; y <= marginY; y++)
                        {
                            Vector2Int checkPos = potentialCenter + new Vector2Int(x, y);
                            if (allWallPositions.Contains(checkPos) || portalPositions.Contains(checkPos) || !roomData.Value.Contains(checkPos))
                            {
                                isSafe = false;
                                break;
                            }
                        }
                        if (!isSafe) break;
                    }

                    if (isSafe)
                    {
                        validPlacementSpots.Add(potentialCenter);
                    }
                }

                if (validPlacementSpots.Count > 0)
                {
                    Vector2Int placementCenter = validPlacementSpots[Random.Range(0, validPlacementSpots.Count)];

                    obstaclesToPlace.Add(new PlacedObstacleData
                    {
                        prefab = selectedObstacleData.prefab,
                        worldPosition = (Vector2)placementCenter + new Vector2(0.5f, 0.5f)
                    });

                    // --- 수정된 부분: 장애물 간격 확보 ---
                    // 장애물이 차지하는 공간뿐만 아니라, 그 주변 1칸의 여유 공간까지 함께 후보에서 제외합니다.
                    int exclusionMarginX = marginX + 1;
                    int exclusionMarginY = marginY + 1;

                    for (int x = -exclusionMarginX; x <= exclusionMarginX; x++)
                    {
                        for (int y = -exclusionMarginY; y <= exclusionMarginY; y++)
                        {
                            candidatePositions.Remove(placementCenter + new Vector2Int(x, y));
                        }
                    }
                    // --- 여기까지 ---
                }
            }
        }
        return obstaclesToPlace;
    }

    #endregion

    #region Dungeon Layout and Room Type Designation
    private List<Vector2Int> RetrySelectConnectedGridPositions(Vector2Int gridSize)
    {
        List<Vector2Int> selectedGridPositions;
        int generationAttempts = 0;
        const int maxGenerationAttempts = 200;
        int requiredDeadEnds = 2 + dungeonData.numberOfShopRooms + dungeonData.numberOfItemRooms;

        while (true)
        {
            generationAttempts++;
            if (generationAttempts > maxGenerationAttempts)
            {
                Debug.LogError($"최대 시도 횟수({maxGenerationAttempts})를 초과했습니다. 규칙에 맞는 던전을 생성할 수 없습니다. 방 개수를 줄이거나 그리드를 늘려보세요.");
                return new List<Vector2Int>();
            }
            selectedGridPositions = SelectConnectedGridPositions(gridSize);
            if (selectedGridPositions.Count < dungeonData.desiredNumberOfRooms) continue;
            var gridConnectionCount = CalculateGridConnections(selectedGridPositions);
            int deadEndCount = gridConnectionCount.Values.Count(c => c == 1);
            if (deadEndCount >= requiredDeadEnds) break;
        }
        return selectedGridPositions;
    }

    private Room DesignateAllSpecialRooms(Dictionary<Vector2Int, Room> roomGrid, Dictionary<Vector2Int, int> gridConnectionCount)
    {
        var deadEndRooms = roomGrid.Values
            .Where(room => gridConnectionCount.ContainsKey(room.gridPos) && gridConnectionCount[room.gridPos] == 1)
            .OrderBy(r => Random.value).ToList();

        Room bossRoom = deadEndRooms[0];
        bossRoom.type = RoomType.Boss;
        deadEndRooms.RemoveAt(0);

        Room startRoom = deadEndRooms.OrderByDescending(r => Vector2.Distance(r.center, bossRoom.center)).FirstOrDefault();
        if (startRoom != null)
        {
            startRoom.type = RoomType.Start;
            deadEndRooms.Remove(startRoom);
        }

        Action<RoomType, int> designateRooms = (type, count) => {
            for (int i = 0; i < count && deadEndRooms.Count > 0; i++)
            {
                deadEndRooms[0].type = type;
                deadEndRooms.RemoveAt(0);
            }
        };
        designateRooms(RoomType.Shop, dungeonData.numberOfShopRooms);
        designateRooms(RoomType.Item, dungeonData.numberOfItemRooms);

        return startRoom;
    }

    private Dictionary<Vector2Int, int> CalculateGridConnections(List<Vector2Int> gridPositions)
    {
        var connectionCount = new Dictionary<Vector2Int, int>();
        var positionSet = new HashSet<Vector2Int>(gridPositions);
        foreach (var pos in gridPositions)
        {
            int count = 0;
            foreach (var dir in WallGenerator.Direction2D.cardinalDirectionsList)
            {
                if (positionSet.Contains(pos + dir)) count++;
            }
            connectionCount[pos] = count;
        }
        return connectionCount;
    }

    private List<Vector2Int> SelectConnectedGridPositions(Vector2Int gridSize)
    {
        var selectedPositions = new List<Vector2Int>();
        if (dungeonData.desiredNumberOfRooms == 0) return selectedPositions;
        var frontier = new List<Vector2Int>();
        var startPos = new Vector2Int(Random.Range(0, gridSize.x), Random.Range(0, gridSize.y));
        selectedPositions.Add(startPos);
        foreach (var dir in WallGenerator.Direction2D.cardinalDirectionsList)
        {
            Vector2Int neighbor = startPos + dir;
            if (IsGridPositionValid(neighbor, gridSize)) frontier.Add(neighbor);
        }
        while (selectedPositions.Count < dungeonData.desiredNumberOfRooms && frontier.Count > 0)
        {
            int frontierIndex = Random.Range(0, frontier.Count);
            Vector2Int nextPos = frontier[frontierIndex];
            frontier.RemoveAt(frontierIndex);
            if (selectedPositions.Contains(nextPos)) continue;
            selectedPositions.Add(nextPos);
            foreach (var dir in WallGenerator.Direction2D.cardinalDirectionsList)
            {
                Vector2Int neighbor = nextPos + dir;
                if (IsGridPositionValid(neighbor, gridSize)) frontier.Add(neighbor);
            }
        }
        return selectedPositions;
    }

    private bool IsGridPositionValid(Vector2Int pos, Vector2Int gridSize)
    {
        return pos.x >= 0 && pos.x < gridSize.x && pos.y >= 0 && pos.y < gridSize.y;
    }
    #endregion

    #region Room Placement and Alignment
    private Dictionary<Vector2Int, Room> CreateRoomData(List<Vector2Int> selectedGridPositions)
    {
        var roomGrid = new Dictionary<Vector2Int, Room>();
        foreach (var gridPos in selectedGridPositions)
        {
            var roomSize = new Vector2Int(
                Random.Range(dungeonData.minRoomSize.x, dungeonData.maxRoomSize.x + 1),
                Random.Range(dungeonData.minRoomSize.y, dungeonData.maxRoomSize.y + 1));
            roomGrid.Add(gridPos, new Room(gridPos, roomSize));
        }
        return roomGrid;
    }

    private void PlaceAndAlignRooms(Dictionary<Vector2Int, Room> roomGrid, float roomSpacing)
    {
        var queue = new Queue<Room>();
        var visited = new HashSet<Vector2Int>();
        Room startRoom = roomGrid.Values.First();
        startRoom.center = Vector2.zero;
        queue.Enqueue(startRoom);
        visited.Add(startRoom.gridPos);
        while (queue.Count > 0)
        {
            Room parentRoom = queue.Dequeue();
            var shuffledDirections = WallGenerator.Direction2D.cardinalDirectionsList.OrderBy(d => Random.value).ToList();
            foreach (var direction in shuffledDirections)
            {
                Vector2Int neighborGridPos = parentRoom.gridPos + direction;
                if (roomGrid.TryGetValue(neighborGridPos, out Room neighborRoom) && !visited.Contains(neighborGridPos))
                {
                    float xOffset = (parentRoom.size.x + neighborRoom.size.x) / 2f + roomSpacing;
                    float yOffset = (parentRoom.size.y + neighborRoom.size.y) / 2f + roomSpacing;
                    Vector2 offsetVector = new Vector2(direction.x * xOffset, direction.y * yOffset);
                    neighborRoom.center = parentRoom.center + offsetVector;
                    visited.Add(neighborGridPos);
                    queue.Enqueue(neighborRoom);
                }
            }
        }
    }

    private void ResolveOverlaps(List<Room> rooms, int placementIterations, float roomSpacing)
    {
        for (int i = 0; i < placementIterations; i++)
        {
            for (int j = 0; j < rooms.Count; j++)
            {
                for (int k = j + 1; k < rooms.Count; k++)
                {
                    Room roomA = rooms[j];
                    Room roomB = rooms[k];
                    float minDistanceX = (roomA.size.x + roomB.size.x) / 2f + roomSpacing;
                    float minDistanceY = (roomA.size.y + roomB.size.y) / 2f + roomSpacing;
                    Vector2 delta = roomA.center - roomB.center;
                    if (Mathf.Abs(delta.x) < minDistanceX && Mathf.Abs(delta.y) < minDistanceY)
                    {
                        if (delta == Vector2.zero) delta = Random.insideUnitCircle;
                        roomA.center += delta.normalized * 0.5f;
                        roomB.center -= delta.normalized * 0.5f;
                    }
                }
            }
        }
    }
    #endregion

    #region Floor and Portal Generation
    private (HashSet<Vector2Int> portalPositions, List<Tuple<Room, Room>> connections) ConnectAdjacentRooms(
      Dictionary<Vector2Int, Room> roomGrid,
      Dictionary<Room, HashSet<Vector2Int>> roomFloorData,
      HashSet<Vector2Int> allFloorPositions)
    {
        var portalPositions = new HashSet<Vector2Int>();
        var connections = new List<Tuple<Room, Room>>();
        var connectionsMade = new HashSet<Tuple<Vector2Int, Vector2Int>>();

        foreach (var roomA in roomGrid.Values)
        {
            foreach (var direction in WallGenerator.Direction2D.cardinalDirectionsList)
            {
                Vector2Int neighborGridPos = roomA.gridPos + direction;
                if (roomGrid.TryGetValue(neighborGridPos, out Room roomB))
                {
                    var connectionTuple = roomA.gridPos.x < roomB.gridPos.x || (roomA.gridPos.x == roomB.gridPos.x && roomA.gridPos.y < roomB.gridPos.y) ?
                        Tuple.Create(roomA.gridPos, roomB.gridPos) : Tuple.Create(roomB.gridPos, roomA.gridPos);

                    if (connectionsMade.Contains(connectionTuple)) continue;

                    var portalTilesA = FindBestPortalTiles(roomFloorData[roomA], direction, allFloorPositions);
                    var portalTilesB = FindBestPortalTiles(roomFloorData[roomB], -direction, allFloorPositions);

                    if (portalTilesA.Count > 0 && portalTilesB.Count > 0)
                    {
                        Vector3Int centerA = (Vector3Int)portalTilesA[portalTilesA.Count / 2];
                        Vector3Int centerB = (Vector3Int)portalTilesB[portalTilesB.Count / 2];
                        DungeonManager.Instance.RegisterPortalPair(centerA, centerB);

                        // 방 연결 정보를 리스트에 추가합니다.
                        connections.Add(Tuple.Create(roomA, roomB));
                    }

                    portalPositions.UnionWith(portalTilesA);
                    portalPositions.UnionWith(portalTilesB);
                    connectionsMade.Add(connectionTuple);
                }
            }
        }
        return (portalPositions, connections);
    }

    private List<Vector2Int> FindBestPortalTiles(HashSet<Vector2Int> roomFloor, Vector2Int portalDirection, HashSet<Vector2Int> allFloorPositions)
    {
        Vector2Int wallCheckDirection = (portalDirection.x == 0) ? Vector2Int.right : Vector2Int.up;

        var edgeWallCandidates = new HashSet<Vector2Int>();
        foreach (var floorPos in roomFloor)
        {
            var wallPos = floorPos + portalDirection;
            if (!allFloorPositions.Contains(wallPos))
            {
                edgeWallCandidates.Add(wallPos);
            }
        }

        var checkedWalls = new HashSet<Vector2Int>();
        var lines = new List<List<Vector2Int>>();
        foreach (var wall in edgeWallCandidates)
        {
            if (checkedWalls.Contains(wall)) continue;
            var currentLine = new List<Vector2Int> { wall };
            checkedWalls.Add(wall);
            for (int i = 1; i < 20; i++) { var next = wall + wallCheckDirection * i; if (edgeWallCandidates.Contains(next)) { currentLine.Add(next); checkedWalls.Add(next); } else break; }
            for (int i = 1; i < 20; i++) { var next = wall - wallCheckDirection * i; if (edgeWallCandidates.Contains(next)) { currentLine.Add(next); checkedWalls.Add(next); } else break; }
            lines.Add(currentLine);
        }

        if (lines.Count == 0) return new List<Vector2Int>();

        List<Vector2Int> bestLine;
        var linesLongerThan8 = lines.Where(l => l.Count >= 9).ToList();
        if (linesLongerThan8.Count > 0)
        {
            bestLine = linesLongerThan8[Random.Range(0, linesLongerThan8.Count)];
        }
        else
        {
            bestLine = lines.OrderByDescending(l => l.Count).First();
        }

        if (bestLine.Count < 3)
        {
            var fallbackCenter = bestLine.Count > 0 ? bestLine[0] : edgeWallCandidates.First();
            return new List<Vector2Int> { fallbackCenter - wallCheckDirection, fallbackCenter, fallbackCenter + wallCheckDirection };
        }

        bestLine.Sort((a, b) => (a.x == b.x) ? a.y.CompareTo(b.y) : a.x.CompareTo(b.x));
        Vector2Int centerTile = bestLine[bestLine.Count / 2];

        return new List<Vector2Int>
        {
            centerTile - wallCheckDirection,
            centerTile,
            centerTile + wallCheckDirection
        };
    }

    private HashSet<Vector2Int> CreateAllRoomFloors(Dictionary<Vector2Int, Room> roomGrid, Dictionary<Room, HashSet<Vector2Int>> roomFloorData, int offset)
    {
        var totalFloor = new HashSet<Vector2Int>();
        foreach (var room in roomGrid.Values)
        {
            HashSet<Vector2Int> roomFloor;
            if (room.type == RoomType.Boss && dungeonData.bossRoomPrefab != null)
            {
                roomFloor = CreateFloorFromPrefab(room, dungeonData.bossRoomPrefab);
            }
            else
            {
                roomFloor = CreateCompoundRoom(room, offset);
            }
            roomFloorData.Add(room, roomFloor);
            totalFloor.UnionWith(roomFloor);
        }
        return totalFloor;
    }

    private HashSet<Vector2Int> CreateFloorFromPrefab(Room room, Tilemap prefabTilemap)
    {
        var floorPositions = new HashSet<Vector2Int>();
        foreach (var tilePos in prefabTilemap.cellBounds.allPositionsWithin)
        {
            if (prefabTilemap.HasTile(tilePos))
            {
                Vector2Int worldPos = (Vector2Int)Vector3Int.RoundToInt(room.center) + (Vector2Int)tilePos;
                floorPositions.Add(worldPos);
            }
        }
        return floorPositions;
    }

    private HashSet<Vector2Int> CreateCompoundRoom(Room room, int offset)
    {
        if (Random.value > dungeonData.compoundRoomChance)
            return CreateRectangularFloor(room.Bounds, offset);
        var floor = new HashSet<Vector2Int>();
        BoundsInt mainBounds = room.Bounds;
        BoundsInt rect1 = new BoundsInt(mainBounds.min, new Vector3Int(
            Random.Range(mainBounds.size.x / 2, mainBounds.size.x),
            Random.Range(mainBounds.size.y / 2, mainBounds.size.y), 1));
        floor.UnionWith(CreateRectangularFloor(rect1, offset));
        int width = Random.Range(mainBounds.size.x / 2, mainBounds.size.x);
        int height = Random.Range(mainBounds.size.y / 2, mainBounds.size.y);
        Vector3Int position = new Vector3Int(
            mainBounds.xMin + Random.Range(0, mainBounds.size.x - width),
            mainBounds.yMin + Random.Range(0, mainBounds.size.y - height), 0);
        BoundsInt rect2 = new BoundsInt(position, new Vector3Int(width, height, 1));
        floor.UnionWith(CreateRectangularFloor(rect2, offset));
        return floor;
    }

    private HashSet<Vector2Int> CreateRectangularFloor(BoundsInt roomBounds, int offset)
    {
        var floor = new HashSet<Vector2Int>();
        for (int col = offset; col < roomBounds.size.x - offset; col++)
        {
            for (int row = offset; row < roomBounds.size.y - offset; row++)
            {
                Vector2Int position = (Vector2Int)roomBounds.min + new Vector2Int(col, row);
                floor.Add(position);
            }
        }
        return floor;
    }

    #endregion
}
