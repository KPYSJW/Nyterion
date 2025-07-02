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
        }


        public static HashSet<Vector2Int> FindWalls(HashSet<Vector2Int> floorPositions)
        {
            var wallPositions = new HashSet<Vector2Int>();
            var basicWallPositions = FindWallsInDirections(floorPositions, Direction2D.cardinalDirectionsList);
            var cornerWallPositions = FindCornerWalls(floorPositions);

            wallPositions.UnionWith(basicWallPositions);
            wallPositions.UnionWith(cornerWallPositions);

            return wallPositions;
        }

        private static HashSet<Vector2Int> FindCornerWalls(HashSet<Vector2Int> floorPositions)
        {
            var cornerWallPositions = new HashSet<Vector2Int>();
            foreach (var position in floorPositions)
            {
                foreach (var direction in Direction2D.diagonalDirectionsList)
                {
                    var cornerPosition = position + direction;
                    if (floorPositions.Contains(cornerPosition)) continue;

                    var neighbourCheck1 = position + new Vector2Int(direction.x, 0);
                    var neighbourCheck2 = position + new Vector2Int(0, direction.y);
                    if (!floorPositions.Contains(neighbourCheck1) && !floorPositions.Contains(neighbourCheck2))
                    {
                        cornerWallPositions.Add(cornerPosition);
                    }
                }
            }
            return cornerWallPositions;
        }

        private static HashSet<Vector2Int> FindWallsInDirections(HashSet<Vector2Int> floorPositions, List<Vector2Int> directionsList)
        {
            var wallPositions = new HashSet<Vector2Int>();
            foreach (var position in floorPositions)
            {
                foreach (var direction in directionsList)
                {
                    var neighbourPosition = position + direction;
                    if (!floorPositions.Contains(neighbourPosition))
                    {
                        wallPositions.Add(neighbourPosition);
                    }
                }
            }
            return wallPositions;
        }
    }
}