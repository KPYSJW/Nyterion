using System;
using System.Collections.Generic;
using UnityEngine;
using Nytherion.GamePlay.Engravings;

namespace Nytherion.GamePlay.Engravings
{
    public class EngravingGrid
    {

        public int Rows { get; private set; }
        public int Columns { get; private set; }
        private EngravingBlock[,] grid;
        private Dictionary<string, Vector2Int> blockPositions = new();


        public EngravingGrid(int rows, int columns)
        {
            if (rows <= 0 || columns <= 0)
                throw new ArgumentException("행과 열은 0보다 커야 합니다.");

            Rows = rows;
            Columns = columns;
            grid = new EngravingBlock[rows, columns];
        }


        public bool CanPlaceBlock(EngravingBlock block, int row, int col)
        {
            foreach (Vector2Int offset in block.Shape)
            {
                int x = col + offset.x;
                int y = row + offset.y;

                if (x < 0 || x >= Columns || y < 0 || y >= Rows)
                {
                    Debug.LogWarning($"[CanPlaceBlock] Out of Bounds: GridPos({y}, {x}) for Block '{block.BlockId}'");
                    return false;
                }

                if (grid[y, x] != null)
                {
                    Debug.LogWarning($"[CanPlaceBlock] Occupied: GridPos({y}, {x}) is already taken by Block '{grid[y, x].BlockId}'");
                    return false;
                }
            }

            return true;
        }


        public bool PlaceBlock(EngravingBlock block, int row, int col)
        {
            foreach (var offset in block.Shape)
            {
                int y = row + offset.y;
                int x = col + offset.x;

                if (x < 0 || x >= Columns || y < 0 || y >= Rows)
                    return false;

                if (grid[y, x] != null)
                    return false;
            }

            foreach (var offset in block.Shape)
            {
                int y = row + offset.y;
                int x = col + offset.x;
                grid[y, x] = block;
            }

            blockPositions[block.BlockId] = new Vector2Int(col, row);
            return true;
        }

        public bool RemoveBlock(string blockId)
        {
            if (!blockPositions.TryGetValue(blockId, out var position))
                return false;

            var block = grid[position.y, position.x];
            if (block == null)
                return false;

            foreach (var cell in block.GetOccupiedCells(position))
            {
                if (cell.x >= 0 && cell.x < Columns && cell.y >= 0 && cell.y < Rows)
                {
                    grid[cell.y, cell.x] = null;
                }
            }

            blockPositions.Remove(blockId);
            return true;
        }

        public EngravingBlock GetBlockAt(int row, int col)
        {
            if (row < 0 || row >= Rows || col < 0 || col >= Columns)
                return null;

            return grid[row, col];
        }

        public bool TryGetBlockPosition(string blockId, out Vector2Int position)
        {
            return blockPositions.TryGetValue(blockId, out position);
        }

        public void Clear()
        {
            Array.Clear(grid, 0, grid.Length);
            blockPositions.Clear();
        }

        public IEnumerable<Vector2Int> GetEmptyCells()
        {
            for (int row = 0; row < Rows; row++)
            {
                for (int col = 0; col < Columns; col++)
                {
                    if (grid[row, col] == null)
                    {
                        yield return new Vector2Int(col, row);
                    }
                }
            }
        }
    }
}
