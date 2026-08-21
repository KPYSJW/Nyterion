using System;
using System.Collections.Generic;

namespace Nytherion.GamePlay.Relics
{
    public enum InfluenceType { None, LevelUp, LevelDown, Silence, SynergyLink }

    public class RelicGrid
    {
        public int Rows { get; private set; }
        public int Columns { get; private set; }
        private readonly RelicBlock[,] grid;
        private readonly InfluenceType[,] influenceGrid;
        private readonly bool[,] silenceGrid;

        public RelicGrid(int rows, int columns)
        {
            Rows = rows;
            Columns = columns;
            grid = new RelicBlock[rows, columns];
            influenceGrid = new InfluenceType[rows, columns];
            silenceGrid = new bool[rows, columns];
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
            Array.Clear(silenceGrid, 0, silenceGrid.Length);

            foreach (var block in grid)
            {
                if (block != null)
                {
                    block.ResetLevel();
                    block.SetDisabled(false);
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
                                if (zone.type == InfluenceType.Silence)
                                {
                                    silenceGrid[targetRow, targetCol] = true;
                                }
                                else
                                {
                                    influenceGrid[targetRow, targetCol] = zone.type;
                                }
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
                        // Silence가 최우선
                        if (silenceGrid[y, x])
                        {
                            targetBlock.SetDisabled(true);
                        }
                        else
                        {
                            InfluenceType effect = influenceGrid[y, x];
                            if (effect == InfluenceType.LevelUp) targetBlock.ChangeLevel(1);
                            else if (effect == InfluenceType.LevelDown) targetBlock.ChangeLevel(-1);
                        }
                    }
                }
            }

            // 시너지 체인 계산 후 매니저 업데이트 (매니저는 외부에서 호출)
        }

        public Dictionary<string, int> EvaluateSynergyChains()
        {
            var results = new Dictionary<string, int>();
            var adjacency = new Dictionary<RelicBlock, List<RelicBlock>>();
            var allSeriesBlocks = new Dictionary<string, List<RelicBlock>>();

            // 1. 인접 그래프 구성 (SynergyLink 기반)
            for (int y = 0; y < Rows; y++)
            {
                for (int x = 0; x < Columns; x++)
                {
                    RelicBlock source = grid[y, x];
                    if (source == null || string.IsNullOrEmpty(source.SourceData.synergySeriesId)) continue;

                    string seriesId = source.SourceData.synergySeriesId;
                    if (!allSeriesBlocks.ContainsKey(seriesId)) allSeriesBlocks[seriesId] = new List<RelicBlock>();
                    allSeriesBlocks[seriesId].Add(source);

                    foreach (var zone in source.GetRotatedInfluenceZones())
                    {
                        if (zone.type != InfluenceType.SynergyLink) continue;

                        int targetRow = y - zone.offset.y;
                        int targetCol = x + zone.offset.x;

                        if (IsPositionValid(targetRow, targetCol))
                        {
                            RelicBlock target = grid[targetRow, targetCol];
                            if (target != null && target.SourceData.synergySeriesId == seriesId)
                            {
                                if (!adjacency.ContainsKey(source)) adjacency[source] = new List<RelicBlock>();
                                adjacency[source].Add(target);
                            }
                        }
                    }
                }
            }

            // 2. 각 시리즈별 최장 경로 계산
            foreach (var seriesKvp in allSeriesBlocks)
            {
                int maxLen = 0;
                foreach (var startNode in seriesKvp.Value)
                {
                    maxLen = Math.Max(maxLen, GetMaxDepth(startNode, adjacency, new HashSet<RelicBlock>()));
                }
                results[seriesKvp.Key] = maxLen;
            }

            return results;
        }

        private int GetMaxDepth(RelicBlock current, Dictionary<RelicBlock, List<RelicBlock>> adj, HashSet<RelicBlock> visited)
        {
            if (visited.Contains(current)) return 0; // 순환 방지
            visited.Add(current);

            int depth = 1;
            if (adj.TryGetValue(current, out var neighbors))
            {
                int maxSubDepth = 0;
                foreach (var neighbor in neighbors)
                {
                    maxSubDepth = Math.Max(maxSubDepth, GetMaxDepth(neighbor, adj, new HashSet<RelicBlock>(visited)));
                }
                depth += maxSubDepth;
            }
            return depth;
        }

        public bool CanPlaceBlock(int row, int col) => IsPositionValid(row, col) && grid[row, col] == null;
        public RelicBlock GetBlockAt(int row, int col) => IsPositionValid(row, col) ? grid[row, col] : null;
        public InfluenceType GetInfluenceAt(int row, int col)
        {
            if (!IsPositionValid(row, col)) return InfluenceType.None;
            if (silenceGrid[row, col]) return InfluenceType.Silence;
            return influenceGrid[row, col];
        }
        private bool IsPositionValid(int row, int col) => row >= 0 && row < Rows && col >= 0 && col < Columns;
        public void Clear()
        {
            Array.Clear(grid, 0, grid.Length);
            Array.Clear(influenceGrid, 0, influenceGrid.Length);
            Array.Clear(silenceGrid, 0, silenceGrid.Length);
        }
    }
}