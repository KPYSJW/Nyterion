using Nytherion.Core.Managers;
using Nytherion.Data.ScriptableObjects.Enemy;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;
using Random = UnityEngine.Random;
using Nytherion.GamePlay.Characters.Enemy;
using Nytherion.Data.ScriptableObjects.Dungeon;
using Nytherion.UI.Map;
using Nytherion.Core.Data;

namespace Nytherion.GamePlay.Dungeon
{
    public class RoomFirstDungeonGenerator : AbstractDungeonGenertor
    {
        #region 내부 구조체, 열거형, 클래스

        public struct PlacedObstacleData
        {
            public GameObject prefab;
            public Vector2 worldPosition;
        }

        public class DungeonBuildResult
        {
            public List<Room> rooms;
            public Dictionary<Room, HashSet<Vector2Int>> roomFloorData;
            public HashSet<Vector2Int> totalFloorPositions;
            public HashSet<Vector2Int> wallPositions;
            public HashSet<Vector2Int> portalPositions;
            public List<Tuple<Vector2Int, Vector2Int>> portalLinks;
            public List<Tuple<Room, Room>> roomConnections;
            public List<PlacedObstacleData> obstacles;
            public Room startRoom;
        }

        public enum RoomType { Normal, Start, Boss, Shop, Item }

        public class Room
        {
            public Vector2Int gridPos;
            public Vector2Int size;
            public Vector2 center;
            public int id = -1;
            public List<EnemyBase> enemies = new List<EnemyBase>();
            public RoomType type = RoomType.Normal;
            public Vector2? bossSpawnPoint;
            public bool visited;
            public bool cleared;
            public BoundsInt Bounds => new BoundsInt(Vector3Int.RoundToInt(center - (Vector2)size / 2), (Vector3Int)size);

            // 방의 그리드 위치와 크기를 받아 기본 방 데이터를 생성합니다.
            public Room(Vector2Int gridPos, Vector2Int size)
            {
                this.gridPos = gridPos;
                this.size = size;
                this.enemies = new List<EnemyBase>();
            }

            // 이 방에 속한 살아있는 적들을 활성화합니다.
            public void ActivateEnemies()
            {
                foreach (EnemyBase enemy in enemies)
                {
                    if (enemy != null && !enemy.isDead)
                    {
                        enemy.aiController.agent.enabled = true;
                        enemy.gameObject.SetActive(true);
                    }
                }
            }

            // 이 방에 속한 적들을 비활성화합니다.
            public void DeactivateEnemies()
            {
                foreach (EnemyBase enemy in enemies)
                {
                    if (enemy != null)
                    {
                        enemy.gameObject.SetActive(false);
                    }
                }
            }
        }

        #endregion

        #region 변수 및 프로퍼티

        [Header("던전 데이터")]
        [Tooltip("던전 생성에 필요한 모든 설정값을 담고 있는 ScriptableObject")]
        [SerializeField]
        public DungeonData dungeonData;

        public static event Action<Room> OnDungeonGenerated;

        private DungeonManager _dungeonManager;
        public WorldmapController _worldmapController;
        public MinimapTileGenerator _minimapGenerator;
        [SerializeField] private DungeonNavMeshBuilder dungeonNavMeshBuilder;

        #endregion

        #region 의존성 주입 및 초기화

        // 던전 생성에 필요한 매니저와 네비메시 빌더 참조를 찾습니다.
        private void Awake()
        {
            _dungeonManager = GetComponentInParent<DungeonManager>();
            if (_dungeonManager == null)
            {
                Debug.LogError("치명적 오류: RoomFirstDungeonGenerator가 부모인 DungeonManager를 찾지 못했습니다!");
            }
            dungeonNavMeshBuilder = FindObjectOfType<DungeonNavMeshBuilder>();
            if (dungeonNavMeshBuilder == null)
            {
                Debug.LogError("dungeonNavMeshBuilder를 찾지 못했습니다!");
            }
        }

        // 월드맵과 미니맵 컨트롤러 참조를 외부에서 주입합니다.
        public void SetControllers(WorldmapController worldmapController, MinimapTileGenerator minimapGenerator)
        {
            _worldmapController = worldmapController;
            _minimapGenerator = minimapGenerator;
        }

        // 새 던전을 생성하고 저장용 스냅샷까지 만듭니다.
        public void DungeonStart()
        {
            GenerateNewDungeonAndCreateSnapshot();
        }

        #endregion

        #region 주 생성 로직 (코루틴)

        // 추상 던전 생성기의 실행 진입점에서 새 던전 생성 코루틴을 시작합니다.
        protected override IEnumerator RunProceduralGeneration()
        {
            yield return StartCoroutine(GenerateNewDungeonCoroutine());
        }

        // 타일맵 준비 후 새 던전을 생성하고 결과를 저장 데이터로 변환합니다.
        public void GenerateNewDungeonAndCreateSnapshot()
        {
            EnsureTilemapVisualizer();
            StartCoroutine(GenerateNewDungeonCoroutine());
        }

        // 저장된 던전 맵 데이터를 이용해 던전을 복원합니다.
        public void LoadDungeonFromSave(DungeonMapSaveData saveData)
        {
            EnsureTilemapVisualizer();
            StartCoroutine(LoadDungeonFromSaveCoroutine(saveData));
        }

        // 타일맵 시각화 컴포넌트 참조가 없으면 현재 오브젝트에서 다시 찾습니다.
        private void EnsureTilemapVisualizer()
        {
            if (tilemapVisualizer == null)
            {
                tilemapVisualizer = GetComponent<TilemapVisualizer>();
            }
        }

        // 새 던전 생성, 적용, 저장 스냅샷 생성을 순서대로 수행합니다.
        private IEnumerator GenerateNewDungeonCoroutine()
        {
            if (dungeonData == null)
            {
                Debug.LogError("DungeonData가 할당되지 않았습니다! 던전 생성을 중단합니다.");
                yield break;
            }

            DungeonBuildResult result = null;

            yield return StartCoroutine(BuildNewDungeon(resultValue =>
            {
                result = resultValue;
            }));

            if (result == null)
                yield break;

            yield return StartCoroutine(ApplyDungeon(result));

            DungeonMapSaveData snapshot = CreateSaveDataFromResult(result);
            _dungeonManager?.SetCurrentMapSaveData(snapshot);
            _dungeonManager?.InitializeDungeonCheckpoint(result.startRoom);
        }

        // 저장 데이터를 런타임 던전 결과로 변환한 뒤 화면과 매니저에 적용합니다.
        private IEnumerator LoadDungeonFromSaveCoroutine(DungeonMapSaveData saveData)
        {
            if (saveData == null || !saveData.hasMap)
            {
                yield break;
            }

            DungeonBuildResult result = CreateResultFromSaveData(saveData);
            if (result == null)
            {
                yield break;
            }

            yield return StartCoroutine(ApplyDungeon(result));
        }

        // 방 배치, 바닥, 포탈, 장애물, 벽 데이터를 생성해 던전 빌드 결과를 만듭니다.
        private IEnumerator BuildNewDungeon(Action<DungeonBuildResult> onComplete)
        {
            _dungeonManager?.ClearDungeonData();
            tilemapVisualizer.Clear();

            List<Vector2Int> selectedGridPositions = SelectGridPositions();
            if (selectedGridPositions.Count == 0)
            {
                onComplete?.Invoke(null);
                yield break;
            }

            Dictionary<Vector2Int, Room> roomGrid = CreateAndPlaceRooms(selectedGridPositions);
            yield return StartCoroutine(ResolveOverlapsCoroutine(roomGrid.Values.ToList()));

            Room startRoom = DesignateSpecialRooms(roomGrid);

            (Dictionary<Room, HashSet<Vector2Int>> roomFloorData, HashSet<Vector2Int> totalFloorPositions) = CreateAllFloors(roomGrid);

            (HashSet<Vector2Int> portalPositions, List<Tuple<Room, Room>> roomConnections, List<Tuple<Vector2Int, Vector2Int>> portalLinks) =
                ConnectRoomsAndCreatePortals(roomGrid, roomFloorData, totalFloorPositions);

            List<PlacedObstacleData> obstacles = PlaceObstaclesInRooms(roomFloorData, portalPositions, totalFloorPositions);

            HashSet<Vector2Int> wallPositions = WallGenerator.FindWalls(totalFloorPositions, dungeonData.wallThickness);
            List<Room> rooms = roomGrid.Values.ToList();
            AssignRoomIds(rooms);

            onComplete?.Invoke(new DungeonBuildResult
            {
                rooms = rooms,
                roomFloorData = roomFloorData,
                totalFloorPositions = totalFloorPositions,
                wallPositions = wallPositions,
                portalPositions = portalPositions,
                portalLinks = portalLinks,
                roomConnections = roomConnections,
                obstacles = obstacles,
                startRoom = startRoom
            });
        }

