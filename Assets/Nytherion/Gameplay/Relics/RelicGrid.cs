using System;
using System.Collections.Generic;
using Nytherion.Data.ScriptableObjects.Relics;
using UnityEngine;

namespace Nytherion.GamePlay.Relics
{
    public enum InfluenceType { None, LevelUp, LevelDown }

    public class RelicGrid
    {
        public int Rows { get; private set; }
        public int Columns { get; private set; }
        private readonly RelicBlock[,] grid;
        private readonly InfluenceType[,] influenceGrid;

        public RelicGrid(int rows, int columns)
        {
            Rows = rows;
            Columns = columns;
            grid = new RelicBlock[rows, columns];
            influenceGrid = new InfluenceType[rows, columns];
        }

        public void PlaceBlock(RelicBlock block, int row, int col)
        {
            if (!IsPositionValid(row, col)) return;
            grid[row, col] = block;
        }

        public void ClearBlockAt(int row, int col)
        {
            if (!IsPositionValid(row, col)) return;
            grid[row, col] = null;
        }

        public void RecalculateAllInfluences()
        {
            Array.Clear(influenceGrid, 0, influenceGrid.Length);
            foreach (var block in grid)
            {
                if (block != null)
                {
                    block.ResetLevel();
                }
            }

            for (int y = 0; y < Rows; y++)
            {
                for (int x = 0; x < Columns; x++)
                {
                    RelicBlock sourceBlock = grid[y, x];
                    if (sourceBlock != null)
                    {
                        foreach (var zone in sourceBlock.GetRotatedInfluenceZones())
                        {
                            int targetRow = y - zone.offset.y;
                            int targetCol = x + zone.offset.x;

                            if (IsPositionValid(targetRow, targetCol))
                            {
                                influenceGrid[targetRow, targetCol] = zone.type;
                            }
                        }
                    }
                }
            }

            for (int y = 0; y < Rows; y++)
            {
                for (int x = 0; x < Columns; x++)
                {
                    RelicBlock targetBlock = grid[y, x];
                    if (targetBlock != null)
                    {
                        InfluenceType effect = influenceGrid[y, x];
                        if (effect == InfluenceType.LevelUp) targetBlock.ChangeLevel(1);
                        else if (effect == InfluenceType.LevelDown) targetBlock.ChangeLevel(-1);
                    }
                }
            }
        }

        public bool CanPlaceBlock(int row, int col) => IsPositionValid(row, col) && grid[row, col] == null;
        public RelicBlock GetBlockAt(int row, int col) => IsPositionValid(row, col) ? grid[row, col] : null;
        public InfluenceType GetInfluenceAt(int row, int col) => IsPositionValid(row, col) ? influenceGrid[row, col] : InfluenceType.None;
        private bool IsPositionValid(int row, int col) => row >= 0 && row < Rows && col >= 0 && col < Columns;
        public void Clear()
        {
            Array.Clear(grid, 0, grid.Length);
            Array.Clear(influenceGrid, 0, influenceGrid.Length);
        }
    }
}