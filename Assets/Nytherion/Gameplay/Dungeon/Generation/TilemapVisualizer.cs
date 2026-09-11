using Nytherion.Data.ScriptableObjects.Dungeon;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;
using VContainer.Unity;

namespace Nytherion.GamePlay.Dungeon
{
    /// <summary>
    /// 절차적으로 생성된 던전 데이터를 받아 실제 타일맵에 타일을 그리거나 오브젝트를 배치하는 클래스입니다.
    /// </summary>
    public class TilemapVisualizer : MonoBehaviour
    {
        [Header("타일맵 참조")]
        [Tooltip("바닥 타일을 그릴 타일맵")]
        public Tilemap floorTilemap;
        [Tooltip("벽 타일을 그릴 타일맵")]
        public Tilemap wallTilemap;
        [Tooltip("포탈 타일을 그릴 타일맵")]
        public Tilemap portalTilemap;

        [Header("오브젝트 부모")]
        [Tooltip("생성된 장애물 오브젝트들을 담을 부모 Transform")]
        [SerializeField] private Transform obstacleHolder;

        [Header("타일 에셋")]
        [SerializeField] private TileBase floorTile;
        [SerializeField] private TileBase portalTile;
        [SerializeField] private RuleTile wallRuleTile; // 벽은 RuleTile을 사용하여 자동으로 연결 부위를 처리

        [Header("특수 방 타일 에셋")]
        [SerializeField] private TileBase startRoomTile;
        [SerializeField] private TileBase shopRoomTile;
        [SerializeField] private TileBase itemRoomTile;

       

        /// <summary>
        /// 외부(주로 Zenject Installer)에서 타일맵 참조를 설정하기 위한 초기화 메서드입니다.
        /// </summary>
        public void InitializeTilemaps(Tilemap floor, Tilemap wall, Tilemap portal)
        {
            this.floorTilemap = floor;
            this.wallTilemap = wall;
            this.portalTilemap = portal;
        }

        public void CreateObjectHolder()
        {
            if(obstacleHolder==null)
            {
                GameObject gameObject=new GameObject("ObjectHolder");
                obstacleHolder=gameObject.transform;
            }
        }

        /// <summary>
        /// 전달받은 장애물 데이터를 기반으로 실제 게임 오브젝트를 생성합니다.
        /// </summary>
        public void InstantiateObstacles(List<RoomFirstDungeonGenerator.PlacedObstacleData> obstaclesToPlace)
        {
            // 기존에 생성된 장애물이 있다면 모두 삭제합니다.
            
           CreateObjectHolder();

            if (obstaclesToPlace == null) return;

            foreach (RoomFirstDungeonGenerator.PlacedObstacleData obstacleData in obstaclesToPlace)
            {
                if (obstacleData.prefab != null)
                {
                    Instantiate(obstacleData.prefab, obstacleData.worldPosition, Quaternion.identity, obstacleHolder);
                }
            }
        }

        public void InstantiateSpecialRoomObjects(
            List<RoomFirstDungeonGenerator.Room> specialRooms,
            DungeonData dungeonData)
        {
            CreateObjectHolder();
            //bool hasDedicatedHolder = specialRoomObjectHolder != null;
           // Transform holder = hasDedicatedHolder ? specialRoomObjectHolder : obstacleHolder;
            if (/*holder == null ||*/ specialRooms == null || dungeonData == null) return;
            
            /*if (hasDedicatedHolder)
            {
                ClearChildren(holder);
            }*/

            foreach (RoomFirstDungeonGenerator.Room room in specialRooms)
            {
                GameObject roomPrefab = GetRoomPrefab(room.type, dungeonData);
                if (roomPrefab == null) continue;

                Transform objectsRoot = roomPrefab.transform.Find("Objects");
                if (objectsRoot == null) continue;

                foreach (Transform child in objectsRoot)
                {
                    if (ShouldSkipRoomObject(child)) continue;

                    Vector3 localPosition = roomPrefab.transform.InverseTransformPoint(child.position);
                    Vector3 worldPosition = (Vector3)room.center + localPosition;
                   GameObject instance = Instantiate(child.gameObject, worldPosition, child.rotation,obstacleHolder);

                    var scope = VContainer.Unity.LifetimeScope.Find<GameSceneLifetimeScope>();
                    if (scope != null)
                    {
                        scope.Container.InjectGameObject(instance);
                    }
                }
            }
        }

        /// <summary>
        /// 일반 바닥 타일을 그립니다.
        /// </summary>
        public void PaintFloorTiles(IEnumerable<Vector2Int> floorPositions)
        {
            PaintTiles(floorPositions, floorTilemap, floorTile);
        }

        /// <summary>
        /// RuleTile을 사용하여 벽 타일을 그립니다.
        /// </summary>
        public void PaintWallsWithRuleTile(IEnumerable<Vector2Int> wallPositions)
        {
            PaintTiles(wallPositions, wallTilemap, wallRuleTile);
            Debug.Log("타일완성");
        }

        /// <summary>
        /// 포탈 타일을 그립니다.
        /// </summary>
        public void PaintPortals(IEnumerable<Vector2Int> portalPositions)
        {
            PaintTiles(portalPositions, portalTilemap, portalTile);
        }