        // 생성 또는 복원된 던전 결과를 타일맵, 몬스터, 네비메시, 매니저에 반영합니다.
        public IEnumerator ApplyDungeon(DungeonBuildResult result)
        {
            if (result == null) yield break;

            _dungeonManager?.ClearDungeonData();
            tilemapVisualizer.Clear();

            VisualizeDungeon(
                result.totalFloorPositions,
                result.roomFloorData,
                result.portalPositions,
                result.roomConnections,
                result.obstacles,
                result.rooms,
                result.startRoom
            );

            SpawnMonstersInRooms(
                result.rooms.ToDictionary(room => room.gridPos, room => room),
                result.obstacles,
                result.roomFloorData
            );

            RegisterPortalLinks(result.portalLinks);

            if (dungeonNavMeshBuilder != null)
            {
                yield return StartCoroutine(dungeonNavMeshBuilder.RebuildNavMeshCoroutine());
            }

            FinalizeDungeonData(
                result.rooms.ToDictionary(room => room.gridPos, room => room),
                result.roomFloorData
            );

            OnDungeonGenerated?.Invoke(result.startRoom);
        }

        // 런타임 던전 결과를 JSON 저장이 가능한 던전 맵 데이터로 변환합니다.
        private DungeonMapSaveData CreateSaveDataFromResult(DungeonBuildResult result)
        {
            DungeonMapSaveData data = new DungeonMapSaveData();
            data.hasMap = true;
            data.portalsUnlocked = true;

            AssignRoomIds(result.rooms);

            for (int i = 0; i < result.rooms.Count; i++)
            {
                Room room = result.rooms[i];

                DungeonRoomSaveData roomData = new DungeonRoomSaveData
                {
                    id = room.id,
                    gridX = room.gridPos.x,
                    gridY = room.gridPos.y,
                    sizeX = room.size.x,
                    sizeY = room.size.y,
                    centerX = room.center.x,
                    centerY = room.center.y,
                    roomType = room.type.ToString(),
                    hasBossSpawnPoint = room.bossSpawnPoint.HasValue,
                    bossSpawnX = room.bossSpawnPoint?.x ?? 0f,
                    bossSpawnY = room.bossSpawnPoint?.y ?? 0f,
                    visited = room.visited,
                    cleared = room.cleared
                };

                foreach (Vector2Int tile in result.roomFloorData[room])
                {
                    roomData.floorTiles.Add(new Vector2IntSaveData { x = tile.x, y = tile.y });
                }

                data.rooms.Add(roomData);
            }

            foreach (Vector2Int wall in result.wallPositions)
                data.wallTiles.Add(new Vector2IntSaveData { x = wall.x, y = wall.y });

            foreach (Vector2Int portal in result.portalPositions)
                data.portalTiles.Add(new Vector2IntSaveData { x = portal.x, y = portal.y });

            if (result.portalLinks != null)
            {
                foreach (Tuple<Vector2Int, Vector2Int> portalLink in result.portalLinks)
                {
                    data.portalLinks.Add(new PortalLinkSaveData
                    {
                        fromX = portalLink.Item1.x,
                        fromY = portalLink.Item1.y,
                        toX = portalLink.Item2.x,
                        toY = portalLink.Item2.y
                    });
                }
            }

            Dictionary<Room, int> roomIds = result.rooms.ToDictionary(room => room, room => room.id);
            foreach (Tuple<Room, Room> connection in result.roomConnections)
            {
                if (!roomIds.ContainsKey(connection.Item1) || !roomIds.ContainsKey(connection.Item2))
                    continue;

                data.roomConnections.Add(new RoomConnectionSaveData
                {
                    fromRoomId = roomIds[connection.Item1],
                    toRoomId = roomIds[connection.Item2]
                });
            }

            foreach (PlacedObstacleData obstacle in result.obstacles)
            {
                data.obstacles.Add(new ObstacleSaveData
                {
                    prefabId = obstacle.prefab != null ? obstacle.prefab.name : "",
                    x = obstacle.worldPosition.x,
                    y = obstacle.worldPosition.y
                });
            }

            return data;
        }

        // 저장된 던전 맵 데이터를 런타임에서 사용할 던전 결과 객체로 복원합니다.
        private DungeonBuildResult CreateResultFromSaveData(DungeonMapSaveData data)
        {
            DungeonBuildResult result = new DungeonBuildResult
            {
                rooms = new List<Room>(),
                roomFloorData = new Dictionary<Room, HashSet<Vector2Int>>(),
                totalFloorPositions = new HashSet<Vector2Int>(),
                wallPositions = new HashSet<Vector2Int>(),
                portalPositions = new HashSet<Vector2Int>(),
                portalLinks = new List<Tuple<Vector2Int, Vector2Int>>(),
                roomConnections = new List<Tuple<Room, Room>>(),
                obstacles = new List<PlacedObstacleData>()
            };

            Dictionary<int, Room> roomsById = new Dictionary<int, Room>();
            foreach (DungeonRoomSaveData roomData in data.rooms)
            {
                Room room = new Room(
                    new Vector2Int(roomData.gridX, roomData.gridY),
                    new Vector2Int(roomData.sizeX, roomData.sizeY)
                );

                room.center = new Vector2(roomData.centerX, roomData.centerY);
                room.id = roomData.id;
                room.type = Enum.Parse<RoomType>(roomData.roomType);
                room.visited = roomData.visited;
                room.cleared = roomData.cleared;

                if (roomData.hasBossSpawnPoint)
                    room.bossSpawnPoint = new Vector2(roomData.bossSpawnX, roomData.bossSpawnY);

                HashSet<Vector2Int> floorSet = new HashSet<Vector2Int>();
                foreach (Vector2IntSaveData tile in roomData.floorTiles)
                {
                    Vector2Int pos = new Vector2Int(tile.x, tile.y);
                    floorSet.Add(pos);
                    result.totalFloorPositions.Add(pos);
                }

                result.rooms.Add(room);
                roomsById[room.id] = room;
                result.roomFloorData[room] = floorSet;

                if (room.type == RoomType.Start)
                    result.startRoom = room;
            }

            foreach (Vector2IntSaveData wall in data.wallTiles)
                result.wallPositions.Add(new Vector2Int(wall.x, wall.y));

            foreach (Vector2IntSaveData portal in data.portalTiles)
                result.portalPositions.Add(new Vector2Int(portal.x, portal.y));

            foreach (PortalLinkSaveData portalLink in data.portalLinks)
            {
                result.portalLinks.Add(Tuple.Create(
                    new Vector2Int(portalLink.fromX, portalLink.fromY),
                    new Vector2Int(portalLink.toX, portalLink.toY)
                ));
            }

            foreach (RoomConnectionSaveData connection in data.roomConnections)
            {
                if (roomsById.TryGetValue(connection.fromRoomId, out Room fromRoom) &&
                    roomsById.TryGetValue(connection.toRoomId, out Room toRoom))
                {
                    result.roomConnections.Add(Tuple.Create(fromRoom, toRoom));
                }
            }

            foreach (ObstacleSaveData obstacleData in data.obstacles)
            {
                GameObject prefab = FindObstaclePrefabById(obstacleData.prefabId);
                if (prefab == null) continue;

                result.obstacles.Add(new PlacedObstacleData
                {
                    prefab = prefab,
                    worldPosition = new Vector2(obstacleData.x, obstacleData.y)
                });
            }

            return result;
        }

