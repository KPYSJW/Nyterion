using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class TilemapVisualizer : MonoBehaviour
{
    [SerializeField]
    private Tilemap floorTilemap, wallTilemap;
    [SerializeField]
    private TileBase floorTile,Walltop;


    public void PaintFloorTiles(IEnumerable<Vector2Int> floorPosition)
    {
        PaintFloorTiles(floorPosition, floorTilemap, floorTile);
       
    }

    private void PaintFloorTiles(IEnumerable<Vector2Int> Positions, Tilemap Tilemap, TileBase Tile)
    {
        foreach(var position in Positions)
        {
            PaintSingleTile(Tilemap, Tile, position);
        }
    }

    private void PaintSingleTile(Tilemap Tilemap, TileBase Tile, Vector2Int position)
    {
        var tilePosition = Tilemap.WorldToCell((Vector3Int)position);
        Tilemap.SetTile(tilePosition, Tile);
    }

    public void Clear()
    {
        floorTilemap.ClearAllTiles();
        wallTilemap.ClearAllTiles();
    }

    internal void PaintSingleBasicWall(Vector2Int position)
    {
        PaintSingleTile(wallTilemap,Walltop,position);
    }
}
