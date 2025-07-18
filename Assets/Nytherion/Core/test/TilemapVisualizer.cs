using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using Nytherion.GamePlay.Dungeon;

public class TilemapVisualizer : MonoBehaviour
{
    [Header("Tilemap Settings")]
    [SerializeField] private Tilemap floorTilemap;
    [SerializeField] private Tilemap wallTilemap;
    [SerializeField] private Tilemap portalTilemap;

    [Header("Object Holders")]
    [Tooltip(" ֹ Ʈ ϴ.")]
    [SerializeField] private Transform obstacleHolder;

    [Header("Tile Assets")]
    [SerializeField] private TileBase floorTile;
    [SerializeField] private TileBase wallTile;
    [SerializeField] private TileBase portalTile;

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

    
    public void PaintWallTiles(IEnumerable<Vector2Int> wallPositions)
    {
        PaintTiles(wallPositions, wallTilemap, wallTile);
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

        
        if (obstacleHolder != null)
        {
            
            for (int i = obstacleHolder.childCount - 1; i >= 0; i--)
            {
                Transform child = obstacleHolder.GetChild(i);
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