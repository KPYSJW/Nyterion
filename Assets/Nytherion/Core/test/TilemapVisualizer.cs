/* TilemapVisualizer.cs
   (특수방 그리기 로직 통합 버전)
   - 보스방 프리팹을 포함한 모든 특수방의 그리기를 이 스크립트에서 담당하도록 변경했습니다.
   - PaintSpecialRoomFloors 메서드가 방의 데이터와 프리팹 정보를 직접 받아 처리합니다.
*/
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
// --- 추가된 부분 ---
// DungeonData를 참조하여 프리팹 정보를 가져오기 위해 네임스페이스 추가가 필요할 수 있습니다.
// (DungeonData 스크립트의 클래스 정의에 따라 다름)
// --- 여기까지 ---

public class TilemapVisualizer : MonoBehaviour
{
    [Header("Tilemap Settings")]
    [SerializeField] private Tilemap floorTilemap;
    [SerializeField] private Tilemap wallTilemap;
    [SerializeField] private Tilemap portalTilemap;

    [Header("Tile Assets")]
    [SerializeField] private TileBase floorTile;
    [SerializeField] private TileBase wallTile;
    [SerializeField] private TileBase portalTile;

    [Header("Special Room Tile Assets")]
    [SerializeField] private TileBase startRoomTile;
    [SerializeField] private TileBase shopRoomTile;
    [SerializeField] private TileBase itemRoomTile;

    // ... (PaintFloorTiles, PaintWallTiles, PaintPortals 등 다른 함수는 그대로) ...
    public void PaintFloorTiles(IEnumerable<Vector2Int> floorPositions)
    {
        PaintTiles(floorPositions, floorTilemap, floorTile);
    }

    public void PaintWallTiles(IEnumerable<Vector2Int> wallPositions)
    {
        PaintTiles(wallPositions, wallTilemap, wallTile);
    }

    public void PaintPortals(IEnumerable<Vector2Int> portalPositions)
    {
        PaintTiles(portalPositions, portalTilemap, portalTile);
    }

    public void PaintSingleFloorTile(Vector3Int position, TileBase tile)
    {
        floorTilemap.SetTile(position, tile);
    }


    // --- 변경된 부분 ---

    /// <summary>
    /// 모든 특수 방(프리팹 포함)을 종류에 맞게 그립니다.
    /// </summary>
    /// <param name="specialRooms">그려야 할 특수 방 객체 리스트</param>
    /// <param name="dungeonData">프리팹 정보를 담고 있는 DungeonData</param>
    /// <param name="roomFloorData">절차적으로 생성된 방의 바닥 위치 정보</param>
    public void PaintSpecialRoomFloors(
        List<RoomFirstDungeonGenerator.Room> specialRooms,
        DungeonData dungeonData,
        Dictionary<RoomFirstDungeonGenerator.Room, HashSet<Vector2Int>> roomFloorData)
    {
        // 모든 특수방을 순회하며 하나씩 그립니다.
        foreach (var room in specialRooms)
        {
            // 만약 현재 방이 보스방이고, 보스방 프리팹이 할당되어 있다면,
            if (room.type == RoomFirstDungeonGenerator.RoomType.Boss && dungeonData.bossRoomPrefab != null)
            {
                // 프리팹 그리기 전용 함수를 호출합니다.
                PaintPrefab(room.center, dungeonData.bossRoomPrefab);
            }
            else // 보스방이 아니거나 프리팹이 없는 다른 특수방의 경우
            {
                // 방 타입에 맞는 타일을 선택합니다.
                TileBase tileToUse = GetTileForRoomType(room.type);
                if (tileToUse != null)
                {
                    // 해당 방의 바닥 위치 정보를 가져와서 타일을 칠합니다.
                    HashSet<Vector2Int> floorPositions = roomFloorData[room];
                    PaintTiles(floorPositions, floorTilemap, tileToUse);
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
                return null; // 보스방이나 일반방은 다른 방식으로 처리되므로 null 반환
        }
    }

    /// <summary>
    /// 지정된 위치에 프리팹 타일맵을 그대로 복사해서 그립니다.
    /// </summary>
    /// <param name="roomCenter">프리팹이 그려질 중심 월드 좌표</param>
    /// <param name="prefabTilemap">복사할 타일 정보가 담긴 프리팹</param>
    private void PaintPrefab(Vector2 roomCenter, Tilemap prefabTilemap)
    {
        // 프리팹의 모든 타일을 순회합니다.
        foreach (var tilePos in prefabTilemap.cellBounds.allPositionsWithin)
        {
            TileBase tile = prefabTilemap.GetTile(tilePos);
            if (tile != null)
            {
                // 프리팹의 로컬 타일 위치를 월드 위치로 변환하여 그립니다.
                Vector3Int worldPos = Vector3Int.RoundToInt(roomCenter) + tilePos;
                floorTilemap.SetTile(worldPos, tile);
            }
        }
    }
    // --- 여기까지 ---

    private void PaintTiles(IEnumerable<Vector2Int> positions, Tilemap tilemap, TileBase tile)
    {
        if (tile == null || tilemap == null) return;
        foreach (var position in positions)
        {
            var tilePosition = tilemap.WorldToCell((Vector3Int)position);
            tilemap.SetTile(tilePosition, tile);
        }
    }

    public void Clear()
    {
        floorTilemap?.ClearAllTiles();
        wallTilemap?.ClearAllTiles();
        portalTilemap?.ClearAllTiles();
    }
}