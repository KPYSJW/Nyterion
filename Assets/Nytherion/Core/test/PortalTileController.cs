using UnityEngine;
using UnityEngine.Tilemaps;
using Zenject;

namespace Nytherion.GamePlay.Dungeon
{
    /// <summary>
    /// 포탈 타일과의 상호작용을 처리합니다. 플레이어가 포탈에 닿으면 연결된 다른 포탈로 텔레포트시킵니다.
    /// 이 스크립트가 붙는 게임오브젝트에는 반드시 Tilemap과 TilemapCollider2D 컴포넌트가 있어야 합니다.
    /// </summary>
    [RequireComponent(typeof(Tilemap), typeof(TilemapCollider2D))]
    public class PortalTileController : MonoBehaviour
    {
        // --- 의존성 주입 (Zenject) ---
        private Tilemap floorTilemap;
        private Tilemap portalTilemap;
        private DungeonManager _dungeonManager;

        // --- 컴포넌트 참조 ---
        private Collider2D tilemapCollider;

        /// <summary>
        /// Zenject 컨테이너가 이 클래스의 인스턴스를 생성할 때 호출되어 필요한 의존성을 주입합니다.
        /// </summary>
        [Inject]
        public void Construct(
            DungeonManager dungeonManager,
            [Inject(Id = "FloorTilemap")] Tilemap floorTilemap, // "FloorTilemap" ID로 바인딩된 Tilemap 주입
            [Inject(Id = "PortalTilemap")] Tilemap portalTilemapInstance // "PortalTilemap" ID로 바인딩된 Tilemap 주입
        )
        {
            _dungeonManager = dungeonManager;
            this.floorTilemap = floorTilemap;
            this.portalTilemap = portalTilemapInstance;
        }

        private void Awake()
        {
            // 이 게임오브젝트에 붙어있는 TilemapCollider2D 컴포넌트를 가져옵니다.
            tilemapCollider = GetComponent<Collider2D>();
        }

        /// <summary>
        /// 다른 Collider2D가 이 오브젝트의 트리거 영역에 들어왔을 때 호출됩니다.
        /// </summary>
        private void OnTriggerEnter2D(Collider2D other)
        {
            // 충돌한 오브젝트가 "Player" 태그를 가지고 있지 않으면 아무것도 하지 않습니다.
            if (!other.CompareTag("Player"))
            {
                return;
            }

            // DungeonManager가 주입되지 않았으면 로직을 실행할 수 없으므로 중단합니다.
            if (_dungeonManager == null)
            {
                Debug.LogError("[PortalTileController] DungeonManager is not injected!");
                return;
            }

            // 플레이어의 충돌체와 가장 가까운 포탈 타일맵 위의 지점을 찾습니다.
            Vector3 contactPoint = tilemapCollider.ClosestPoint(other.transform.position);
            // 월드 좌표를 타일맵의 셀 좌표로 변환합니다.
            Vector3Int initialContactCell = portalTilemap.WorldToCell(contactPoint);

            // 플레이어의 충돌 지점 및 그 주변에서 포탈 타일을 찾습니다.
            if (TryFindNearbyPortalTile(initialContactCell, out Vector3Int portalCellPos))
            {
                // 찾은 포탈 타일을 사용하여 텔레포트를 시도합니다.
                TryToFindAndUsePortal(portalCellPos, other);
            }
        }

        /// <summary>
        /// 지정된 위치와 그 주변 8방향을 탐색하여 포탈 타일이 있는지 확인합니다.
        /// 플레이어의 충돌체가 타일 경계에 걸쳤을 때도 안정적으로 포탈을 감지하기 위함입니다.
        /// </summary>
        /// <returns>포탈 타일을 찾았으면 true, 아니면 false를 반환합니다.</returns>
        private bool TryFindNearbyPortalTile(Vector3Int centerPos, out Vector3Int foundPortalPos)
        {
            // 먼저 중앙 위치에 포탈 타일이 있는지 확인합니다.
            if (portalTilemap.HasTile(centerPos))
            {
                foundPortalPos = centerPos;
                return true;
            }

            // 중앙에 없다면, 주변 8방향의 이웃 타일들을 정의합니다.
            Vector3Int[] neighbors = {
            centerPos + Vector3Int.up, centerPos + Vector3Int.down, centerPos + Vector3Int.left,
            centerPos + Vector3Int.right, centerPos + new Vector3Int(1, 1, 0), centerPos + new Vector3Int(1, -1, 0),
            centerPos + new Vector3Int(-1, -1, 0), centerPos + new Vector3Int(-1, 1, 0)
        };

            // 이웃 타일들을 순회하며 포탈 타일이 있는지 확인합니다.
            foreach (Vector3Int neighbor in neighbors)
            {
                if (portalTilemap.HasTile(neighbor))
                {
                    foundPortalPos = neighbor;
                    return true; // 찾으면 즉시 반환
                }
            }

            // 주변 8방향 어디에도 포탈 타일이 없으면 실패를 반환합니다.
            foundPortalPos = Vector3Int.zero;
            return false;
        }

