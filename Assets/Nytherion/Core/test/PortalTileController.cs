// ScriptsArchive/PortalTileController.cs

using Nytherion.GamePlay.Dungeon;
using UnityEngine;
using UnityEngine.Tilemaps;
using Zenject; // Zenject 네임스페이스 추가

[RequireComponent(typeof(Tilemap), typeof(TilemapCollider2D))]
public class PortalTileController : MonoBehaviour
{
    [Header("Tilemap References")]
    [SerializeField] private Tilemap floorTilemap;

    private Tilemap portalTilemap;
    private Collider2D tilemapCollider;

    // --- 의존성 주입 ---
    private DungeonManager _dungeonManager;

    [Inject]
    public void Construct(DungeonManager dungeonManager)
    {
        _dungeonManager = dungeonManager;
    }

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

        // DungeonManager가 없으면 아무것도 하지 않음
        if (_dungeonManager == null) return;

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
            // DungeonManager.Instance 대신 주입받은 _dungeonManager 사용
            if (_dungeonManager.TryGetDestination(pos, out Vector3Int destinationPos))
            {
                TeleportPlayer(player, destinationPos);
                return;
            }
        }
    }

    private void TeleportPlayer(Collider2D player, Vector3Int destinationPortalCell)
    {
        if (TryFindSpawnPoint(destinationPortalCell, out Vector3Int spawnCell))
        {
            Vector3 targetWorldPos = floorTilemap.GetCellCenterWorld(spawnCell);
            player.transform.position = targetWorldPos;
        }
        else
        {
            Vector3 targetWorldPos = portalTilemap.GetCellCenterWorld(destinationPortalCell);
            player.transform.position = targetWorldPos;
        }
    }

    // ... (TryFindSpawnPoint 메서드는 변경 없음) ...
    private bool TryFindSpawnPoint(Vector3Int portalCenterCell, out Vector3Int spawnPoint)
    {
        bool isHorizontal = portalTilemap.HasTile(portalCenterCell + Vector3Int.left) || portalTilemap.HasTile(portalCenterCell + Vector3Int.right);
        Vector3Int checkDir1, checkDir2;

        if (isHorizontal)
        {
            checkDir1 = Vector3Int.up;
            checkDir2 = Vector3Int.down;
        }
        else
        {
            checkDir1 = Vector3Int.left;
            checkDir2 = Vector3Int.right;
        }

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

        spawnPoint = Vector3Int.zero;
        return false;
    }
}