        // 저장된 장애물 prefabId와 일치하는 장애물 프리팹을 던전 데이터에서 찾습니다.
        private GameObject FindObstaclePrefabById(string prefabId)
        {
            if (string.IsNullOrEmpty(prefabId) || dungeonData?.obstacles == null)
                return null;

            foreach (ObstacleData obstacle in dungeonData.obstacles)
            {
                if (obstacle?.prefab != null && obstacle.prefab.name == prefabId)
                    return obstacle.prefab;
            }

            return null;
        }

        // 저장과 복원에서 방을 안정적으로 참조할 수 있도록 ID를 채웁니다.
        private void AssignRoomIds(List<Room> rooms)
        {
            for (int i = 0; i < rooms.Count; i++)
            {
                if (rooms[i].id < 0)
                {
                    rooms[i].id = i;
                }
            }
        }

        // 저장되었거나 새로 생성된 포탈 연결 정보를 DungeonManager에 등록합니다.
        private void RegisterPortalLinks(List<Tuple<Vector2Int, Vector2Int>> portalLinks)
        {
            if (portalLinks == null)
                return;

            foreach (Tuple<Vector2Int, Vector2Int> portalLink in portalLinks)
            {
                _dungeonManager?.RegisterPortalPair((Vector3Int)portalLink.Item1, (Vector3Int)portalLink.Item2);
            }
        }

        #endregion

        #region --- 던전 생성 단계별 래퍼(Wrapper) 메서드 ---

        // 던전 설정값을 바탕으로 사용할 방 그리드 위치 목록을 선택합니다.
        private List<Vector2Int> SelectGridPositions()
        {
            int side = Mathf.CeilToInt(Mathf.Sqrt(dungeonData.desiredNumberOfRooms));
            Vector2Int gridSize = new Vector2Int(side * 2, side * 2);
            return RetrySelectConnectedGridPositions(gridSize);
        }

        // 선택된 그리드 위치에 방 데이터를 만들고 월드 좌표상 위치를 정렬합니다.
        private Dictionary<Vector2Int, Room> CreateAndPlaceRooms(List<Vector2Int> selectedGridPositions)
        {
            Dictionary<Vector2Int, Room> roomGrid = CreateRoomData(selectedGridPositions);
            float roomSpacing = Mathf.Max(dungeonData.maxRoomSize.x, dungeonData.maxRoomSize.y) * dungeonData.roomSpacingMultiplier;
            PlaceAndAlignRooms(roomGrid, roomSpacing);
            return roomGrid;
        }

        // 시작방, 보스방, 상점방, 아이템방 같은 특수 방을 지정합니다.
        private Room DesignateSpecialRooms(Dictionary<Vector2Int, Room> roomGrid)
        {
            Dictionary<Vector2Int, int> gridConnectionCount = CalculateGridConnections(roomGrid.Keys.ToList());
            return DesignateAllSpecialRooms(roomGrid, gridConnectionCount);
        }

        // 모든 방의 바닥 타일과 전체 바닥 타일 집합을 생성합니다.
        private (Dictionary<Room, HashSet<Vector2Int>>, HashSet<Vector2Int>) CreateAllFloors(Dictionary<Vector2Int, Room> roomGrid)
        {
            Dictionary<Room, HashSet<Vector2Int>> roomFloorData = new Dictionary<Room, HashSet<Vector2Int>>();
            HashSet<Vector2Int> totalFloorPositions = new HashSet<Vector2Int>();
            IEnumerator floorCoroutine = CreateAllRoomFloorsCoroutine(roomGrid, roomFloorData, totalFloorPositions, 1);
            while (floorCoroutine.MoveNext()) { }
            return (roomFloorData, totalFloorPositions);
        }

        // 방별 바닥 정보와 포탈 위치를 고려해 장애물 배치 데이터를 생성합니다.
        private List<PlacedObstacleData> PlaceObstaclesInRooms(Dictionary<Room, HashSet<Vector2Int>> roomFloorData, HashSet<Vector2Int> portalPositions, HashSet<Vector2Int> totalFloorPositions)
        {
            return PlaceObstacles(roomFloorData, portalPositions, totalFloorPositions);
        }

        // 방 정보와 장애물 위치를 기준으로 몬스터 스폰을 실행합니다.
        private void SpawnMonstersInRooms(Dictionary<Vector2Int, Room> roomGrid, List<PlacedObstacleData> obstacles, Dictionary<Room, HashSet<Vector2Int>> roomFloorData)
        {
            SpawnMonsters(roomGrid, obstacles, roomFloorData);
        }

        // 바닥, 벽, 포탈, 장애물, 특수 방 오브젝트를 그리고 지도 UI를 초기화합니다.
        private void VisualizeDungeon(HashSet<Vector2Int> totalFloorPositions, Dictionary<Room, HashSet<Vector2Int>> roomFloorData, HashSet<Vector2Int> portalPositions, List<Tuple<Room, Room>> roomConnections, List<PlacedObstacleData> obstaclesToPlace, List<Room> allRooms, Room startRoom)
        {
            HashSet<Vector2Int> normalFloorPositions = new HashSet<Vector2Int>(totalFloorPositions);
            List<Room> specialRooms = allRooms.Where(r => r.type != RoomType.Normal).ToList();
            foreach (Room specialRoom in specialRooms)
            {
                if (roomFloorData.ContainsKey(specialRoom))
                {
                    normalFloorPositions.ExceptWith(roomFloorData[specialRoom]);
                }
            }
            tilemapVisualizer.PaintFloorTiles(normalFloorPositions);

            tilemapVisualizer.PaintSpecialRoomFloors(specialRooms, dungeonData, roomFloorData);
            tilemapVisualizer.PaintPortals(portalPositions);

            HashSet<Vector2Int> allWallPositions = WallGenerator.FindWalls(totalFloorPositions, dungeonData.wallThickness);
            RemoveThinWalls(totalFloorPositions, allWallPositions, tilemapVisualizer, roomFloorData);

            tilemapVisualizer.PaintWallsWithRuleTile(allWallPositions);

            tilemapVisualizer.InstantiateObstacles(obstaclesToPlace);
            tilemapVisualizer.InstantiateSpecialRoomObjects(specialRooms, dungeonData);

            if (_worldmapController != null && _minimapGenerator != null)
            {
                _worldmapController.DrawMap(allRooms, roomConnections, dungeonData);
                _minimapGenerator.InitializeMap(tilemapVisualizer, obstaclesToPlace, portalPositions, roomFloorData, allRooms, startRoom);
            }
        }

        // 생성된 방 목록과 타일-방 조회 정보를 DungeonManager에 등록합니다.
        private void FinalizeDungeonData(Dictionary<Vector2Int, Room> roomGrid, Dictionary<Room, HashSet<Vector2Int>> roomFloorData)
        {
            if (_dungeonManager != null)
            {
                _dungeonManager.SetAllRooms(new List<Room>(roomGrid.Values));
                _dungeonManager.SetRoomFloorData(roomFloorData);

                Dictionary<Vector2Int, Room> roomLookup = new Dictionary<Vector2Int, Room>();
                foreach (KeyValuePair<Room, HashSet<Vector2Int>> entry in roomFloorData)
                {
                    Room room = entry.Key;
                    foreach (Vector2Int tilePos in entry.Value)
                    {
                        roomLookup[tilePos] = room;
                    }
                }
                _dungeonManager.SetRoomLookup(roomLookup);
            }
        }

        #endregion

        #region --- 세부 구현 메서드 ---

