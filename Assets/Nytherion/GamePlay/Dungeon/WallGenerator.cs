
using System.Collections.Generic;
using UnityEngine;

namespace Nytherion.GamePlay.Dungeon
{
    /// <summary>
    /// 던전의 벽을 생성하는 알고리즘을 제공하는 static 유틸리티 클래스입니다.
    /// 인스턴스화할 필요 없이 어디서든 벽 생성 로직을 호출할 수 있습니다.
    /// </summary>
    public static class WallGenerator
    {
        /// <summary>
        /// 2D 방향 벡터들을 미리 정의해둔 내부 클래스입니다.
        /// 코드의 가독성을 높이고 반복적인 벡터 생성을 방지합니다.
        /// </summary>
        public static class Direction2D
        {
            // 상, 하, 좌, 우 4방향 벡터 리스트
            public static List<Vector2Int> cardinalDirectionsList = new List<Vector2Int> {
                Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left
            };

            // 대각선 4방향 벡터 리스트
            public static List<Vector2Int> diagonalDirectionsList = new List<Vector2Int> {
                new Vector2Int( 1,  1), new Vector2Int( 1, -1),
                new Vector2Int(-1, -1), new Vector2Int(-1,  1)
            };

            // 상하좌우와 대각선을 모두 포함하는 8방향 벡터 리스트
            public static List<Vector2Int> eightDirectionsList = new List<Vector2Int> {
                Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left,
                new Vector2Int( 1,  1), new Vector2Int( 1, -1),
                new Vector2Int(-1, -1), new Vector2Int(-1,  1)
            };
        }

        /// <summary>
        /// 주어진 바닥 타일 위치들을 감싸는 벽 타일 위치들을 찾아 반환합니다.
        /// </summary>
        /// <param name="floorPositions">벽을 생성할 기준이 되는 바닥 타일들의 위치 집합입니다.</param>
        /// <param name="thickness">생성될 벽의 두께입니다.</param>
        /// <returns>계산된 모든 벽 타일의 위치 집합을 반환합니다.</returns>
        public static HashSet<Vector2Int> FindWalls(HashSet<Vector2Int> floorPositions, int thickness)
        {
            // 최종적으로 반환될 모든 벽 타일의 위치를 저장할 HashSet입니다.
            HashSet<Vector2Int> finalWallPositions = new HashSet<Vector2Int>();

            // 탐색의 기준이 될 현재 모양(바닥 + 이미 생성된 벽)을 저장할 HashSet입니다.
            // 초기값으로 전달받은 바닥 위치들을 복사하여 시작합니다.
            HashSet<Vector2Int> currentShape = new HashSet<Vector2Int>(floorPositions);

            // 지정된 두께만큼 벽 생성 과정을 반복합니다.
            for (int i = 0; i < thickness; i++)
            {
                // 이번 반복에서 새로 찾아낼 벽 타일 레이어를 저장할 임시 HashSet입니다.
                HashSet<Vector2Int> newWallLayer = new HashSet<Vector2Int>();

                // 현재 모양을 구성하는 모든 타일을 순회합니다.
                foreach (Vector2Int position in currentShape)
                {
                    // 각 타일의 8방향 주변을 모두 탐색합니다.
                    // 8방향을 모두 확인해야 대각선 방향의 빈틈없이 벽을 만들 수 있습니다.
                    foreach (Vector2Int direction in Direction2D.eightDirectionsList)
                    {
                        Vector2Int neighbourPosition = position + direction;

                        // 이웃한 타일이 현재 모양(바닥 또는 이미 생성된 벽)에 포함되어 있지 않다면,
                        // 그곳은 새로운 벽이 생겨야 할 위치입니다.
                        if (!currentShape.Contains(neighbourPosition))
                        {
                            newWallLayer.Add(neighbourPosition);
                        }
                    }
                }

                // 이번에 찾아낸 새로운 벽 레이어를 최종 벽 위치 집합에 추가합니다.
                finalWallPositions.UnionWith(newWallLayer);

                // 다음 두께의 벽을 올바르게 찾기 위해, 이번에 생성한 벽 레이어를
                // 탐색 기준이 되는 '현재 모양'에 추가합니다.
                currentShape.UnionWith(newWallLayer);
            }

            return finalWallPositions;
        }
    }
}
