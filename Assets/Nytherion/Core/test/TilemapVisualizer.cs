/* TilemapVisualizer.cs
   포탈을 그릴 타일맵과 그리는 함수가 추가된 버전입니다.
*/
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class TilemapVisualizer : MonoBehaviour
{
    [SerializeField]
    private Tilemap floorTilemap, wallTilemap;

    // --- 추가된 부분 ---
    [Header("Portal Settings")]
    [SerializeField]
    private Tilemap portalTilemap; // 포탈을 그릴 타일맵 (Inspector에서 할당 필요)
    [SerializeField]
    private TileBase portalTile;   // 포탈 타일 (Inspector에서 할당 필요)
    // --- 여기까지 ---

    [SerializeField]
    private TileBase floorTile, Walltop;


    public void PaintFloorTiles(IEnumerable<Vector2Int> floorPositions)
    {
        PaintTiles(floorPositions, floorTilemap, floorTile);
    }

    // --- 추가된 함수 ---
    public void PaintPortals(IEnumerable<Vector2Int> portalPositions)
    {
        PaintTiles(portalPositions, portalTilemap, portalTile);
    }
    // --- 여기까지 ---

    private void PaintTiles(IEnumerable<Vector2Int> positions, Tilemap tilemap, TileBase tile)
    {
        foreach (var position in positions)
        {
            PaintSingleTile(tilemap, tile, position);
        }
    }

    private void PaintSingleTile(Tilemap tilemap, TileBase tile, Vector2Int position)
    {
        var tilePosition = tilemap.WorldToCell((Vector3Int)position);
        tilemap.SetTile(tilePosition, tile);
    }

    public void Clear()
    {
        floorTilemap.ClearAllTiles();
        wallTilemap.ClearAllTiles();
        if (portalTilemap != null)
            portalTilemap.ClearAllTiles(); // --- 추가: 클리어 시 포탈 타일맵도 초기화
    }

    internal void PaintSingleBasicWall(Vector2Int position)
    {
        PaintSingleTile(wallTilemap, Walltop, position);
    }
}