        // 일반 방의 유효한 바닥 위치에 몬스터를 무작위로 배치합니다.
        private void SpawnMonsters(Dictionary<Vector2Int, Room> roomGrid, List<PlacedObstacleData> obstacles, Dictionary<Room, HashSet<Vector2Int>> roomFloorData)
        {
            if (dungeonData.dungeonMonsters == null || dungeonData.dungeonMonsters.Count == 0) return;

            HashSet<Vector2Int> obstaclePositions = new HashSet<Vector2Int>(obstacles.Select(o => Vector2Int.RoundToInt(o.worldPosition)));

            foreach (Room room in roomGrid.Values.Where(r => r.type == RoomType.Normal && !r.cleared))
            {
                if (!roomFloorData.TryGetValue(room, out HashSet<Vector2Int> floorTiles)) continue;

                List<Vector2Int> candidatePositions = floorTiles.Where(pos => !obstaclePositions.Contains(pos)).ToList();
                if (candidatePositions.Count == 0) continue;

                float roomRatio = (float)(room.size.x * room.size.y) / (dungeonData.maxRoomSize.x * dungeonData.maxRoomSize.y);
                int monsterCount = Mathf.RoundToInt(Mathf.Lerp(1, 5, roomRatio));

                for (int i = 0; i < monsterCount; i++)
                {
                    if (candidatePositions.Count == 0) break;

                    EnemyData monsterToSpawn = dungeonData.dungeonMonsters[Random.Range(0, dungeonData.dungeonMonsters.Count)];
                    int randomIndex = Random.Range(0, candidatePositions.Count);
                    Vector2Int spawnPosition = candidatePositions[randomIndex];
                    candidatePositions.RemoveAt(randomIndex);

                    if (_dungeonManager?._objectPoolManager != null)
                    {
                        GameObject enemyObj = _dungeonManager._objectPoolManager.SpawnFromPool(monsterToSpawn.enemyName, (Vector3Int)spawnPosition + new Vector3(0.5f, 0.5f, 0), Quaternion.identity);
                        if (enemyObj != null)
                        {
                            EnemyBase enemy = enemyObj.GetComponent<EnemyBase>();
                            if (enemy != null)
                            {
                                enemy.Initialize(monsterToSpawn);
                                enemy.homeRoom = room;
                                room.enemies.Add(enemy);
                                enemyObj.SetActive(false);
                            }
                        }
                    }
                }
            }
        }


        // 인접한 방 사이의 포탈 위치를 찾고 방 연결 정보를 생성합니다.
        private (HashSet<Vector2Int> portalPositions,
         List<Tuple<Room, Room>> roomConnections,
         List<Tuple<Vector2Int, Vector2Int>> portalLinks) ConnectRoomsAndCreatePortals(Dictionary<Vector2Int, Room> roomGrid, Dictionary<Room, HashSet<Vector2Int>> roomFloorData, HashSet<Vector2Int> allFloorPositions)
        {
            HashSet<Vector2Int> portalPositions = new HashSet<Vector2Int>();
            List<Tuple<Room, Room>> finalConnections = new List<Tuple<Room, Room>>();
            List<Tuple<Vector2Int, Vector2Int>> portalLinks = new List<Tuple<Vector2Int, Vector2Int>>();
            HashSet<Tuple<Vector2Int, Vector2Int>> connectionsMade = new HashSet<Tuple<Vector2Int, Vector2Int>>();

            const int portalWidth = 3;
            const int requiredSpace = portalWidth + 2; // 포탈(3) + 양옆 여유(1+1) = 5

            foreach (Room roomA in roomGrid.Values)
            {
                foreach (Vector2Int direction in WallGenerator.Direction2D.cardinalDirectionsList)
                {
                    Vector2Int neighborGridPos = roomA.gridPos + direction;
                    if (roomGrid.TryGetValue(neighborGridPos, out Room roomB))
                    {
                        Tuple<Vector2Int, Vector2Int> connectionTuple = (roomA.gridPos.x < roomB.gridPos.x || (roomA.gridPos.x == roomB.gridPos.x && roomA.gridPos.y < roomB.gridPos.y))
                            ? Tuple.Create(roomA.gridPos, roomB.gridPos) : Tuple.Create(roomB.gridPos, roomA.gridPos);

                        if (connectionsMade.Contains(connectionTuple)) continue;

                       

                        // 1. 각 방의 경계 벽들을 찾습니다.
                        HashSet<Vector2Int> wallsA = new HashSet<Vector2Int>();
                        foreach (Vector2Int pos in roomFloorData[roomA]) if (!roomFloorData[roomA].Contains(pos + direction)) wallsA.Add(pos + direction);

                        HashSet<Vector2Int> wallsB = new HashSet<Vector2Int>();
                        foreach (Vector2Int pos in roomFloorData[roomB]) if (!roomFloorData[roomB].Contains(pos - direction)) wallsB.Add(pos - direction);

                        Vector2Int perpendicularDir = (direction.x == 0) ? Vector2Int.right : Vector2Int.up;

                        // 2. 각 방에서 포탈을 놓을 수 있는 '좋은 위치' 후보들을 모두 찾습니다.
                        List<Vector2Int> candidatesA = FindPortalCandidates(wallsA, perpendicularDir, requiredSpace);
                        List<Vector2Int> candidatesB = FindPortalCandidates(wallsB, perpendicularDir, requiredSpace);

                        Vector2Int centerA, centerB;

                        // 3. 양쪽 방 모두 '좋은 위치'를 찾았을 경우, 각자 최적의 위치를 선택합니다.
                        if (candidatesA.Count > 0 && candidatesB.Count > 0)
                        {
                            centerA = FindClosestCandidate(candidatesA, roomB.center);
                            centerB = FindClosestCandidate(candidatesB, roomA.center);
                        }
                        // 4. '좋은 위치'를 찾지 못했다면, 안전장치(가장 가까운 두 점)를 가동합니다.
                        else
                        {
                            float minSqrDist = float.MaxValue;
                            Vector2Int closestA = Vector2Int.zero, closestB = Vector2Int.zero;
                            foreach (Vector2Int wallA in wallsA)
                            {
                                foreach (Vector2Int wallB in wallsB)
                                {
                                    float sqrDist = (wallA - wallB).sqrMagnitude;
                                    if (sqrDist < minSqrDist)
                                    {
                                        minSqrDist = sqrDist;
                                        closestA = wallA;
                                        closestB = wallB;
                                    }
                                }
                            }
                            centerA = closestA;
                            centerB = closestB;
                        }

                        // 5. 최종 위치에 포탈을 생성합니다.
                        if (centerA != Vector2Int.zero && centerB != Vector2Int.zero)
                        {
                            portalLinks.Add(Tuple.Create(centerA, centerB));

                            Vector2Int spanDir = (direction.x == 0) ? Vector2Int.right : Vector2Int.up;
                            for (int i = -(portalWidth - 1) / 2; i <= (portalWidth - 1) / 2; i++)
                            {
                                portalPositions.Add(centerA + spanDir * i);
                                portalPositions.Add(centerB + spanDir * i);
                            }
                            finalConnections.Add(Tuple.Create(roomA, roomB));
                        }

                        connectionsMade.Add(connectionTuple);

                        
                    }
                }
            }
            return (portalPositions, finalConnections, portalLinks);
        }

        
        /// 벽 후보들 중에서 포탈을 놓기에 충분한 공간(예: 5칸)이 확보되는 모든 중앙 지점을 찾아 리스트로 반환합니다.
        private List<Vector2Int> FindPortalCandidates(HashSet<Vector2Int> walls, Vector2Int perpendicularDir, int requiredLength)
        {
            List<Vector2Int> candidates = new List<Vector2Int>();
            int margin = (requiredLength - 1) / 2;

            foreach (Vector2Int wall in walls)
            {
                bool hasSpace = true;
                for (int i = 1; i <= margin; i++)
                {
                    if (!walls.Contains(wall + perpendicularDir * i) || !walls.Contains(wall - perpendicularDir * i))
                    {
                        hasSpace = false;
                        break;
                    }
                }
                if (hasSpace)
                {
                    candidates.Add(wall);
                }
            }
            return candidates;
        }

