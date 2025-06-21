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
        private readonly EngravingBlock[,] _grid;


        public EngravingGrid(int rows, int columns)
        {
            Rows = rows;
            Columns = columns;
            _grid = new EngravingBlock[rows, columns];
        }

        public bool CanPlaceBlock(EngravingBlock block, int row, int col)
        {
            foreach (var offset in block.Shape)
            {
                int x = col + offset.x;
                int y = row + offset.y;
                if (x < 0 || x >= Columns || y < 0 || y >= Rows || _grid[y, x] != null)
                {
                    return false;
                }
            }
            return true;
        }

        public void PlaceBlock(EngravingBlock block, int row, int col)
        {
            if (block != null)
            {
                foreach (var offset in block.Shape)
                {
                    _grid[row + offset.y, col + offset.x] = block;
                }
            }
            else // block이 null이면 해당 칸만 비움
            {
                if (col >= 0 && col < Columns && row >= 0 && row < Rows)
                {
                    _grid[row, col] = null;
                }
            }
        }

        public EngravingBlock GetBlockAt(int row, int col)
        {
            if (row < 0 || row >= Rows || col < 0 || col >= Columns) return null;
            return _grid[row, col];
        }

        public void Clear()
        {
            Array.Clear(_grid, 0, _grid.Length);
        }
    }
}