        /// <summary>
        /// 찾은 포탈 타일 위치를 기반으로 DungeonManager에 등록된 포탈인지 확인하고 텔레포트를 실행합니다.
        /// 포탈은 보통 3칸으로 이루어져 있으므로, 중심 타일을 찾기 위해 주변을 탐색합니다.
        /// </summary>
        private void TryToFindAndUsePortal(Vector3Int position, Collider2D player)
        {
            // DungeonManager에는 포탈의 중심 타일만 등록되어 있을 수 있으므로,
            // 현재 타일과 상하좌우 타일을 모두 확인하여 등록된 포탈 링크가 있는지 찾아봅니다.
            Vector3Int[] positionsToCheck = {
            position, position + Vector3Int.left, position + Vector3Int.right,
            position + Vector3Int.up, position + Vector3Int.down
        };

            foreach (Vector3Int pos in positionsToCheck)
            {
                // DungeonManager에게 해당 위치가 등록된 포탈인지, 그렇다면 목적지가 어디인지 물어봅니다.
                if (_dungeonManager.TryGetDestination(pos, out Vector3Int destinationPos))
                {
                    // 목적지를 찾았다면 플레이어를 텔레포트시키고 함수를 종료합니다.
                    TeleportPlayer(player, destinationPos);
                    return;
                }
            }
        }

        /// <summary>
        /// 플레이어를 목적지 포탈 근처의 안전한 위치로 텔레포트시킵니다.
        /// </summary>
        private void TeleportPlayer(Collider2D player, Vector3Int destinationPortalCell)
        {
            // 먼저 목적지 포탈 앞에 안전하게 스폰될 수 있는 바닥 타일이 있는지 찾아봅니다.
            if (TryFindSpawnPoint(destinationPortalCell, out Vector3Int spawnCell))
            {
                // 안전한 스폰 지점을 찾았다면, 그곳의 중앙 월드 좌표로 플레이어를 이동시킵니다.
                Vector3 targetWorldPos = floorTilemap.GetCellCenterWorld(spawnCell);
                player.transform.position = targetWorldPos;
            }
            else
            {
                // 안전한 스폰 지점을 찾지 못했다면, 비상 수단으로 목적지 포탈의 중앙으로 이동시킵니다.
                Vector3 targetWorldPos = portalTilemap.GetCellCenterWorld(destinationPortalCell);
                player.transform.position = targetWorldPos;
                Debug.LogWarning($"Could not find a safe spawn point near portal {destinationPortalCell}. Teleporting to portal center.");
            }
        }

        /// <summary>
        /// 목적지 포탈 주변에서 플레이어가 안전하게 스폰될 수 있는 바닥 타일을 찾습니다.
        /// 플레이어가 텔레포트 후 벽에 끼이는 현상을 방지합니다.
        /// </summary>
        /// <returns>안전한 스폰 지점을 찾았으면 true, 아니면 false를 반환합니다.</returns>
        private bool TryFindSpawnPoint(Vector3Int portalCenterCell, out Vector3Int spawnPoint)
        {
            // 포탈이 가로 방향인지 세로 방향인지 판단합니다.
            bool isHorizontal = portalTilemap.HasTile(portalCenterCell + Vector3Int.left) || portalTilemap.HasTile(portalCenterCell + Vector3Int.right);
            Vector3Int checkDir1, checkDir2;

            if (isHorizontal)
            {
                // 가로 포탈이면 위/아래 방향을 탐색합니다.
                checkDir1 = Vector3Int.up;
                checkDir2 = Vector3Int.down;
            }
            else
            {
                // 세로 포탈이면 왼쪽/오른쪽 방향을 탐색합니다.
                checkDir1 = Vector3Int.left;
                checkDir2 = Vector3Int.right;
            }

            // 가장 이상적인 위치(포탈에서 2칸 떨어진 곳)부터 탐색합니다.
            if (floorTilemap.HasTile(portalCenterCell + checkDir1 * 2))
            {
                spawnPoint = portalCenterCell + checkDir1 * 2;
                return true;
            }
            if (floorTilemap.HasTile(portalCenterCell + checkDir2 * 2))
            {
                spawnPoint = portalCenterCell + checkDir2 * 2;
                return true;
            }

            // 2칸 떨어진 곳이 막혀있다면, 차선책으로 1칸 떨어진 곳을 탐색합니다.
            if (floorTilemap.HasTile(portalCenterCell + checkDir1))
            {
                spawnPoint = portalCenterCell + checkDir1;
                return true;
            }
            if (floorTilemap.HasTile(portalCenterCell + checkDir2))
            {
                spawnPoint = portalCenterCell + checkDir2;
                return true;
            }

            // 안전한 스폰 지점을 찾지 못했으면 실패를 반환합니다.
            spawnPoint = Vector3Int.zero;
            return false;
        }
    }
}