        /// <summary>
        /// 시작 방, 상점 방 등 특수 방의 바닥을 종류에 맞는 타일로 그립니다.
        /// </summary>
        public void PaintSpecialRoomFloors(
            List<RoomFirstDungeonGenerator.Room> specialRooms,
            DungeonData dungeonData,
            Dictionary<RoomFirstDungeonGenerator.Room, HashSet<Vector2Int>> roomFloorData)
        {
            foreach (RoomFirstDungeonGenerator.Room room in specialRooms)
            {
                // 보스 방이고, 프리팹이 지정되어 있다면 프리팹을 그립니다.
                if (room.type == RoomFirstDungeonGenerator.RoomType.Boss && dungeonData.bossRoomPrefab != null)
                {
                    PaintPrefab(room.center, dungeonData.bossRoomPrefab.GetComponentInChildren<Tilemap>());
                }
                else if (room.type == RoomFirstDungeonGenerator.RoomType.Shop && dungeonData.ShopRoomPrefab != null)
                {
                    PaintPrefab(room.center, dungeonData.ShopRoomPrefab.GetComponentInChildren<Tilemap>());
                }
                else if (room.type == RoomFirstDungeonGenerator.RoomType.Start && dungeonData.StartRoomPrefab != null)
                {
                    PaintPrefab(room.center, dungeonData.StartRoomPrefab.GetComponentInChildren<Tilemap>());
                }
                else // 그 외의 특수 방
                {
                    TileBase tileToUse = GetTileForRoomType(room.type);
                    if (tileToUse != null && roomFloorData.TryGetValue(room, out HashSet<Vector2Int> floorPositions))
                    {
                        PaintTiles(floorPositions, floorTilemap, tileToUse);
                    }
                }
            }
        }

        /// <summary>
        /// 모든 타일맵과 생성된 오브젝트를 지웁니다.
        /// </summary>
        public void Clear()
        {
            floorTilemap?.ClearAllTiles();
            wallTilemap?.ClearAllTiles();
            portalTilemap?.ClearAllTiles();

            // 장애물 홀더의 자식 오브젝트들도 모두 삭제합니다.
             CreateObjectHolder();
            if (obstacleHolder != null)
            {
                ClearChildren(obstacleHolder);
            }

            
        }

        /// <summary>
        /// 여러 개의 타일을 한 번에 그리는 최적화된 메서드입니다.
        /// SetTile을 반복 호출하는 것보다 SetTiles를 한 번 호출하는 것이 훨씬 빠릅니다.
        /// </summary>
        public void PaintTiles(IEnumerable<Vector2Int> positions, Tilemap tilemap, TileBase tile)
        {
            if (tile == null || tilemap == null || positions == null || !positions.Any()) return;

            // 1. Vector2Int 컬렉션을 Vector3Int 배열로 변환합니다.
            Vector3Int[] positionArray = positions.Select(pos => (Vector3Int)pos).ToArray();
            // 2. 타일 배열을 생성합니다.
            TileBase[] tileArray = Enumerable.Repeat(tile, positionArray.Length).ToArray();
            // 3. SetTiles 메서드로 한 번에 그립니다.
            tilemap.SetTiles(positionArray, tileArray);
        }

        /// <summary>
        /// 방 종류에 맞는 타일 에셋을 반환합니다.
        /// </summary>
        public TileBase GetTileForRoomType(RoomFirstDungeonGenerator.RoomType type)
        {
            switch (type)
            {
                case RoomFirstDungeonGenerator.RoomType.Start:
                    return startRoomTile;
                case RoomFirstDungeonGenerator.RoomType.Shop:
                    return shopRoomTile;
                case RoomFirstDungeonGenerator.RoomType.Item:
                    return itemRoomTile;
                default:
                    return floorTile; // 일반 방이나 보스 방은 다른 방식으로 처리되므로 null 반환
            }
        }

        /// <summary>
        /// 지정된 Tilemap 프리팹을 특정 위치에 그립니다.
        /// </summary>
        private void PaintPrefab(Vector2 roomCenter, Tilemap prefabTilemap)
        {
            if (prefabTilemap == null) return;

            // 프리팹 타일맵의 모든 타일을 순회합니다.
            foreach (Vector3Int tilePos in prefabTilemap.cellBounds.allPositionsWithin)
            {
                TileBase tile = prefabTilemap.GetTile(tilePos);
                if (tile != null)
                {
                    // 프리팹의 로컬 타일 위치를 월드 위치로 변환하여 그립니다.
                    Vector3Int worldPos = Vector3Int.RoundToInt(roomCenter) + tilePos;
                    floorTilemap.SetTile(worldPos, tile);
                }
            }
        }

        private GameObject GetRoomPrefab(RoomFirstDungeonGenerator.RoomType type, DungeonData dungeonData)
        {
            switch (type)
            {
                case RoomFirstDungeonGenerator.RoomType.Start:
                    return dungeonData.StartRoomPrefab;
                case RoomFirstDungeonGenerator.RoomType.Shop:
                    return dungeonData.ShopRoomPrefab;
                case RoomFirstDungeonGenerator.RoomType.Item:
                    return dungeonData.ItemRoomPrefab;
                case RoomFirstDungeonGenerator.RoomType.Boss:
                    return dungeonData.bossRoomPrefab;
                default:
                    return null;
            }
        }

        private bool ShouldSkipRoomObject(Transform target)
        {
            return target.GetComponent<Tilemap>() != null
                || target.GetComponent<Grid>() != null
                || target.GetComponent<BossSpawnPoint>() != null;
        }

        private void ClearChildren(Transform holder)
        {
            for (int i = holder.childCount - 1; i >= 0; i--)
            {
                Transform child = holder.GetChild(i);
                if (Application.isPlaying)
                {
                    Destroy(child.gameObject);
                }
                else
                {
                    DestroyImmediate(child.gameObject);
                }
            }
        }
    }
}