        /// 후보 위치 리스트 중에서 특정 목표 지점과 가장 가까운 위치를 찾아 반환합니다.
        private Vector2Int FindClosestCandidate(List<Vector2Int> candidates, Vector2 targetPosition)
        {
            Vector2Int closest = Vector2Int.zero;
            float minSqrDist = float.MaxValue;

            foreach (Vector2Int candidate in candidates)
            {
                float sqrDist = ((Vector2)candidate - targetPosition).sqrMagnitude;
                if (sqrDist < minSqrDist)
                {
                    minSqrDist = sqrDist;
                    closest = candidate;
                }
            }
            return closest;
        }

        // 포탈 접근 구역과 벽을 피해서 일반 방에 장애물을 배치합니다.
        private List<PlacedObstacleData> PlaceObstacles(Dictionary<Room, HashSet<Vector2Int>> roomFloorData, HashSet<Vector2Int> portalPositions, HashSet<Vector2Int> allFloorTiles)
        {
            List<PlacedObstacleData> obstaclesToPlace = new List<PlacedObstacleData>();
            HashSet<Vector2Int> allWallPositions = WallGenerator.FindWalls(allFloorTiles, dungeonData.wallThickness);

            HashSet<Vector2Int> portalExclusionZone = new HashSet<Vector2Int>();
            int portalExclusionRadius = 2;

            foreach (Vector2Int portalPos in portalPositions)
            {
                for (int x = -portalExclusionRadius; x <= portalExclusionRadius; x++)
                {
                    for (int y = -portalExclusionRadius; y <= portalExclusionRadius; y++)
                    {
                        portalExclusionZone.Add(portalPos + new Vector2Int(x, y));
                    }
                }

                foreach (Vector2Int direction in WallGenerator.Direction2D.cardinalDirectionsList)
                {
                    Vector2Int accessPoint = portalPos + direction;
                    if (allFloorTiles.Contains(accessPoint))
                    {
                        for (int x = -portalExclusionRadius; x <= portalExclusionRadius; x++)
                        {
                            for (int y = -portalExclusionRadius; y <= portalExclusionRadius; y++)
                            {
                                portalExclusionZone.Add(accessPoint + new Vector2Int(x, y));
                            }
                        }
                    }
                }
            }


            foreach (KeyValuePair<Room, HashSet<Vector2Int>> roomData in roomFloorData)
            {
                if (roomData.Key.type != RoomType.Normal) continue;

                HashSet<Vector2Int> candidatePositions = new HashSet<Vector2Int>(roomData.Value);
                int numberOfObstacles = Random.Range(dungeonData.minObstaclesPerRoom, dungeonData.maxObstaclesPerRoom + 1);

                for (int i = 0; i < numberOfObstacles && candidatePositions.Count > 1; i++)
                {
                    if (dungeonData.obstacles.Length == 0) break;
                    ObstacleData selectedObstacleData = dungeonData.obstacles[Random.Range(0, dungeonData.obstacles.Length)];

                    if (selectedObstacleData.prefab == null) continue;

                    Vector2Int obstacleSize = selectedObstacleData.size;
                    int marginX = Mathf.CeilToInt((obstacleSize.x - 1) / 2.0f);
                    int marginY = Mathf.CeilToInt((obstacleSize.y - 1) / 2.0f);

                    List<Vector2Int> validPlacementSpots = new List<Vector2Int>();
                    foreach (Vector2Int potentialCenter in candidatePositions)
                    {
                        bool isSafe = true;
                        for (int x = -marginX; x <= marginX; x++)
                        {
                            for (int y = -marginY; y <= marginY; y++)
                            {
                                Vector2Int checkPos = potentialCenter + new Vector2Int(x, y);
                                if (allWallPositions.Contains(checkPos) || portalExclusionZone.Contains(checkPos) || !roomData.Value.Contains(checkPos))
                                {
                                    isSafe = false;
                                    break;
                                }
                            }
                            if (!isSafe) break;
                        }

                        if (isSafe)
                        {
                            validPlacementSpots.Add(potentialCenter);
                        }
                    }

                    if (validPlacementSpots.Count > 0)
                    {
                        Vector2Int placementCenter = validPlacementSpots[Random.Range(0, validPlacementSpots.Count)];
                        obstaclesToPlace.Add(new PlacedObstacleData
                        {
                            prefab = selectedObstacleData.prefab,
                            worldPosition = (Vector2)placementCenter + new Vector2(0.5f, 0.5f)
                        });

                        int exclusionMarginX = marginX + 1;
                        int exclusionMarginY = marginY + 1;
                        for (int x = -exclusionMarginX; x <= exclusionMarginX; x++)
                        {
                            for (int y = -exclusionMarginY; y <= exclusionMarginY; y++)
                            {
                                candidatePositions.Remove(placementCenter + new Vector2Int(x, y));
                            }
                        }
                    }
                }
            }
            return obstaclesToPlace;
        }



        // 방 개수를 줄이지 않고, 필요한 막다른 방 수를 만족하는 연결 그리드를 찾습니다.
        private List<Vector2Int> RetrySelectConnectedGridPositions(Vector2Int gridSize)
        {
            int requiredDeadEnds = 2 + dungeonData.numberOfShopRooms + dungeonData.numberOfItemRooms;
            int desiredRoomCount = dungeonData.desiredNumberOfRooms;
            if (requiredDeadEnds > desiredRoomCount)
            {
                Debug.LogWarning($"특수방 배치에 필요한 막다른 방 수({requiredDeadEnds})가 전체 방 수({desiredRoomCount})보다 많습니다. 일부 특수방은 배치되지 않을 수 있습니다.");
                requiredDeadEnds = desiredRoomCount;
            }

            int maxAttempts = 500;
            for (int i = 0; i < maxAttempts; i++)
            {
                List<Vector2Int> candidatePositions = SelectConnectedGridPositions(gridSize);
                if (candidatePositions.Count == desiredRoomCount &&
                    CountDeadEnds(candidatePositions) >= requiredDeadEnds)
                {
                    return candidatePositions;
                }
            }

            List<Vector2Int> guaranteedPositions = SelectTreeGridPositions(gridSize, requiredDeadEnds);
            if (guaranteedPositions.Count == desiredRoomCount)
            {
                return guaranteedPositions;
            }

            Debug.LogWarning($"요청한 방 개수({desiredRoomCount})를 정확히 만족하는 던전 그리드 생성에 실패했습니다. 마지막 후보를 사용합니다.");
            return SelectConnectedGridPositions(gridSize);
        }

        // 새 방을 항상 기존 방 하나에만 붙여, 정확한 방 개수와 막다른 방을 확보하기 쉬운 트리형 그리드를 만듭니다.
        private List<Vector2Int> SelectTreeGridPositions(Vector2Int gridSize, int requiredDeadEnds)
        {
            int desiredRoomCount = dungeonData.desiredNumberOfRooms;
            int maxAttempts = 1000;

            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                List<Vector2Int> positions = new List<Vector2Int>();
                HashSet<Vector2Int> occupied = new HashSet<Vector2Int>();
                Vector2Int startPos = new Vector2Int(Random.Range(0, gridSize.x), Random.Range(0, gridSize.y));

                positions.Add(startPos);
                occupied.Add(startPos);

                int safetyBreak = gridSize.x * gridSize.y * 4;
                while (positions.Count < desiredRoomCount && safetyBreak-- > 0)
                {
                    List<Vector2Int> shuffledAnchors = positions.OrderBy(_ => Random.value).ToList();
                    bool added = false;

                    foreach (Vector2Int anchor in shuffledAnchors)
                    {
                        List<Vector2Int> directions = WallGenerator.Direction2D.cardinalDirectionsList.OrderBy(_ => Random.value).ToList();
                        foreach (Vector2Int direction in directions)
                        {
                            Vector2Int candidate = anchor + direction;
                            if (!IsGridPositionValid(candidate, gridSize) || occupied.Contains(candidate))
                                continue;

                            if (CountOccupiedNeighbors(candidate, occupied) != 1)
                                continue;

                            positions.Add(candidate);
                            occupied.Add(candidate);
                            added = true;
                            break;
                        }

                        if (added)
                            break;
                    }

                    if (!added)
                        break;
                }

                if (positions.Count == desiredRoomCount && CountDeadEnds(positions) >= requiredDeadEnds)
                {
                    return positions;
                }
            }

