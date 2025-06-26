using System;
using System.Collections.Generic;
using Nytherion.Data.ScriptableObjects.Engravings;
using UnityEngine;

namespace Nytherion.GamePlay.Engravings
{
    public enum InfluenceType { None, LevelUp, LevelDown }

    public class EngravingGrid
    {
        public int Rows { get; private set; }
        public int Columns { get; private set; }
        private readonly EngravingBlock[,] grid;
        private readonly InfluenceType[,] influenceGrid;

        public EngravingGrid(int rows, int columns)
        {
            Rows = rows;
            Columns = columns;
            grid = new EngravingBlock[rows, columns];
            influenceGrid = new InfluenceType[rows, columns];
        }

        public void PlaceBlock(EngravingBlock block, int row, int col)
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
                    EngravingBlock sourceBlock = grid[y, x];
                    if (sourceBlock != null)
                    {
                        foreach (var zone in sourceBlock.SourceData.influenceZones)
                        {
                            int targetRow = y + zone.offset.y;
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
                    EngravingBlock targetBlock = grid[y, x];
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
        public EngravingBlock GetBlockAt(int row, int col) => IsPositionValid(row, col) ? grid[row, col] : null;
        public InfluenceType GetInfluenceAt(int row, int col) => IsPositionValid(row, col) ? influenceGrid[row, col] : InfluenceType.None;
        private bool IsPositionValid(int row, int col) => row >= 0 && row < Rows && col >= 0 && col < Columns;
        public void Clear()
        {
            Array.Clear(grid, 0, grid.Length);
            Array.Clear(influenceGrid, 0, influenceGrid.Length);
        }
    }
}