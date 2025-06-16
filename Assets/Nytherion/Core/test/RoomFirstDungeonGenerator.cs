/* RoomFirstDungeonGenerator.cs
   (��ħ ���� �ذ� ����)
   ������ �׸��� ������ ���ϴ� ������ŭ�� ���� �������� �����Ͽ� ��ġ�ϰ�,
   ������ ����� ��Ż�� �Ϻ��ϰ� ���������� �����ϴ� ���.
*/
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Random = UnityEngine.Random;

// ��� Ŭ������ dungeonTest���� AbstractDungeonGenertor�� �����Ͽ� ���ʿ��� ������ ����
public class RoomFirstDungeonGenerator : AbstractDungeonGenertor
{
    [Header("Dungeon Layout Settings")]
    [SerializeField]
    private Vector2Int gridSize = new Vector2Int(5, 5); // ���� ��ġ�� ���� �׸����� ��ü ũ��
    [SerializeField]
    private int desiredNumberOfRooms = 15; // gridSize ������ ������ ������ ���� ����

    [SerializeField]
    private Vector2Int minRoomSize = new Vector2Int(8, 8); // �ּ� �� ũ��
    [SerializeField]
    private Vector2Int maxRoomSize = new Vector2Int(15, 15); // �ִ� �� ũ��

    [SerializeField]
    private float roomSpacing = 5f; // ��� �� ������ �ּ� ����

    // --- �߰��� �κ�: ��ħ �ذ��� ���� ���� ---
    [Header("Placement Correction")]
    [SerializeField]
    private int placementIterations = 50; // ��ħ ���� ��ȭ �ùķ��̼� �ݺ� Ƚ��
    // --- ������� ---

    [Header("Room Style")]
    [SerializeField]
    [Range(0, 10)]
    private int offset = 1; // �� ���� ���� �β�(����)

    // ���� �����͸� �����ϱ� ���� ���� Ŭ����
    private class Room
    {
        public Vector2Int gridPos;
        public Vector2Int size;
        public Vector2 center; // ���� ���� ���� ��ǥ �߽� (������ �ƴ� �� ����)
        public BoundsInt Bounds => new BoundsInt(Vector3Int.RoundToInt(center - (Vector2)size / 2), (Vector3Int)size);

        public Room(Vector2Int gridPos, Vector2Int size)
        {
            this.gridPos = gridPos;
            this.size = size;
        }
    }

    /// <summary>
    /// ���� ���� ���μ����� �����մϴ�.
    /// </summary>
    protected override void RunProceduralGeneration()
    {
        // 1. �� ������ ���� (�׸��� ��ġ, ũ�� ��)
        Dictionary<Vector2Int, Room> roomGrid = CreateRoomData();
        if (roomGrid.Count == 0) return;

        // 2. �׷��� ��ȸ�� ���� ����� ���� ���� ��ǥ�� ��ġ�ϰ� �����մϴ�.
        PlaceAndAlignRooms(roomGrid);

        // 3. (�߰��� �ܰ�) ��ġ �� �߻��� �� �ִ� ��ħ ������ ���� �ùķ��̼����� ��ȭ�մϴ�.
        ResolveOverlaps(roomGrid.Values.ToList());

        // 4. ���� ��ġ�� ����� �ٴ� Ÿ�� ��ġ�� ����մϴ�.
        HashSet<Vector2Int> floorPositions = new HashSet<Vector2Int>();
        foreach (var room in roomGrid.Values)
        {
            floorPositions.UnionWith(CreateSimpleRoom(room.Bounds));
        }

        // 5. ������ ����� �����ϴ� ��Ż ��ġ�� ����մϴ�.
        HashSet<Vector2Int> portalPositions = ConnectAdjacentRooms(roomGrid);

        // 6. ���� �����͸� ������� Ÿ�ϸ��� �׸��ϴ�.
        floorPositions.UnionWith(portalPositions);
        tilemapVisualizer.PaintFloorTiles(floorPositions);
        WallGenerator.CreateWalls(floorPositions, tilemapVisualizer);
        tilemapVisualizer.PaintPortals(portalPositions);
    }

    /// <summary>
    /// �������� �������� Room ��ü���� �����ϰ� �׸��忡 �����մϴ�.
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
    /// ��ü �׸��� �������� ���ϴ� ������ŭ�� ��ġ�� �������� �����մϴ�.
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
            Debug.LogWarning($"���ϴ� ���� ����({desiredNumberOfRooms})�� �׸��� ũ��({totalCells})���� Ů�ϴ�. �׸��� ũ�⸸ŭ�� �����մϴ�.");

        return allGridPositions.OrderBy(pos => Random.value).Take(roomsToCreate).ToList();
    }

    /// <summary>
    /// BFS(�ʺ� �켱 Ž��) ��ȸ�� ���� ����� ��ġ�ϰ�, �θ�-�ڽ� ������ �� �߽����� �����մϴ�.
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
    /// (���ο� �Լ�) ��ġ�� ����� ��ħ ������ ��ȭ�մϴ�.
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
                            direction = Random.insideUnitCircle; // ������ ������ ��� ���� ����
                        }

                        // �� ���� ���� �о���ϴ�.
                        roomA.center += direction.normalized * 0.5f;
                        roomB.center -= direction.normalized * 0.5f;
                    }
                }
            }
        }
    }

    /// <summary>
    /// (���ο� �Լ�) �� ���� ��ġ���� Ȯ���մϴ�.
    /// </summary>
    private bool DoRoomsOverlap(Room roomA, Room roomB)
    {
        // �� ���� �߽� ������ �ּ� �Ÿ��� ����մϴ�. (���� ����)
        float minDistanceX = (roomA.size.x + roomB.size.x) / 2f + roomSpacing;
        float minDistanceY = (roomA.size.y + roomB.size.y) / 2f + roomSpacing;

        // ���� �� ���� �߽� ������ �Ÿ��� ����մϴ�.
        float deltaX = Mathf.Abs(roomA.center.x - roomB.center.x);
        float deltaY = Mathf.Abs(roomA.center.y - roomB.center.y);

        // ���� �Ÿ��� �ּ� �Ÿ����� ������ ��ģ ������ �Ǵ��մϴ�.
        return deltaX < minDistanceX && deltaY < minDistanceY;
    }

    /// <summary>
    /// �׸��� �󿡼� ������ ����� ���� ��Ż ��ġ�� ����մϴ�.
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
    /// �־��� ���(Bounds) ���� �簢�� ����� �ٴ� Ÿ�� ��ġ���� �����մϴ�.
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