            return new List<Vector2Int>();
        }

        // 선택된 그리드에서 연결 수가 1인 막다른 방 개수를 셉니다.
        private int CountDeadEnds(List<Vector2Int> positions)
        {
            if (positions.Count <= 1)
                return positions.Count;

            return CalculateGridConnections(positions).Values.Count(count => count == 1);
        }

        // 새 후보 칸이 기존 방 몇 개와 맞닿는지 계산합니다.
        private int CountOccupiedNeighbors(Vector2Int position, HashSet<Vector2Int> occupied)
        {
            int count = 0;
            foreach (Vector2Int direction in WallGenerator.Direction2D.cardinalDirectionsList)
            {
                if (occupied.Contains(position + direction))
                {
                    count++;
                }
            }

            return count;

        }

        // 주어진 방 그리드 목록이 하나의 연결 그래프인지 검사합니다.
        private bool IsGraphConnected(List<Vector2Int> positions, Vector2Int startNode)
        {
            if (positions.Count == 0) return true;

            Queue<Vector2Int> queue = new Queue<Vector2Int>();
            HashSet<Vector2Int> visited = new HashSet<Vector2Int>();

            queue.Enqueue(startNode);
            visited.Add(startNode);

            while (queue.Count > 0)
            {
                Vector2Int current = queue.Dequeue();
                foreach (Vector2Int dir in WallGenerator.Direction2D.cardinalDirectionsList)
                {
                    Vector2Int neighbor = current + dir;
                    if (positions.Contains(neighbor) && !visited.Contains(neighbor))
                    {
                        visited.Add(neighbor);
                        queue.Enqueue(neighbor);
                    }
                }
            }

            return visited.Count == positions.Count;
        }

        // 막다른 방을 우선 사용해 보스방, 시작방, 상점방, 아이템방을 지정합니다.
        private Room DesignateAllSpecialRooms(Dictionary<Vector2Int, Room> roomGrid, Dictionary<Vector2Int, int> gridConnectionCount)
        {
            List<Room> deadEndRooms = roomGrid.Values
       .Where(room => gridConnectionCount.ContainsKey(room.gridPos) && gridConnectionCount[room.gridPos] == 1)
       .OrderBy(r => Random.value).ToList();

            if (deadEndRooms.Count < 2)
            {
                Debug.LogWarning("특수 방을 지정할 막다른 길이 부족합니다. 임의의 방을 지정합니다.");
                List<Room> allRooms = roomGrid.Values.ToList();
                Room bossRoomFallback = allRooms[0];
                bossRoomFallback.type = RoomType.Boss;
                Room startRoomFallback = allRooms.Count > 1 ? allRooms[1] : allRooms[0];
                startRoomFallback.type = RoomType.Start;
                return startRoomFallback;
            }

            Tuple<Room, Room> farthestPair = FindFarthestDeadEndPair(deadEndRooms, roomGrid);
            Room startRoom = farthestPair.Item1;
            Room bossRoom = farthestPair.Item2;

            startRoom.type = RoomType.Start;
            bossRoom.type = RoomType.Boss;
            deadEndRooms.Remove(startRoom);
            deadEndRooms.Remove(bossRoom);

            Action<RoomType, int> designateRooms = (type, count) =>
            {
                for (int i = 0; i < count && deadEndRooms.Count > 0; i++)
                {
                    deadEndRooms[0].type = type;
                    deadEndRooms.RemoveAt(0);
                }
            };
            designateRooms(RoomType.Shop, dungeonData.numberOfShopRooms);
            designateRooms(RoomType.Item, dungeonData.numberOfItemRooms);

            return startRoom;
        }

        // 막다른 방들 중 그래프 이동 거리가 가장 먼 두 방을 찾아 시작방과 보스방 후보로 사용합니다.
        private Tuple<Room, Room> FindFarthestDeadEndPair(List<Room> deadEndRooms, Dictionary<Vector2Int, Room> roomGrid)
        {
            Room bestStartRoom = deadEndRooms[0];
            Room bestBossRoom = deadEndRooms[1];
            int bestGraphDistance = -1;
            float bestWorldDistance = -1f;

            for (int i = 0; i < deadEndRooms.Count; i++)
            {
                for (int j = i + 1; j < deadEndRooms.Count; j++)
                {
                    Room roomA = deadEndRooms[i];
                    Room roomB = deadEndRooms[j];
                    int graphDistance = CalculateGridDistance(roomA.gridPos, roomB.gridPos, roomGrid);
                    float worldDistance = Vector2.Distance(roomA.center, roomB.center);

                    if (graphDistance > bestGraphDistance ||
                        (graphDistance == bestGraphDistance && worldDistance > bestWorldDistance))
                    {
                        bestGraphDistance = graphDistance;
                        bestWorldDistance = worldDistance;
                        bestStartRoom = roomA;
                        bestBossRoom = roomB;
                    }
                }
            }

            return Tuple.Create(bestStartRoom, bestBossRoom);
        }

        // 방 그리드 연결을 따라 두 방 사이의 최단 이동 거리를 계산합니다.
        private int CalculateGridDistance(Vector2Int start, Vector2Int target, Dictionary<Vector2Int, Room> roomGrid)
        {
            Queue<Vector2Int> queue = new Queue<Vector2Int>();
            Dictionary<Vector2Int, int> distances = new Dictionary<Vector2Int, int>();

            queue.Enqueue(start);
            distances[start] = 0;

            while (queue.Count > 0)
            {
                Vector2Int current = queue.Dequeue();
                if (current == target)
                {
                    return distances[current];
                }

                foreach (Vector2Int direction in WallGenerator.Direction2D.cardinalDirectionsList)
                {
                    Vector2Int neighbor = current + direction;
                    if (!roomGrid.ContainsKey(neighbor) || distances.ContainsKey(neighbor))
                        continue;

                    distances[neighbor] = distances[current] + 1;
                    queue.Enqueue(neighbor);
                }
            }

            return -1;
        }

        // 각 그리드 위치가 상하좌우로 몇 개의 방과 연결되는지 계산합니다.
        private Dictionary<Vector2Int, int> CalculateGridConnections(List<Vector2Int> gridPositions)
        {
            Dictionary<Vector2Int, int> connectionCount = new Dictionary<Vector2Int, int>();
            HashSet<Vector2Int> positionSet = new HashSet<Vector2Int>(gridPositions);
            foreach (Vector2Int pos in gridPositions)
            {
                int count = 0;
                foreach (Vector2Int dir in WallGenerator.Direction2D.cardinalDirectionsList)
                {
                    if (positionSet.Contains(pos + dir)) count++;
                }
                connectionCount[pos] = count;
            }
            return connectionCount;
        }

