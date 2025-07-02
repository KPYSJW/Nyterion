/* TilemapVisualizer.cs (함수 시그니처 수정)

    [역할]
    던전 데이터를 기반으로 실제 타일과 오브젝트를 그리는 작업을 전담.

    [핵심 변경점]
    - (버그 수정) InstantiateObstacles() 함수가 새로운 데이터 구조인
      'PlacedObstacleData' 리스트를 받도록 수정하여 컴파일 오류를 해결했습니다.
*/
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class TilemapVisualizer : MonoBehaviour
{
    [Header("Tilemap Settings")]
    [SerializeField] private Tilemap floorTilemap;
    [SerializeField] private Tilemap wallTilemap;
    [SerializeField] private Tilemap portalTilemap;

    [Header("Object Holders")]
    [Tooltip("생성된 장애물들을 담아둘 부모 오브젝트입니다.")]
    [SerializeField] private Transform obstacleHolder;

    [Header("Tile Assets")]
    [SerializeField] private TileBase floorTile;
    [SerializeField] private TileBase wallTile;
    [SerializeField] private TileBase portalTile;

    [Header("Special Room Tile Assets")]
    [SerializeField] private TileBase startRoomTile;
    [SerializeField] private TileBase shopRoomTile;
    [SerializeField] private TileBase itemRoomTile;

    /// <summary>
    /// 주어진 위치들에 장애물 게임 오브젝트를 생성합니다.
    /// </summary>
    public void InstantiateObstacles(List<RoomFirstDungeonGenerator.PlacedObstacleData> obstaclesToPlace)
    {
        // 이전에 생성된 장애물이 있다면 모두 삭제
        // obstacleHolder가 null일 경우를 대비한 안전장치 추가
        if (obstacleHolder != null)
        {
            foreach (Transform child in obstacleHolder)
            {
                if (Application.isPlaying)
                {
                    Destroy(child.gameObject);
                }
                else
                {
                    DestroyImmediate(child.gameObject);
                }
            }
        }

        if (obstaclesToPlace == null) return;

        // 새로운 장애물 생성
        foreach (var obstacleData in obstaclesToPlace)
        {
            // 프리팹이 null이 아닐 때만 생성
            if (obstacleData.prefab != null)
            {
                Instantiate(obstacleData.prefab, obstacleData.worldPosition, Quaternion.identity, obstacleHolder);
            }
        }
    }

    /// <summary>
    /// 주어진 위치들에 일반 바닥 타일을 그립니다.
    /// </summary>
    public void PaintFloorTiles(IEnumerable<Vector2Int> floorPositions)
    {
        PaintTiles(floorPositions, floorTilemap, floorTile);
    }

    /// <summary>
    /// 주어진 위치들에 벽 타일을 그립니다.
    /// </summary>
    public void PaintWallTiles(IEnumerable<Vector2Int> wallPositions)
    {
        PaintTiles(wallPositions, wallTilemap, wallTile);
    }

    /// <summary>
    /// 주어진 위치들에 포탈 타일을 그립니다.
    /// </summary>
    public void PaintPortals(IEnumerable<Vector2Int> portalPositions)
    {
        PaintTiles(portalPositions, portalTilemap, portalTile);
    }

    /// <summary>
    /// 특수 방의 바닥을 종류에 맞는 타일로 칠합니다.
    /// </summary>
    public void PaintSpecialRoomFloors(
        List<RoomFirstDungeonGenerator.Room> specialRooms,
        DungeonData dungeonData,
        Dictionary<RoomFirstDungeonGenerator.Room, HashSet<Vector2Int>> roomFloorData)
    {
        foreach (var room in specialRooms)
        {
            if (room.type == RoomFirstDungeonGenerator.RoomType.Boss && dungeonData.bossRoomPrefab != null)
            {
                PaintPrefab(room.center, dungeonData.bossRoomPrefab);
            }
            else
            {
                TileBase tileToUse = GetTileForRoomType(room.type);
                if (tileToUse != null && roomFloorData.TryGetValue(room, out var floorPositions))
                {
                    PaintTiles(floorPositions, floorTilemap, tileToUse);
                }
            }
        }
    }

    /// <summary>
    /// 지정된 위치 목록에 특정 타일을 그리는 범용 함수입니다.
    /// </summary>
    private void PaintTiles(IEnumerable<Vector2Int> positions, Tilemap tilemap, TileBase tile)
    {
        if (tile == null || tilemap == null) return;
        foreach (var position in positions)
        {
            var tilePosition = tilemap.WorldToCell((Vector3Int)position);
            tilemap.SetTile(tilePosition, tile);
        }
    }

    /// <summary>
    /// 모든 타일맵의 타일을 지우고, 생성된 오브젝트를 삭제합니다.
    /// </summary>
    public void Clear()
    {
        floorTilemap?.ClearAllTiles();
        wallTilemap?.ClearAllTiles();
        portalTilemap?.ClearAllTiles();

        // 장애물 게임 오브젝트도 함께 삭제
        if (obstacleHolder != null)
        {
            // 루프를 도는 동안 자식 개수가 변할 수 있으므로 뒤에서부터 제거하는 것이 안전합니다.
            for (int i = obstacleHolder.childCount - 1; i >= 0; i--)
            {
                Transform child = obstacleHolder.GetChild(i);
                if (Application.isPlaying)
                {
                    Destroy(child.gameObject);
                }
                else
                {
                    // 에디터 모드에서는 DestroyImmediate를 사용해야 바로 삭제됩니다.
                    DestroyImmediate(child.gameObject);
                }
            }
        }
    }

    /// <summary>
    /// 방 타입에 따라 사용할 타일을 반환하는 헬퍼 함수입니다.
    /// </summary>
    private TileBase GetTileForRoomType(RoomFirstDungeonGenerator.RoomType type)
    {
        switch (type)
        {
            case RoomFirstDungeonGenerator.RoomType.Start:
                return startRoomTile;
            case RoomFirstDungeonGenerator.RoomType.Shop:
                return shopRoomTile;
            case RoomFirstDungeonGenerator.RoomType.Item:
                return itemRoomTile;
            default:
                return null;
        }
    }

    /// <summary>
    /// 지정된 위치에 프리팹 타일맵을 그대로 복사해서 그립니다.
    /// </summary>
    private void PaintPrefab(Vector2 roomCenter, Tilemap prefabTilemap)
    {
        foreach (var tilePos in prefabTilemap.cellBounds.allPositionsWithin)
        {
            TileBase tile = prefabTilemap.GetTile(tilePos);
            if (tile != null)
            {
                Vector3Int worldPos = Vector3Int.RoundToInt(roomCenter) + tilePos;
                floorTilemap.SetTile(worldPos, tile);
            }
        }
    }
}
