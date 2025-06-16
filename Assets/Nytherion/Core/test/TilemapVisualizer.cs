/* TilemapVisualizer.cs
   ��Ż�� �׸� Ÿ�ϸʰ� �׸��� �Լ��� �߰��� �����Դϴ�.
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

    // --- �߰��� �κ� ---
    [Header("Portal Settings")]
    [SerializeField]
    private Tilemap portalTilemap; // ��Ż�� �׸� Ÿ�ϸ� (Inspector���� �Ҵ� �ʿ�)
    [SerializeField]
    private TileBase portalTile;   // ��Ż Ÿ�� (Inspector���� �Ҵ� �ʿ�)
    // --- ������� ---

    [SerializeField]
    private TileBase floorTile, Walltop;


    public void PaintFloorTiles(IEnumerable<Vector2Int> floorPositions)
    {
        PaintTiles(floorPositions, floorTilemap, floorTile);
    }

    // --- �߰��� �Լ� ---
    public void PaintPortals(IEnumerable<Vector2Int> portalPositions)
    {
        PaintTiles(portalPositions, portalTilemap, portalTile);
    }
    // --- ������� ---

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
            portalTilemap.ClearAllTiles(); // --- �߰�: Ŭ���� �� ��Ż Ÿ�ϸʵ� �ʱ�ȭ
    }

    internal void PaintSingleBasicWall(Vector2Int position)
    {
        PaintSingleTile(wallTilemap, Walltop, position);
    }
}