        // 시작 위치에서 인접 칸을 확장하며 연결된 방 그리드 목록을 선택합니다.
        private List<Vector2Int> SelectConnectedGridPositions(Vector2Int gridSize)
        {
            List<Vector2Int> selectedPositions = new List<Vector2Int>();
            if (dungeonData.desiredNumberOfRooms == 0) return selectedPositions;

            List<Vector2Int> frontier = new List<Vector2Int>();
            Vector2Int startPos = new Vector2Int(Random.Range(0, gridSize.x), Random.Range(0, gridSize.y));

            selectedPositions.Add(startPos);
            foreach (Vector2Int dir in WallGenerator.Direction2D.cardinalDirectionsList)
            {
                Vector2Int neighbor = startPos + dir;
                if (IsGridPositionValid(neighbor, gridSize)) frontier.Add(neighbor);
            }

            while (selectedPositions.Count < dungeonData.desiredNumberOfRooms && frontier.Count > 0)
            {
                int frontierIndex = Random.Range(0, frontier.Count);
                Vector2Int nextPos = frontier[frontierIndex];
                frontier.RemoveAt(frontierIndex);

                if (selectedPositions.Contains(nextPos)) continue;

                selectedPositions.Add(nextPos);
                foreach (Vector2Int dir in WallGenerator.Direction2D.cardinalDirectionsList)
                {
                    Vector2Int neighbor = nextPos + dir;
                    if (IsGridPositionValid(neighbor, gridSize)) frontier.Add(neighbor);
                }
            }
            return selectedPositions;
        }

        // 그리드 위치가 던전 그리드 범위 안에 있는지 확인합니다.
        private bool IsGridPositionValid(Vector2Int pos, Vector2Int gridSize)
        {
            return pos.x >= 0 && pos.x < gridSize.x && pos.y >= 0 && pos.y < gridSize.y;
        }

        // 선택된 그리드마다 무작위 크기의 Room 데이터를 생성합니다.
        private Dictionary<Vector2Int, Room> CreateRoomData(List<Vector2Int> selectedGridPositions)
        {
            Dictionary<Vector2Int, Room> roomGrid = new Dictionary<Vector2Int, Room>();
            foreach (Vector2Int gridPos in selectedGridPositions)
            {
                Vector2Int roomSize = new Vector2Int(
                  Random.Range(dungeonData.minRoomSize.x, dungeonData.maxRoomSize.x + 1),
                  Random.Range(dungeonData.minRoomSize.y, dungeonData.maxRoomSize.y + 1));
                roomGrid.Add(gridPos, new Room(gridPos, roomSize));
            }
            return roomGrid;
        }

        // 연결된 방들을 기준 방에서부터 BFS로 배치해 중심 좌표를 정합니다.
        private void PlaceAndAlignRooms(Dictionary<Vector2Int, Room> roomGrid, float roomSpacing)
        {
            Queue<Room> queue = new Queue<Room>();
            HashSet<Vector2Int> visited = new HashSet<Vector2Int>();
            Room startRoom = roomGrid.Values.First();
            startRoom.center = Vector2.zero;
            queue.Enqueue(startRoom);
            visited.Add(startRoom.gridPos);

            while (queue.Count > 0)
            {
                Room parentRoom = queue.Dequeue();
                List<Vector2Int> shuffledDirections = WallGenerator.Direction2D.cardinalDirectionsList.OrderBy(d => Random.value).ToList();
                foreach (Vector2Int direction in shuffledDirections)
                {
                    Vector2Int neighborGridPos = parentRoom.gridPos + direction;
                    if (roomGrid.TryGetValue(neighborGridPos, out Room neighborRoom) && !visited.Contains(neighborGridPos))
                    {
                        float xOffset = (parentRoom.size.x + neighborRoom.size.x) / 2f + roomSpacing;
                        float yOffset = (parentRoom.size.y + neighborRoom.size.y) / 2f + roomSpacing;
                        Vector2 offsetVector = new Vector2(direction.x * xOffset, direction.y * yOffset);
                        neighborRoom.center = parentRoom.center + offsetVector;
                        visited.Add(neighborGridPos);
                        queue.Enqueue(neighborRoom);
                    }
                }
            }
        }

        // 반복적으로 겹치는 방을 밀어내어 방 사이의 최소 간격을 확보합니다.
        private IEnumerator ResolveOverlapsCoroutine(List<Room> rooms)
        {
            float roomSpacing = Mathf.Max(dungeonData.maxRoomSize.x, dungeonData.maxRoomSize.y) * dungeonData.roomSpacingMultiplier;
            for (int i = 0; i < dungeonData.placementIterations; i++)
            {
                for (int j = 0; j < rooms.Count; j++)
                {
                    for (int k = j + 1; k < rooms.Count; k++)
                    {
                        Room roomA = rooms[j];
                        Room roomB = rooms[k];
                        float minDistanceX = (roomA.size.x + roomB.size.x) / 2f + roomSpacing;
                        float minDistanceY = (roomA.size.y + roomB.size.y) / 2f + roomSpacing;
                        Vector2 delta = roomA.center - roomB.center;

                        if (Mathf.Abs(delta.x) < minDistanceX && Mathf.Abs(delta.y) < minDistanceY)
                        {
                            if (delta == Vector2.zero) delta = Random.insideUnitCircle;
                            roomA.center += delta.normalized * 0.5f;
                            roomB.center -= delta.normalized * 0.5f;
                        }
                    }
                }
                yield return null;
            }
        }

        // 방 타입에 따라 프리팹 또는 절차 생성 방식으로 모든 방의 바닥을 만듭니다.
        private IEnumerator CreateAllRoomFloorsCoroutine(Dictionary<Vector2Int, Room> roomGrid, Dictionary<Room, HashSet<Vector2Int>> roomFloorData, HashSet<Vector2Int> totalFloor, int offset)
        {
            foreach (Room room in roomGrid.Values)
            {
                HashSet<Vector2Int> roomFloor;

                if (room.type == RoomType.Boss && dungeonData.bossRoomPrefab != null)
                {
                    Tilemap prefabTilemap = dungeonData.bossRoomPrefab.GetComponentInChildren<Tilemap>();
                    if (prefabTilemap != null)
                    {
                        roomFloor = CreateFloorFromPrefab(room, prefabTilemap);
                    }
                    else
                    {
                        Debug.LogWarning($"'{dungeonData.bossRoomPrefab.name}' 프리팹에 Tilemap 컴포넌트가 없어 기본 사각형으로 바닥을 생성합니다.");
                        roomFloor = CreateRectangularFloor(room.Bounds, offset);
                    }

                    BossSpawnPoint spawnPointMarker = dungeonData.bossRoomPrefab.GetComponentInChildren<BossSpawnPoint>();
                    if (spawnPointMarker != null)
                    {
                        room.bossSpawnPoint = room.center + (Vector2)spawnPointMarker.transform.localPosition;
                    }
                    else
                    {
                        room.bossSpawnPoint = room.center;
                        Debug.LogWarning($"'{dungeonData.bossRoomPrefab.name}' 프리팹에 BossSpawnPoint 컴포넌트가 없어 방의 중심으로 스폰 위치를 설정합니다.");
                    }
                }
                else if (room.type == RoomType.Shop && dungeonData.ShopRoomPrefab != null)
                {
                    Tilemap prefabTilemap = dungeonData.ShopRoomPrefab.GetComponentInChildren<Tilemap>();
                    if (prefabTilemap != null)
                    {
                        roomFloor = CreateFloorFromPrefab(room, prefabTilemap);
                    }
                    else
                    {
                        Debug.LogWarning($"'{dungeonData.bossRoomPrefab.name}' 프리팹에 Tilemap 컴포넌트가 없어 기본 사각형으로 바닥을 생성합니다.");
                        roomFloor = CreateRectangularFloor(room.Bounds, offset);
                    }
                    //추후 오브젝트 추가
                }
                else if (room.type == RoomType.Start && dungeonData.StartRoomPrefab != null)
                {
                    Tilemap prefabTilemap = dungeonData.StartRoomPrefab.GetComponentInChildren<Tilemap>();
                    if (prefabTilemap != null)
                    {
                        roomFloor = CreateFloorFromPrefab(room, prefabTilemap);
                    }
                    else
                    {
                        Debug.LogWarning($"'{dungeonData.bossRoomPrefab.name}' 프리팹에 Tilemap 컴포넌트가 없어 기본 사각형으로 바닥을 생성합니다.");
                        roomFloor = CreateRectangularFloor(room.Bounds, offset);
                    }
                    //추후 오브젝트 추가
                }
                else
                {
                    roomFloor = CreateCompoundRoom(room, offset);
                }

                roomFloorData.Add(room, roomFloor);
                totalFloor.UnionWith(roomFloor);
                yield return null;
            }
        }

