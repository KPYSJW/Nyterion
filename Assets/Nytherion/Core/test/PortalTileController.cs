/* PortalTileController.cs (최종 수정)

    [역할]
    이 스크립트는 'PortalTilemap'에 직접 붙어서, 플레이어와의 물리적 상호작용을 감지합니다.
    플레이어와의 충돌이 감지되면, 플레이어의 위치와 상관없이 '충돌이 일어난 타일'의 위치를
    찾아내어 포탈 이동을 처리합니다.

    [핵심 변경점]
    - 순간이동 도착 지점을 포탈에서 2칸 떨어진 바닥 타일로 우선적으로 탐색하도록 변경했습니다.
    - 만약 2칸 앞에 스폰이 불가능할 경우, 1칸 앞으로 스폰하는 안전장치를 마련했습니다.
*/
using UnityEngine;
using UnityEngine.Tilemaps;

[RequireComponent(typeof(Tilemap), typeof(TilemapCollider2D))]
public class PortalTileController : MonoBehaviour
{
    // --- 추가된 부분 ---
    [Header("Tilemap References")]
    [Tooltip("플레이어가 스폰될 바닥을 찾기 위해 FloorTilemap이 필요합니다.")]
    [SerializeField] private Tilemap floorTilemap; // Inspector에서 FloorTilemap을 할당해줘야 함
    // --- 여기까지 ---

    private Tilemap portalTilemap;
    private Collider2D tilemapCollider;

    private void Awake()
    {
        portalTilemap = GetComponent<Tilemap>();
        tilemapCollider = GetComponent<Collider2D>();

        if (floorTilemap == null)
        {
            Debug.LogError("FloorTilemap이 PortalTileController에 할당되지 않았습니다!", this.gameObject);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player"))
        {
            return;
        }

        Vector3 contactPoint = tilemapCollider.ClosestPoint(other.transform.position);
        Vector3Int initialContactCell = portalTilemap.WorldToCell(contactPoint);

        if (TryFindNearbyPortalTile(initialContactCell, out Vector3Int portalCellPos))
        {
            TryToFindAndUsePortal(portalCellPos, other);
        }
    }

    private bool TryFindNearbyPortalTile(Vector3Int centerPos, out Vector3Int foundPortalPos)
    {
        if (portalTilemap.HasTile(centerPos))
        {
            foundPortalPos = centerPos;
            return true;
        }

        Vector3Int[] neighbors = {
            centerPos + Vector3Int.up, centerPos + Vector3Int.down, centerPos + Vector3Int.left,
            centerPos + Vector3Int.right, centerPos + new Vector3Int(1, 1, 0), centerPos + new Vector3Int(1, -1, 0),
            centerPos + new Vector3Int(-1, -1, 0), centerPos + new Vector3Int(-1, 1, 0)
        };

        foreach (var neighbor in neighbors)
        {
            if (portalTilemap.HasTile(neighbor))
            {
                foundPortalPos = neighbor;
                return true;
            }
        }

        foundPortalPos = Vector3Int.zero;
        return false;
    }

    private void TryToFindAndUsePortal(Vector3Int position, Collider2D player)
    {
        Vector3Int[] positionsToCheck = {
            position, position + Vector3Int.left, position + Vector3Int.right,
            position + Vector3Int.up, position + Vector3Int.down
        };

        foreach (var pos in positionsToCheck)
        {
            if (DungeonManager.Instance.TryGetDestination(pos, out Vector3Int destinationPos))
            {
                TeleportPlayer(player, destinationPos);
                return;
            }
        }
    }

    /// <summary>
    /// 플레이어를 목적지 포탈 앞의 바닥 타일로 순간이동시킵니다.
    /// </summary>
    private void TeleportPlayer(Collider2D player, Vector3Int destinationPortalCell)
    {
        if (DungeonManager.Instance.IsRoomCleared(Vector2Int.zero))
        {
            // 목적지 포탈 셀을 기반으로 실제 스폰될 바닥 타일을 찾습니다.
            if (TryFindSpawnPoint(destinationPortalCell, out Vector3Int spawnCell))
            {
                Vector3 targetWorldPos = floorTilemap.GetCellCenterWorld(spawnCell);
                player.transform.position = targetWorldPos;
                Debug.Log($"플레이어를 바닥 {targetWorldPos}으로 이동시켰습니다!");
            }
            else
            {
                // 비상시: 만약 바닥을 못찾으면 그냥 포탈 위로 이동 (안전 장치)
                Vector3 targetWorldPos = portalTilemap.GetCellCenterWorld(destinationPortalCell);
                player.transform.position = targetWorldPos;
                Debug.LogWarning($"스폰 지점을 찾지 못해 포탈 위 {targetWorldPos}으로 이동했습니다.");
            }
        }
        else
        {
            Debug.Log("방의 몬스터를 모두 처리해야 합니다.");
        }
    }

    /// <summary>
    /// 포탈 위치 주변에서 플레이어가 스폰될 안전한 바닥 타일 위치를 찾습니다.
    /// </summary>
    private bool TryFindSpawnPoint(Vector3Int portalCenterCell, out Vector3Int spawnPoint)
    {
        // 1. 포탈의 방향을 추정합니다.
        // 포탈 타일의 왼쪽/오른쪽에 다른 포탈 타일이 있는지 확인하여 수평/수직 여부를 판단합니다.
        bool isHorizontal = portalTilemap.HasTile(portalCenterCell + Vector3Int.left) || portalTilemap.HasTile(portalCenterCell + Vector3Int.right);

        Vector3Int checkDir1, checkDir2;

        if (isHorizontal)
        {
            // 포탈이 수평이면, 스폰 지점은 위 또는 아래 바닥입니다.
            checkDir1 = Vector3Int.up;
            checkDir2 = Vector3Int.down;
        }
        else
        {
            // 포탈이 수직이면, 스폰 지점은 왼쪽 또는 오른쪽 바닥입니다.
            checkDir1 = Vector3Int.left;
            checkDir2 = Vector3Int.right;
        }

        // 2. 포탈에서 2칸 떨어진 곳을 우선적으로 확인합니다.
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

        // 3. 2칸 떨어진 곳에 스폰 지점이 없다면, 1칸 떨어진 곳을 확인합니다. (안전 장치)
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

        // 비상시 (거의 발생하지 않음)
        spawnPoint = Vector3Int.zero;
        return false;
    }
}
