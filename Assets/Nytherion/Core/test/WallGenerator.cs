/* WallGenerator.cs
   (기능 단순화 버전)
   - 벽을 그리는 책임을 TilemapVisualizer로 완전히 넘기고,
     이 스크립트는 벽과 코너의 위치를 '계산'하는 역할만 담당합니다.
*/
using System.Collections.Generic;
using UnityEngine;

public static class WallGenerator
{
    // 방향 벡터를 정리하기 위한 정적 클래스
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

    /// <summary>
    /// 주어진 바닥 타일 위치를 기반으로, 모든 외벽과 코너 벽의 위치를 계산하여 반환합니다.
    /// </summary>
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