        // 방 프리팹의 Tilemap 타일을 현재 방 중심 기준 월드 좌표로 변환합니다.
        private HashSet<Vector2Int> CreateFloorFromPrefab(Room room, Tilemap prefabTilemap)
        {
            HashSet<Vector2Int> floorPositions = new HashSet<Vector2Int>();
            foreach (Vector3Int tilePos in prefabTilemap.cellBounds.allPositionsWithin)
            {
                if (prefabTilemap.HasTile(tilePos))
                {
                    Vector2Int worldPos = (Vector2Int)Vector3Int.RoundToInt(room.center) + (Vector2Int)tilePos;
                    floorPositions.Add(worldPos);
                }
            }
            return floorPositions;
        }

        // 기본 사각형 방에 가지 형태의 추가 영역을 붙여 복합형 방을 만듭니다.
        private HashSet<Vector2Int> CreateCompoundRoom(Room room, int offset)
        {
            if (Random.value > dungeonData.compoundRoomChance)
                return CreateRectangularFloor(room.Bounds, offset);

            HashSet<Vector2Int> floor = new HashSet<Vector2Int>();
            BoundsInt mainBounds = room.Bounds;

            floor.UnionWith(CreateRectangularFloor(mainBounds, offset));

            int numberOfBranches = Random.Range(2, 4);

            for (int i = 0; i < numberOfBranches; i++)
            {
                List<Vector2Int> edgeTiles = FindEdgeTiles(floor);
                if (edgeTiles.Count == 0) break;

                Vector2Int anchorTile = edgeTiles[Random.Range(0, edgeTiles.Count)];

                int width = Random.Range(dungeonData.minRoomSize.x / 2, dungeonData.maxRoomSize.x / 2);
                int height = Random.Range(dungeonData.minRoomSize.y / 2, dungeonData.maxRoomSize.y / 2);
                width = Mathf.Max(3, width);
                height = Mathf.Max(3, height);

                Vector2Int placementDirection = Vector2Int.zero;
                foreach (Vector2Int dir in WallGenerator.Direction2D.cardinalDirectionsList)
                {
                    if (!floor.Contains(anchorTile + dir))
                    {
                        placementDirection = dir;
                        break;
                    }
                }
                if (placementDirection == Vector2Int.zero) continue;

                Vector3Int newRectCenter = (Vector3Int)anchorTile + new Vector3Int(
         placementDirection.x * (width / 2),
         placementDirection.y * (height / 2),
         0);

                BoundsInt newRect = new BoundsInt(newRectCenter - new Vector3Int(width / 2, height / 2, 0), new Vector3Int(width, height, 1));

                floor.UnionWith(CreateRectangularFloor(newRect, 0));
            }

            return floor;
        }

        // 바닥 집합에서 외곽에 해당하는 타일들을 찾습니다.
        private List<Vector2Int> FindEdgeTiles(HashSet<Vector2Int> floor)
        {
            List<Vector2Int> edgeTiles = new List<Vector2Int>();
            foreach (Vector2Int tile in floor)
            {
                foreach (Vector2Int direction in WallGenerator.Direction2D.cardinalDirectionsList)
                {
                    if (!floor.Contains(tile + direction))
                    {
                        edgeTiles.Add(tile);
                        break;
                    }
                }
            }
            return edgeTiles;
        }

       /* private GameObject GetRoomPrefab(RoomType type)
        {
            return type switch
            {
                RoomType.Start => dungeonData.StartRoomPrefab,
                RoomType.Shop => dungeonData.ShopRoomPrefab,
                RoomType.Item => dungeonData.ItemRoomPrefab,
                RoomType.Boss => dungeonData.bossRoomPrefab,
                RoomType.Normal=>null,
            };
        }*/
        // 방 Bounds 안쪽에 offset을 적용한 사각형 바닥 타일 집합을 생성합니다.
        private HashSet<Vector2Int> CreateRectangularFloor(BoundsInt roomBounds, int offset)
        {
            HashSet<Vector2Int> floor = new HashSet<Vector2Int>();
            for (int col = offset; col < roomBounds.size.x - offset; col++)
            {
                for (int row = offset; row < roomBounds.size.y - offset; row++)
                {
                    Vector2Int position = (Vector2Int)roomBounds.min + new Vector2Int(col, row);
                    floor.Add(position);
                }
            }
            return floor;
        }

        // 너무 얇게 남은 벽을 바닥으로 바꿔 이동과 시야가 어색한 구간을 줄입니다.
        private void RemoveThinWalls(
    HashSet<Vector2Int> allFloorPositions,
    HashSet<Vector2Int> allWallPositions,
    TilemapVisualizer visualizer,
    Dictionary<Room, HashSet<Vector2Int>> roomFloorData)
        {
            Dictionary<Vector2Int, Room> tileToRoomMap = new Dictionary<Vector2Int, Room>();
            foreach (KeyValuePair<Room, HashSet<Vector2Int>> entry in roomFloorData)
            {
                foreach (Vector2Int tilePos in entry.Value)
                {
                    tileToRoomMap[tilePos] = entry.Key;
                }
            }

            Dictionary<Room, HashSet<Vector2Int>> wallsToConvertByRoom = new Dictionary<Room, HashSet<Vector2Int>>();
            HashSet<Vector2Int> totalWallsToConvert = new HashSet<Vector2Int>();

            foreach (Vector2Int wallPos in new List<Vector2Int>(allWallPositions))
            {
                if (totalWallsToConvert.Contains(wallPos)) continue;

                Room adjacentRoom = null;
                foreach (Vector2Int dir in WallGenerator.Direction2D.cardinalDirectionsList)
                {
                    if (tileToRoomMap.TryGetValue(wallPos + dir, out Room room))
                    {
                        adjacentRoom = room;
                        break;
                    }
                }
                if (adjacentRoom == null) continue;

                Action<Vector2Int> addWall = (pos) =>
                {
                    if (!wallsToConvertByRoom.ContainsKey(adjacentRoom))
                    {
                        wallsToConvertByRoom[adjacentRoom] = new HashSet<Vector2Int>();
                    }
                    wallsToConvertByRoom[adjacentRoom].Add(pos);
                    totalWallsToConvert.Add(pos);
                };

                if (allFloorPositions.Contains(wallPos + Vector2Int.up) && allFloorPositions.Contains(wallPos + Vector2Int.down))
                {
                    addWall(wallPos); continue;
                }
                if (allFloorPositions.Contains(wallPos + Vector2Int.left) && allFloorPositions.Contains(wallPos + Vector2Int.right))
                {
                    addWall(wallPos); continue;
                }
                if (allFloorPositions.Contains(wallPos + Vector2Int.down) && allWallPositions.Contains(wallPos + Vector2Int.up) && allFloorPositions.Contains(wallPos + Vector2Int.up * 2))
                {
                    addWall(wallPos); addWall(wallPos + Vector2Int.up); continue;
                }
                if (allFloorPositions.Contains(wallPos + Vector2Int.left) && allWallPositions.Contains(wallPos + Vector2Int.right) && allFloorPositions.Contains(wallPos + Vector2Int.right * 2))
                {
                    addWall(wallPos); addWall(wallPos + Vector2Int.right); continue;
                }
            }

            if (totalWallsToConvert.Count > 0)
            {
                allWallPositions.ExceptWith(totalWallsToConvert);
                allFloorPositions.UnionWith(totalWallsToConvert);

                foreach (KeyValuePair<Room, HashSet<Vector2Int>> entry in wallsToConvertByRoom)
                {
                    Room room = entry.Key;
                    HashSet<Vector2Int> positions = entry.Value;

                    TileBase tileToUse = visualizer.GetTileForRoomType(room.type);

                    visualizer.PaintTiles(positions, visualizer.floorTilemap, tileToUse);
                }
            }
        }
        #endregion
    }
}
