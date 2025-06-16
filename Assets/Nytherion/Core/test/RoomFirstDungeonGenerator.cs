/* RoomFirstDungeonGenerator.cs
   (겹침 문제 해결 버전)
   지정된 그리드 내에서 원하는 개수만큼의 방을 무작위로 선택하여 배치하고,
   인접한 방들의 포탈을 완벽하게 일직선으로 정렬하는 기능.
*/
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Random = UnityEngine.Random;

// 상속 클래스를 dungeonTest에서 AbstractDungeonGenertor로 변경하여 불필요한 의존성 제거
public class RoomFirstDungeonGenerator : AbstractDungeonGenertor
{
    [Header("Dungeon Layout Settings")]
    [SerializeField]
    private Vector2Int gridSize = new Vector2Int(5, 5); // 방을 배치할 가상 그리드의 전체 크기
    [SerializeField]
    private int desiredNumberOfRooms = 15; // gridSize 내에서 실제로 생성할 방의 개수

    [SerializeField]
    private Vector2Int minRoomSize = new Vector2Int(8, 8); // 최소 방 크기
    [SerializeField]
    private Vector2Int maxRoomSize = new Vector2Int(15, 15); // 최대 방 크기

    [SerializeField]
    private float roomSpacing = 5f; // 방과 방 사이의 최소 간격

    // --- 추가된 부분: 겹침 해결을 위한 설정 ---
    [Header("Placement Correction")]
    [SerializeField]
    private int placementIterations = 50; // 겹침 현상 완화 시뮬레이션 반복 횟수
    // --- 여기까지 ---

    [Header("Room Style")]
    [SerializeField]
    [Range(0, 10)]
    private int offset = 1; // 방 내부 벽의 두께(여백)

    // 방의 데이터를 관리하기 위한 내부 클래스
    private class Room
    {
        public Vector2Int gridPos;
        public Vector2Int size;
        public Vector2 center; // 방의 실제 월드 좌표 중심 (정수가 아닐 수 있음)
        public BoundsInt Bounds => new BoundsInt(Vector3Int.RoundToInt(center - (Vector2)size / 2), (Vector3Int)size);

        public Room(Vector2Int gridPos, Vector2Int size)
        {
            this.gridPos = gridPos;
            this.size = size;
        }
    }

    /// <summary>
    /// 던전 생성 프로세스를 시작합니다.
    /// </summary>
    protected override void RunProceduralGeneration()
    {
        // 1. 방 데이터 생성 (그리드 위치, 크기 등)
        Dictionary<Vector2Int, Room> roomGrid = CreateRoomData();
        if (roomGrid.Count == 0) return;

        // 2. 그래프 순회를 통해 방들의 실제 월드 좌표를 배치하고 정렬합니다.
        PlaceAndAlignRooms(roomGrid);

        // 3. (추가된 단계) 배치 후 발생할 수 있는 겹침 현상을 물리 시뮬레이션으로 완화합니다.
        ResolveOverlaps(roomGrid.Values.ToList());

        // 4. 최종 배치된 방들의 바닥 타일 위치를 계산합니다.
        HashSet<Vector2Int> floorPositions = new HashSet<Vector2Int>();
        foreach (var room in roomGrid.Values)
        {
            floorPositions.UnionWith(CreateSimpleRoom(room.Bounds));
        }

        // 5. 인접한 방들을 연결하는 포탈 위치를 계산합니다.
        HashSet<Vector2Int> portalPositions = ConnectAdjacentRooms(roomGrid);

        // 6. 계산된 데이터를 기반으로 타일맵을 그립니다.
        floorPositions.UnionWith(portalPositions);
        tilemapVisualizer.PaintFloorTiles(floorPositions);
        WallGenerator.CreateWalls(floorPositions, tilemapVisualizer);
        tilemapVisualizer.PaintPortals(portalPositions);
    }

    /// <summary>
    /// 설정값을 바탕으로 Room 객체들을 생성하고 그리드에 저장합니다.
    /// </summary>
    private Dictionary<Vector2Int, Room> CreateRoomData()
    {
        Dictionary<Vector2Int, Room> roomGrid = new Dictionary<Vector2Int, Room>();
        List<Vector2Int> selectedGridPositions = SelectRandomGridPositions();

        foreach (var gridPos in selectedGridPositions)
        {
            int roomWidth = Random.Range(minRoomSize.x, maxRoomSize.x + 1);
            int roomHeight = Random.Range(minRoomSize.y, maxRoomSize.y + 1);
            roomGrid.Add(gridPos, new Room(gridPos, new Vector2Int(roomWidth, roomHeight)));
        }
        return roomGrid;
    }

    /// <summary>
    /// 전체 그리드 공간에서 원하는 개수만큼의 위치를 무작위로 선택합니다.
    /// </summary>
    private List<Vector2Int> SelectRandomGridPositions()
    {
        List<Vector2Int> allGridPositions = new List<Vector2Int>();
        for (int x = 0; x < gridSize.x; x++)
            for (int y = 0; y < gridSize.y; y++)
                allGridPositions.Add(new Vector2Int(x, y));

        int totalCells = gridSize.x * gridSize.y;
        int roomsToCreate = Mathf.Min(desiredNumberOfRooms, totalCells);

        if (desiredNumberOfRooms > totalCells)
            Debug.LogWarning($"원하는 방의 개수({desiredNumberOfRooms})가 그리드 크기({totalCells})보다 큽니다. 그리드 크기만큼만 생성합니다.");

        return allGridPositions.OrderBy(pos => Random.value).Take(roomsToCreate).ToList();
    }

