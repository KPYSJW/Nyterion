using Nytherion.Core.Managers;
using Nytherion.Data.ScriptableObjects.Enemy;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;
using Random = UnityEngine.Random;
using VContainer;
using Nytherion.GamePlay.Characters.Enemy;
using Nytherion.Data.ScriptableObjects.Dungeon;
using Nytherion.UI.Map;

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

        public enum RoomType { Normal, Start, Boss, Shop, Item }

        public class Room
        {
            public Vector2Int gridPos;
            public Vector2Int size;
            public Vector2 center;
            public List<EnemyBase> enemies = new List<EnemyBase>();
            public RoomType type = RoomType.Normal;
            public Vector2? bossSpawnPoint;
            public BoundsInt Bounds => new BoundsInt(Vector3Int.RoundToInt(center - (Vector2)size / 2), (Vector3Int)size);

            public Room(Vector2Int gridPos, Vector2Int size)
            {
                this.gridPos = gridPos;
                this.size = size;
                this.enemies = new List<EnemyBase>();
            }

            public void ActivateEnemies()
            {
                foreach (EnemyBase enemy in enemies)
                {
                    if (enemy != null && !enemy.isDead)
                    {
                        enemy.gameObject.SetActive(true);
                    }
                }
            }

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

        #endregion

        #region 의존성 주입 및 초기화

        /* [Inject]
         public void Construct(
             DungeonManager dungeonManager,
             WorldmapController worldmapController,
             MinimapTileGenerator minimapGenerator)
         {
             _dungeonManager = dungeonManager;
             _worldmapController = worldmapController;
             _minimapGenerator = minimapGenerator;
         }*/
        private void Awake()
        {
            _dungeonManager = GetComponentInParent<DungeonManager>();
            if (_dungeonManager == null)
            {
                Debug.LogError("치명적 오류: RoomFirstDungeonGenerator가 부모인 DungeonManager를 찾지 못했습니다!");
            }
        }

        public void SetControllers(WorldmapController worldmapController, MinimapTileGenerator minimapGenerator)
        {
            _worldmapController = worldmapController;
            _minimapGenerator = minimapGenerator;
        }

        public void DungeonStart()
        {
            if (tilemapVisualizer == null)
            {
                tilemapVisualizer = GetComponent<TilemapVisualizer>();
            }
            StartCoroutine(RunProceduralGeneration());
        }

        #endregion

        #region 주 생성 로직 (코루틴)

        // ... (이 아래로 기존 RoomFirstDungeonGenerator.cs의 모든 코드를 그대로 붙여넣으면 돼) ...
        // (RunProceduralGeneration, VisualizeDungeon 등 모든 메서드 포함)

        protected override IEnumerator RunProceduralGeneration()
        {
            if (dungeonData == null)
            {
                Debug.LogError("DungeonData가 할당되지 않았습니다! 던전 생성을 중단합니다.");
                yield break;
            }

            _dungeonManager?.ClearDungeonData();
            tilemapVisualizer.Clear();

            List<Vector2Int> selectedGridPositions = SelectGridPositions();
            if (selectedGridPositions.Count == 0) yield break;
            yield return null;

            Dictionary<Vector2Int, Room> roomGrid = CreateAndPlaceRooms(selectedGridPositions);
            yield return StartCoroutine(ResolveOverlapsCoroutine(roomGrid.Values.ToList()));

            Room startRoom = DesignateSpecialRooms(roomGrid);
            yield return null;

            (Dictionary<Room, HashSet<Vector2Int>> roomFloorData, HashSet<Vector2Int> totalFloorPositions) = CreateAllFloors(roomGrid);
            (HashSet<Vector2Int> portalPositions, List<Tuple<Room, Room>> roomConnections) = ConnectRoomsAndCreatePortals(roomGrid, roomFloorData, totalFloorPositions);
            yield return null;

            List<PlacedObstacleData> obstaclesToPlace = PlaceObstaclesInRooms(roomFloorData, portalPositions, totalFloorPositions);
            SpawnMonstersInRooms(roomGrid, obstaclesToPlace, roomFloorData);
            yield return null;

            VisualizeDungeon(totalFloorPositions, roomFloorData, portalPositions, roomConnections, obstaclesToPlace, roomGrid.Values.ToList(),startRoom);

            FinalizeDungeonData(roomGrid, roomFloorData);
            OnDungeonGenerated?.Invoke(startRoom);
        }

        #endregion

        #region --- 던전 생성 단계별 래퍼(Wrapper) 메서드 ---

        private List<Vector2Int> SelectGridPositions()
        {
            int side = Mathf.CeilToInt(Mathf.Sqrt(dungeonData.desiredNumberOfRooms));
            Vector2Int gridSize = new Vector2Int(side * 2, side * 2);
            return RetrySelectConnectedGridPositions(gridSize);
        }

        private Dictionary<Vector2Int, Room> CreateAndPlaceRooms(List<Vector2Int> selectedGridPositions)
        {
            Dictionary<Vector2Int, Room> roomGrid = CreateRoomData(selectedGridPositions);
            float roomSpacing = Mathf.Max(dungeonData.maxRoomSize.x, dungeonData.maxRoomSize.y) * dungeonData.roomSpacingMultiplier;
            PlaceAndAlignRooms(roomGrid, roomSpacing);
            return roomGrid;
        }

        private Room DesignateSpecialRooms(Dictionary<Vector2Int, Room> roomGrid)
        {
            Dictionary<Vector2Int, int> gridConnectionCount = CalculateGridConnections(roomGrid.Keys.ToList());
            return DesignateAllSpecialRooms(roomGrid, gridConnectionCount);
        }

        private (Dictionary<Room, HashSet<Vector2Int>>, HashSet<Vector2Int>) CreateAllFloors(Dictionary<Vector2Int, Room> roomGrid)
        {
            Dictionary<Room, HashSet<Vector2Int>> roomFloorData = new Dictionary<Room, HashSet<Vector2Int>>();
            HashSet<Vector2Int> totalFloorPositions = new HashSet<Vector2Int>();
            IEnumerator floorCoroutine = CreateAllRoomFloorsCoroutine(roomGrid, roomFloorData, totalFloorPositions, 1);
            while (floorCoroutine.MoveNext()) { }
            return (roomFloorData, totalFloorPositions);
        }

        private List<PlacedObstacleData> PlaceObstaclesInRooms(Dictionary<Room, HashSet<Vector2Int>> roomFloorData, HashSet<Vector2Int> portalPositions, HashSet<Vector2Int> totalFloorPositions)
        {
            return PlaceObstacles(roomFloorData, portalPositions, totalFloorPositions);
        }

        private void SpawnMonstersInRooms(Dictionary<Vector2Int, Room> roomGrid, List<PlacedObstacleData> obstacles, Dictionary<Room, HashSet<Vector2Int>> roomFloorData)
        {
            SpawnMonsters(roomGrid, obstacles, roomFloorData);
        }

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

            if (_worldmapController != null && _minimapGenerator != null)
            {
                _worldmapController.DrawMap(allRooms, roomConnections, dungeonData);
                _minimapGenerator.InitializeMap(tilemapVisualizer, obstaclesToPlace, portalPositions, roomFloorData, allRooms, startRoom);
            }
        }

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

        private void SpawnMonsters(Dictionary<Vector2Int, Room> roomGrid, List<PlacedObstacleData> obstacles, Dictionary<Room, HashSet<Vector2Int>> roomFloorData)
        {
            if (dungeonData.dungeonMonsters == null || dungeonData.dungeonMonsters.Count == 0) return;

            HashSet<Vector2Int> obstaclePositions = new HashSet<Vector2Int>(obstacles.Select(o => Vector2Int.RoundToInt(o.worldPosition)));

            foreach (Room room in roomGrid.Values.Where(r => r.type == RoomType.Normal))
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

        private (HashSet<Vector2Int>, List<Tuple<Room, Room>>) ConnectRoomsAndCreatePortals(Dictionary<Vector2Int, Room> roomGrid, Dictionary<Room, HashSet<Vector2Int>> roomFloorData, HashSet<Vector2Int> allFloorPositions)
        {
            HashSet<Vector2Int> portalPositions = new HashSet<Vector2Int>();
            List<Tuple<Room, Room>> connections = new List<Tuple<Room, Room>>();
            HashSet<Tuple<Vector2Int, Vector2Int>> connectionsMade = new HashSet<Tuple<Vector2Int, Vector2Int>>();
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

                        List<Vector2Int> portalTilesA = FindBestPortalTiles(roomFloorData[roomA], direction, allFloorPositions);
                        List<Vector2Int> portalTilesB = FindBestPortalTiles(roomFloorData[roomB], -direction, allFloorPositions);

                        if (portalTilesA.Count > 0 && portalTilesB.Count > 0)
                        {
                            Vector3Int centerA = (Vector3Int)portalTilesA[portalTilesA.Count / 2];
                            Vector3Int centerB = (Vector3Int)portalTilesB[portalTilesB.Count / 2];
                            _dungeonManager?.RegisterPortalPair(centerA, centerB);
                            connections.Add(Tuple.Create(roomA, roomB));
                        }
                        else
                        {
                            Debug.LogWarning($"[던전 생성] 포탈 연결 실패: {roomA.gridPos}와 {roomB.gridPos} 사이의 적절한 포탈 위치를 찾지 못했습니다.");
                        }


                        portalPositions.UnionWith(portalTilesA);
                        portalPositions.UnionWith(portalTilesB);
                        connectionsMade.Add(connectionTuple);
                    }
                }
            }
            return (portalPositions, connections);
        }

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



        private List<Vector2Int> RetrySelectConnectedGridPositions(Vector2Int gridSize)
        {
            int requiredDeadEnds = 2 + dungeonData.numberOfShopRooms + dungeonData.numberOfItemRooms;
            List<Vector2Int> finalPositions = null;

        
            int maxAttempts = 500;
            for (int i = 0; i < maxAttempts; i++)
            {
            
                var candidatePositions = SelectConnectedGridPositions(gridSize);

 
                if (PruneConnectionsToCreateDeadEnds(candidatePositions, requiredDeadEnds))
                {
                    return candidatePositions;
                }
       
            }

         
            finalPositions = SelectConnectedGridPositions(gridSize);
            PruneConnectionsToCreateDeadEnds(finalPositions, requiredDeadEnds); // 최선의 노력
            return finalPositions;
        }

        private bool PruneConnectionsToCreateDeadEnds(List<Vector2Int> positions, int requiredDeadEnds)
        {
            int safetyBreak = 100;
            while (safetyBreak-- > 0)
            {
                Dictionary<Vector2Int, int> gridConnectionCount = CalculateGridConnections(positions);
                int deadEndCount = gridConnectionCount.Values.Count(c => c == 1);

                if (deadEndCount >= requiredDeadEnds)
                {

                    return true;
                }

                List<Vector2Int> candidates = positions.Where(p => gridConnectionCount[p] > 1).OrderBy(p => Random.value).ToList();

                bool connectionPruned = false;
                foreach (var candidate in candidates)
                {
                    List<Vector2Int> neighbors = new List<Vector2Int>();
                    foreach (var dir in WallGenerator.Direction2D.cardinalDirectionsList)
                    {
                        if (positions.Contains(candidate + dir))
                        {
                            neighbors.Add(candidate + dir);
                        }
                    }

                    if (neighbors.Count > 1)
                    {
                        Vector2Int neighborToDisconnect = neighbors[0];

                        List<Vector2Int> tempPositions = new List<Vector2Int>(positions);
                        tempPositions.Remove(candidate);

                        if (IsGraphConnected(tempPositions, neighborToDisconnect))
                        {
                            positions.Remove(candidate);
                            connectionPruned = true;
                            break;
                        }
                    }
                }

                if (!connectionPruned)
                {
                    Debug.LogWarning("가지치기 중단: 더 이상 안전하게 제거할 수 있는 방 연결이 없습니다.");
                    return false;
                }
            }

            return CalculateGridConnections(positions).Values.Count(c => c == 1) >= requiredDeadEnds;

        }

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
                foreach (var dir in WallGenerator.Direction2D.cardinalDirectionsList)
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

            Room bossRoom = deadEndRooms[0];
            bossRoom.type = RoomType.Boss;
            deadEndRooms.RemoveAt(0);

            Room startRoom = deadEndRooms.OrderByDescending(r => Vector2.Distance(r.center, bossRoom.center)).FirstOrDefault();
            if (startRoom != null)
            {
                startRoom.type = RoomType.Start;
                deadEndRooms.Remove(startRoom);
            }
            else
            {
                startRoom = roomGrid.Values.Where(r => r.type == RoomType.Normal).OrderByDescending(r => Vector2.Distance(r.center, bossRoom.center)).FirstOrDefault();
                if (startRoom != null) startRoom.type = RoomType.Start;
            }

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

        private bool IsGridPositionValid(Vector2Int pos, Vector2Int gridSize)
        {
            return pos.x >= 0 && pos.x < gridSize.x && pos.y >= 0 && pos.y < gridSize.y;
        }

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

        /* private List<Vector2Int> FindBestPortalTiles(HashSet<Vector2Int> roomFloor, Vector2Int portalDirection, HashSet<Vector2Int> allFloorPositions)
         {
             Vector2Int wallCheckDirection = (portalDirection.x == 0) ? Vector2Int.right : Vector2Int.up;

             HashSet<Vector2Int> edgeWallCandidates = new HashSet<Vector2Int>();
             foreach (Vector2Int floorPos in roomFloor)
             {
                 Vector2Int wallPos = floorPos + portalDirection;
                 if (!allFloorPositions.Contains(wallPos))
                 {
                     edgeWallCandidates.Add(wallPos);
                 }
             }

             HashSet<Vector2Int> checkedWalls = new HashSet<Vector2Int>();
             List<List<Vector2Int>> lines = new List<List<Vector2Int>>();
             foreach (Vector2Int wall in edgeWallCandidates)
             {
                 if (checkedWalls.Contains(wall)) continue;
                 List<Vector2Int> currentLine = new List<Vector2Int> { wall };
                 checkedWalls.Add(wall);
                 for (int i = 1; i < 20; i++) { Vector2Int next = wall + wallCheckDirection * i; if (edgeWallCandidates.Contains(next)) { currentLine.Add(next); checkedWalls.Add(next); } else break; }
                 for (int i = 1; i < 20; i++) { Vector2Int next = wall - wallCheckDirection * i; if (edgeWallCandidates.Contains(next)) { currentLine.Add(next); checkedWalls.Add(next); } else break; }
                 lines.Add(currentLine);
             }

             if (lines.Count == 0) return new List<Vector2Int>();

             List<Vector2Int> bestLine;
             List<List<Vector2Int>> linesLongerThan8 = lines.Where(l => l.Count >= 9).ToList();
             if (linesLongerThan8.Count > 0)
             {
                 bestLine = linesLongerThan8[Random.Range(0, linesLongerThan8.Count)];
             }
             else
             {
                 bestLine = lines.OrderByDescending(l => l.Count).First();
             }

             if (bestLine.Count < 3)
             {
                 Vector2Int fallbackCenter = bestLine.Count > 0 ? bestLine[0] : edgeWallCandidates.First();
                 return new List<Vector2Int> { fallbackCenter - wallCheckDirection, fallbackCenter, fallbackCenter + wallCheckDirection };
             }

             bestLine.Sort((a, b) => (a.x == b.x) ? a.y.CompareTo(b.y) : a.x.CompareTo(b.x));
             Vector2Int centerTile = bestLine[bestLine.Count / 2];

             return new List<Vector2Int>
       {
         centerTile - wallCheckDirection,
         centerTile,
         centerTile + wallCheckDirection
       };
         }*/

        private List<Vector2Int> FindBestPortalTiles(HashSet<Vector2Int> roomFloor, Vector2Int portalDirection, HashSet<Vector2Int> allFloorPositions)
        {
            Vector2Int wallCheckDirection = (portalDirection.x == 0) ? Vector2Int.right : Vector2Int.up;

            HashSet<Vector2Int> edgeWallCandidates = new HashSet<Vector2Int>();
            foreach (Vector2Int floorPos in roomFloor)
            {
                Vector2Int wallPos = floorPos + portalDirection;
                if (!allFloorPositions.Contains(wallPos))
                {
                    edgeWallCandidates.Add(wallPos);
                }
            }

            if (edgeWallCandidates.Count == 0)
            {
                return new List<Vector2Int>();
            }

            HashSet<Vector2Int> checkedWalls = new HashSet<Vector2Int>();
            List<List<Vector2Int>> lines = new List<List<Vector2Int>>();
            foreach (Vector2Int wall in edgeWallCandidates)
            {
                if (checkedWalls.Contains(wall)) continue;
                List<Vector2Int> currentLine = new List<Vector2Int> { wall };
                checkedWalls.Add(wall);
                for (int i = 1; i < 20; i++) { Vector2Int next = wall + wallCheckDirection * i; if (edgeWallCandidates.Contains(next)) { currentLine.Add(next); checkedWalls.Add(next); } else break; }
                for (int i = 1; i < 20; i++) { Vector2Int next = wall - wallCheckDirection * i; if (edgeWallCandidates.Contains(next)) { currentLine.Add(next); checkedWalls.Add(next); } else break; }
                lines.Add(currentLine);
            }

            // 여기가 핵심: '선'을 못 찾았더라도 '벽 후보'가 있으면 그걸로 포탈을 강제로 생성
            if (lines.Count == 0 || lines.All(l => l.Count == 0))
            {
                Vector2Int fallbackCenter = edgeWallCandidates.First();
                return new List<Vector2Int> { fallbackCenter - wallCheckDirection, fallbackCenter, fallbackCenter + wallCheckDirection };
            }

            List<Vector2Int> bestLine = lines.OrderByDescending(l => l.Count).First();

            if (bestLine.Count < 3)
            {
                Vector2Int fallbackCenter = bestLine.Count > 0 ? bestLine[0] : edgeWallCandidates.First();
                return new List<Vector2Int> { fallbackCenter - wallCheckDirection, fallbackCenter, fallbackCenter + wallCheckDirection };
            }

            bestLine.Sort((a, b) => (a.x == b.x) ? a.y.CompareTo(b.y) : a.x.CompareTo(b.x));
            Vector2Int centerTile = bestLine[bestLine.Count / 2];

            return new List<Vector2Int>
    {
        centerTile - wallCheckDirection,
        centerTile,
        centerTile + wallCheckDirection
    };
        }

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
                else
                {
                    roomFloor = CreateCompoundRoom(room, offset);
                }

                roomFloorData.Add(room, roomFloor);
                totalFloor.UnionWith(roomFloor);
                yield return null;
            }
        }

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

        private void RemoveThinWalls(
    HashSet<Vector2Int> allFloorPositions,
    HashSet<Vector2Int> allWallPositions,
    TilemapVisualizer visualizer,
    Dictionary<Room, HashSet<Vector2Int>> roomFloorData)
        {
            Dictionary<Vector2Int, Room> tileToRoomMap = new Dictionary<Vector2Int, Room>();
            foreach (var entry in roomFloorData)
            {
                foreach (var tilePos in entry.Value)
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
                foreach (var dir in WallGenerator.Direction2D.cardinalDirectionsList)
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

                foreach (var entry in wallsToConvertByRoom)
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