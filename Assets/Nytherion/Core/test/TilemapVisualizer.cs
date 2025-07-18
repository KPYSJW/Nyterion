using Nytherion.GamePlay.Dungeon;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;

public class TilemapVisualizer : MonoBehaviour
{
    [Header("Tilemap Settings")]
    [SerializeField] public Tilemap floorTilemap;
    [SerializeField] public Tilemap wallTilemap;
    [SerializeField] public Tilemap portalTilemap;

    [Header("Object Holders")]
    [Tooltip(" ֹ Ʈ ϴ.")]
    [SerializeField] private Transform obstacleHolder;

    [Header("Tile Assets")]
    [SerializeField] private TileBase floorTile;
    [SerializeField] private TileBase portalTile;



    [Header("벽 타일 세트")]
    public RuleTile wallRuleTile;

    [Header("Special Room Tile Assets")]
    [SerializeField] private TileBase startRoomTile;
    [SerializeField] private TileBase shopRoomTile;
    [SerializeField] private TileBase itemRoomTile;


    public void InstantiateObstacles(List<RoomFirstDungeonGenerator.PlacedObstacleData> obstaclesToPlace)
    {
        if (obstacleHolder != null)
        {
            foreach (Transform child in obstacleHolder)
            {
                if (Application.isPlaying) Destroy(child.gameObject);
                else DestroyImmediate(child.gameObject);
            }
        }
        if (obstaclesToPlace == null) return;
        foreach (var obstacleData in obstaclesToPlace)
        {
            if (obstacleData.prefab != null)
            {
                Instantiate(obstacleData.prefab, obstacleData.worldPosition, Quaternion.identity, obstacleHolder);
            }
        }
    }


    public void PaintFloorTiles(IEnumerable<Vector2Int> floorPositions)
    {
        PaintTiles(floorPositions, floorTilemap, floorTile);
    }


    public void PaintWallsWithRuleTile(IEnumerable<Vector2Int> wallPositions)
    {
        if (wallRuleTile == null)
        {
            Debug.LogError("Wall Rule Tile이 할당되지 않았습니다!");
            return;
        }

        foreach (var position in wallPositions)
        {
            PaintSingleTile(position, wallTilemap, wallRuleTile);
        }
    }



    public void PaintPortals(IEnumerable<Vector2Int> portalPositions)
    {
        PaintTiles(portalPositions, portalTilemap, portalTile);
    }

    
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


    private void PaintTiles(IEnumerable<Vector2Int> positions, Tilemap tilemap, TileBase tile)
    {
        if (tile == null || tilemap == null || !positions.Any()) return;

        // 1. Vector2Int 컬렉션을 Vector3Int 배열로 변환
        Vector3Int[] positionArray = positions.Select(pos => (Vector3Int)pos).ToArray();

        // 2. 타일 배열 생성
        TileBase[] tileArray = Enumerable.Repeat(tile, positionArray.Length).ToArray();

        // 3. SetTiles 메서드로 한 번에 그리기
        tilemap.SetTiles(positionArray, tileArray);
    }

    private void PaintSingleTile(Vector2Int position, Tilemap tilemap, TileBase tile)
    {
        var tilePosition = tilemap.WorldToCell((Vector3Int)position);
        tilemap.SetTile(tilePosition, tile);
    }

    public void Clear()
    {
        floorTilemap?.ClearAllTiles();
        wallTilemap?.ClearAllTiles();
        portalTilemap?.ClearAllTiles();

        if (obstacleHolder != null)
        {
            for (int i = obstacleHolder.childCount - 1; i >= 0; i--)
            {
                Transform child = obstacleHolder.GetChild(i);
                if (Application.isPlaying) Destroy(child.gameObject);
                else DestroyImmediate(child.gameObject);
            }
        }
    }



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