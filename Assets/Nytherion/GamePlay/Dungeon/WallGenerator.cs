using System.Collections.Generic;
using UnityEngine;

namespace Nytherion.GamePlay.Dungeon
{
    public static class WallGenerator
    {
        public static class Direction2D
        {
            public static List<Vector2Int> cardinalDirectionsList = new List<Vector2Int> {
            Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left
        };
            public static List<Vector2Int> diagonalDirectionsList = new List<Vector2Int> {
            new Vector2Int( 1,  1), new Vector2Int( 1, -1),
            new Vector2Int(-1, -1), new Vector2Int(-1,  1)
        };
            public static List<Vector2Int> eightDirectionsList = new List<Vector2Int> {
            Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left,
            new Vector2Int( 1,  1), new Vector2Int( 1, -1),
            new Vector2Int(-1, -1), new Vector2Int(-1,  1)
        };
        }

        // 두꺼운 직각 벽을 만드는 최종 함수!
        public static HashSet<Vector2Int> FindWalls(HashSet<Vector2Int> floorPositions, int thickness)
        {
            var finalWallPositions = new HashSet<Vector2Int>();
            var currentShape = new HashSet<Vector2Int>(floorPositions);

            for (int i = 0; i < thickness; i++)
            {
                var newWallLayer = new HashSet<Vector2Int>();
                foreach (var position in currentShape)
                {
                    // 8방향을 모두 탐색해서 빈틈없이 벽을 만듦
                    foreach (var direction in Direction2D.eightDirectionsList)
                    {
                        var neighbourPosition = position + direction;
                        if (!currentShape.Contains(neighbourPosition))
                        {
                            newWallLayer.Add(neighbourPosition);
                        }
                    }
                }
                finalWallPositions.UnionWith(newWallLayer);
                currentShape.UnionWith(newWallLayer);
            }
            return finalWallPositions;
        }
    }
}


     
    