    /// <summary>
    /// BFS(너비 우선 탐색) 순회를 통해 방들을 배치하고, 부모-자식 관계의 방 중심축을 정렬합니다.
    /// </summary>
    private void PlaceAndAlignRooms(Dictionary<Vector2Int, Room> roomGrid)
    {
        Queue<Room> queue = new Queue<Room>();
        HashSet<Vector2Int> visited = new HashSet<Vector2Int>();

        Room startRoom = roomGrid.Values.First();
        startRoom.center = Vector2.zero;
        queue.Enqueue(startRoom);
        visited.Add(startRoom.gridPos);

        while (queue.Count > 0)
        {
            Room parentRoom = queue.Dequeue();

            foreach (var direction in ProceduralGenerationAlgorithms.Direction2D.cardinalDirectionsList)
            {
                Vector2Int neighborGridPos = parentRoom.gridPos + direction;

                if (roomGrid.TryGetValue(neighborGridPos, out Room neighborRoom) && !visited.Contains(neighborGridPos))
                {
                    float newX, newY;
                    if (direction.x != 0)
                    {
                        newY = parentRoom.center.y;
                        float xOffset = (parentRoom.size.x + neighborRoom.size.x) / 2f + roomSpacing;
                        newX = parentRoom.center.x + direction.x * xOffset;
                    }
                    else
                    {
                        newX = parentRoom.center.x;
                        float yOffset = (parentRoom.size.y + neighborRoom.size.y) / 2f + roomSpacing;
                        newY = parentRoom.center.y + direction.y * yOffset;
                    }

                    neighborRoom.center = new Vector2(newX, newY);

                    visited.Add(neighborGridPos);
                    queue.Enqueue(neighborRoom);
                }
            }
        }
    }

    /// <summary>
    /// (새로운 함수) 배치된 방들의 겹침 현상을 완화합니다.
    /// </summary>
    private void ResolveOverlaps(List<Room> rooms)
    {
        for (int i = 0; i < placementIterations; i++)
        {
            for (int j = 0; j < rooms.Count; j++)
            {
                for (int k = j + 1; k < rooms.Count; k++)
                {
                    Room roomA = rooms[j];
                    Room roomB = rooms[k];

                    if (DoRoomsOverlap(roomA, roomB))
                    {
                        Vector2 direction = roomA.center - roomB.center;
                        if (direction == Vector2.zero)
                        {
                            direction = Random.insideUnitCircle; // 완전히 겹쳤을 경우 랜덤 방향
                        }

                        // 두 방을 서로 밀어냅니다.
                        roomA.center += direction.normalized * 0.5f;
                        roomB.center -= direction.normalized * 0.5f;
                    }
                }
            }
        }
    }

    /// <summary>
    /// (새로운 함수) 두 방이 겹치는지 확인합니다.
    /// </summary>
    private bool DoRoomsOverlap(Room roomA, Room roomB)
    {
        // 두 방의 중심 사이의 최소 거리를 계산합니다. (간격 포함)
        float minDistanceX = (roomA.size.x + roomB.size.x) / 2f + roomSpacing;
        float minDistanceY = (roomA.size.y + roomB.size.y) / 2f + roomSpacing;

        // 실제 두 방의 중심 사이의 거리를 계산합니다.
        float deltaX = Mathf.Abs(roomA.center.x - roomB.center.x);
        float deltaY = Mathf.Abs(roomA.center.y - roomB.center.y);

        // 실제 거리가 최소 거리보다 작으면 겹친 것으로 판단합니다.
        return deltaX < minDistanceX && deltaY < minDistanceY;
    }

    /// <summary>
    /// 그리드 상에서 인접한 방들의 벽에 포탈 위치를 계산합니다.
    /// </summary>
    private HashSet<Vector2Int> ConnectAdjacentRooms(Dictionary<Vector2Int, Room> roomGrid)
    {
        HashSet<Vector2Int> portalPositions = new HashSet<Vector2Int>();

        foreach (var item in roomGrid)
        {
            Vector2Int gridPos = item.Key;
            Room room = item.Value;

            if (roomGrid.ContainsKey(gridPos + Vector2Int.right))
            {
                portalPositions.Add(new Vector2Int(room.Bounds.xMax - 1, (int)room.center.y));
            }
            if (roomGrid.ContainsKey(gridPos + Vector2Int.left))
            {
                portalPositions.Add(new Vector2Int(room.Bounds.xMin, (int)room.center.y));
            }
            if (roomGrid.ContainsKey(gridPos + Vector2Int.up))
            {
                portalPositions.Add(new Vector2Int((int)room.center.x, room.Bounds.yMax - 1));
            }
            if (roomGrid.ContainsKey(gridPos + Vector2Int.down))
            {
                portalPositions.Add(new Vector2Int((int)room.center.x, room.Bounds.yMin));
            }
        }
        return portalPositions;
    }

    /// <summary>
    /// 주어진 경계(Bounds) 내에 사각형 모양의 바닥 타일 위치들을 생성합니다.
    /// </summary>
    private HashSet<Vector2Int> CreateSimpleRoom(BoundsInt roomBounds)
    {
        HashSet<Vector2Int> floor = new HashSet<Vector2Int>();
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
}
