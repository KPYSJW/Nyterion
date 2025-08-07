using Nytherion.Core.Managers;

using Nytherion.Data.ScriptableObjects.Enemy;

using Nytherion.UI.Controllers;

using System;

using System.Collections;

using System.Collections.Generic;

using System.Linq;

using UnityEngine;

using UnityEngine.Tilemaps;

using Random = UnityEngine.Random;

using Zenject;

using Nytherion.GamePlay.Characters.Enemy;

using Nytherion.Data.ScriptableObjects.Dungeon;

using Nytherion.UI.Map;



namespace Nytherion.GamePlay.Dungeon

{

    /// <summary>

    /// "Room First" 알고리즘을 사용하여 절차적 던전을 생성하는 클래스입니다.

    /// 방을 먼저 생성하고 배치한 후, 방들을 연결하는 방식으로 던전을 만듭니다.

    /// </summary>

    public class RoomFirstDungeonGenerator : AbstractDungeonGenertor

    {

        #region 내부 구조체, 열거형, 클래스



        /// <summary>

        /// 던전에 배치된 장애물의 데이터를 저장하는 구조체입니다.

        /// </summary>

        public struct PlacedObstacleData

        {

            public GameObject prefab;

            public Vector2 worldPosition;

        }



        /// <summary>

        /// 던전 내 방의 종류를 정의합니다.

        /// </summary>

        public enum RoomType { Normal, Start, Boss, Shop, Item }



        /// <summary>

        /// 던전을 구성하는 개별 방의 데이터를 나타내는 클래스입니다.

        /// </summary>

        public class Room

        {

            public Vector2Int gridPos; // 방의 가상 그리드 좌표

            public Vector2Int size;    // 방의 크기 (타일 단위)

            public Vector2 center;     // 방의 중심 월드 좌표

            public List<EnemyBase> enemies = new List<EnemyBase>(); // 방에 속한 적 리스트

            public RoomType type = RoomType.Normal; // 방의 종류



            // 방의 경계를 나타내는 BoundsInt. 계산된 프로퍼티입니다.

            public BoundsInt Bounds => new BoundsInt(Vector3Int.RoundToInt(center - (Vector2)size / 2), (Vector3Int)size);



            public Room(Vector2Int gridPos, Vector2Int size)

            {

                this.gridPos = gridPos;

                this.size = size;

                this.enemies = new List<EnemyBase>();

            }



            /// <summary>

            /// 이 방에 속한 모든 적들을 활성화합니다.

            /// </summary>

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



            /// <summary>

            /// 이 방에 속한 모든 적들을 비활성화합니다.

            /// </summary>

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

        private DungeonData dungeonData;



        /// <summary>

        /// 던전 생성이 완료되었을 때 호출되는 이벤트입니다. 시작 방 정보를 전달합니다.

        /// </summary>

        public static event Action<Room> OnDungeonGenerated;



        // --- 의존성 주입 ---

        private DungeonManager _dungeonManager;

        private WorldmapController _worldmapController;

        private MinimapTileGenerator _minimapGenerator;



        #endregion



        #region 의존성 주입 및 초기화



        /// <summary>

        /// Zenject를 통해 필요한 의존성들을 주입받습니다.

        /// </summary>

        [Inject]

        public void Construct(DungeonManager dungeonManager, WorldmapController worldmapController, MinimapTileGenerator minimapGenerator)

        {

            _dungeonManager = dungeonManager;

            _worldmapController = worldmapController;

            _minimapGenerator = minimapGenerator;

        }



        /// <summary>

        /// 외부에서 던전 생성을 시작하기 위한 메서드입니다.

        /// </summary>

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



        /// <summary>

        /// 던전 생성의 전체 과정을 단계별로 실행하는 메인 코루틴입니다.

        /// </summary>

        protected override IEnumerator RunProceduralGeneration()

        {

            if (dungeonData == null)

            {

                Debug.LogError("DungeonData가 할당되지 않았습니다! 던전 생성을 중단합니다.");

                yield break;

            }

            // 생성 시작 전, 이전 데이터를 모두 초기화합니다.

            _dungeonManager?.ClearDungeonData();

            tilemapVisualizer.Clear();



            // --- 1단계: 방 위치 선정 ---

            // 가상 그리드 상에 방이 위치할 좌표들을 선택합니다.

            List<Vector2Int> selectedGridPositions = SelectGridPositions();

            if (selectedGridPositions.Count == 0) yield break; // 위치 선정 실패 시 중단

            yield return null; // 다음 단계로 넘어가기 전 한 프레임 대기



            // --- 2단계: 방 데이터 생성 및 배치 ---

            // 선택된 그리드 좌표에 실제 방 데이터를 생성하고, 초기 위치를 설정한 후 겹침 문제를 해결합니다.

            Dictionary<Vector2Int, Room> roomGrid = CreateAndPlaceRooms(selectedGridPositions);

            yield return StartCoroutine(ResolveOverlapsCoroutine(roomGrid.Values.ToList()));



            // --- 3단계: 특수 방 지정 ---

            // 생성된 방들 중에서 시작 방, 보스 방 등을 전략적으로 지정합니다.

            Room startRoom = DesignateSpecialRooms(roomGrid);

            yield return null;



            // --- 4단계: 바닥, 포탈 생성 ---

            // 모든 방의 바닥 타일을 생성하고, 방들을 포탈로 연결합니다.

            (Dictionary<Room, HashSet<Vector2Int>> roomFloorData, HashSet<Vector2Int> totalFloorPositions) = CreateAllFloors(roomGrid);

            (HashSet<Vector2Int> portalPositions, List<Tuple<Room, Room>> roomConnections) = ConnectRoomsAndCreatePortals(roomGrid, roomFloorData, totalFloorPositions);

            yield return null;



            // --- 5단계: 장애물 및 몬스터 배치 ---

            // 생성된 방 내부에 장애물과 몬스터를 배치합니다.

            List<PlacedObstacleData> obstaclesToPlace = PlaceObstaclesInRooms(roomFloorData, portalPositions, totalFloorPositions);

            SpawnMonstersInRooms(roomGrid, obstaclesToPlace, roomFloorData);

            yield return null;



            // --- 6단계: 타일맵 시각화 ---

            // 생성된 모든 데이터를 기반으로 타일맵과 오브젝트를 실제로 그립니다.

            VisualizeDungeon(totalFloorPositions, roomFloorData, portalPositions, roomConnections, obstaclesToPlace, roomGrid.Values.ToList());



            // --- 7단계: 데이터 후처리 및 이벤트 호출 ---

            // 생성된 데이터를 DungeonManager에 최종 등록하고, 생성 완료 이벤트를 호출하여 다른 시스템에 알립니다.

            FinalizeDungeonData(roomGrid, roomFloorData);

            OnDungeonGenerated?.Invoke(startRoom);

        }



        #endregion



        #region --- 던전 생성 단계별 래퍼(Wrapper) 메서드 ---



        /// <summary>

        /// 1단계: 방 위치 선정을 위한 래퍼 메서드입니다.

        /// </summary>

        private List<Vector2Int> SelectGridPositions()

        {

            int side = Mathf.CeilToInt(Mathf.Sqrt(dungeonData.desiredNumberOfRooms));

            Vector2Int gridSize = new Vector2Int(side * 2, side * 2);

            return RetrySelectConnectedGridPositions(gridSize);

        }



        /// <summary>

        /// 2단계: 방 데이터 생성 및 배치를 위한 래퍼 메서드입니다.

        /// </summary>

        private Dictionary<Vector2Int, Room> CreateAndPlaceRooms(List<Vector2Int> selectedGridPositions)

        {

            Dictionary<Vector2Int, Room> roomGrid = CreateRoomData(selectedGridPositions);

            float roomSpacing = Mathf.Max(dungeonData.maxRoomSize.x, dungeonData.maxRoomSize.y) * dungeonData.roomSpacingMultiplier;

            PlaceAndAlignRooms(roomGrid, roomSpacing);

            return roomGrid;

        }



        /// <summary>

        /// 3단계: 특수 방 지정을 위한 래퍼 메서드입니다.

        /// </summary>

        private Room DesignateSpecialRooms(Dictionary<Vector2Int, Room> roomGrid)

        {

            Dictionary<Vector2Int, int> gridConnectionCount = CalculateGridConnections(roomGrid.Keys.ToList());

            return DesignateAllSpecialRooms(roomGrid, gridConnectionCount);

        }



        /// <summary>

        /// 4단계: 모든 바닥 생성을 위한 래퍼 메서드입니다.

        /// </summary>

        private (Dictionary<Room, HashSet<Vector2Int>>, HashSet<Vector2Int>) CreateAllFloors(Dictionary<Vector2Int, Room> roomGrid)

        {

            Dictionary<Room, HashSet<Vector2Int>> roomFloorData = new Dictionary<Room, HashSet<Vector2Int>>();

            HashSet<Vector2Int> totalFloorPositions = new HashSet<Vector2Int>();

            IEnumerator floorCoroutine = CreateAllRoomFloorsCoroutine(roomGrid, roomFloorData, totalFloorPositions, 1);

            while (floorCoroutine.MoveNext()) { } // 코루틴을 동기적으로 실행하여 결과를 즉시 받습니다.

            return (roomFloorData, totalFloorPositions);

        }



        /// <summary>

        /// 5-1단계: 장애물 배치를 위한 래퍼 메서드입니다.

        /// </summary>

        private List<PlacedObstacleData> PlaceObstaclesInRooms(Dictionary<Room, HashSet<Vector2Int>> roomFloorData, HashSet<Vector2Int> portalPositions, HashSet<Vector2Int> totalFloorPositions)

        {

            return PlaceObstacles(roomFloorData, portalPositions, totalFloorPositions);

        }



        /// <summary>

        /// 5-2단계: 몬스터 스폰을 위한 래퍼 메서드입니다.

        /// </summary>

        private void SpawnMonstersInRooms(Dictionary<Vector2Int, Room> roomGrid, List<PlacedObstacleData> obstacles, Dictionary<Room, HashSet<Vector2Int>> roomFloorData)

        {

            SpawnMonsters(roomGrid, obstacles, roomFloorData);

        }



        /// <summary>

        /// 6단계: 던전 시각화를 위한 래퍼 메서드입니다.

        /// </summary>

        private void VisualizeDungeon(HashSet<Vector2Int> totalFloorPositions, Dictionary<Room, HashSet<Vector2Int>> roomFloorData, HashSet<Vector2Int> portalPositions, List<Tuple<Room, Room>> roomConnections, List<PlacedObstacleData> obstaclesToPlace, List<Room> allRooms)
        {
            // 특수 방을 제외한 일반 바닥 타일만 먼저 그립니다.
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

            // 특수 방 바닥과 포탈, 벽을 그립니다.
            tilemapVisualizer.PaintSpecialRoomFloors(specialRooms, dungeonData, roomFloorData);
            tilemapVisualizer.PaintPortals(portalPositions);
            HashSet<Vector2Int> allWallPositions = WallGenerator.FindWalls(totalFloorPositions, dungeonData.wallThickness);

            // --- [수정된 로직 호출!] ---
            // 생성된 벽들 중에서 얇은 벽을 찾아 바닥으로 변환하고, 즉시 그립니다.
            RemoveThinWalls(totalFloorPositions, allWallPositions, tilemapVisualizer);
            // --------------------------

            tilemapVisualizer.PaintWallsWithRuleTile(allWallPositions);

            // 장애물 오브젝트를 생성합니다.
            tilemapVisualizer.InstantiateObstacles(obstaclesToPlace);

            // 월드맵과 미니맵을 그립니다.
            if (_worldmapController != null && _minimapGenerator != null)
            {
                _worldmapController.DrawMap(allRooms, roomConnections, dungeonData);
                _minimapGenerator.InitializeMap(tilemapVisualizer, obstaclesToPlace, portalPositions, roomFloorData, allRooms);
            }
        }



        /// <summary>

        /// 7단계: 데이터 후처리를 위한 래퍼 메서드입니다.

        /// </summary>

        private void FinalizeDungeonData(Dictionary<Vector2Int, Room> roomGrid, Dictionary<Room, HashSet<Vector2Int>> roomFloorData)

        {

            if (_dungeonManager != null)

            {

                _dungeonManager.SetAllRooms(new List<Room>(roomGrid.Values));

                _dungeonManager.SetRoomFloorData(roomFloorData);



                // 타일 위치로 방을 빠르게 찾기 위한 룩업 테이블을 생성하여 DungeonManager에 전달합니다.

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



        /// <summary>

        /// 각 방에 몬스터를 스폰합니다.

        /// </summary>

        private void SpawnMonsters(Dictionary<Vector2Int, Room> roomGrid, List<PlacedObstacleData> obstacles, Dictionary<Room, HashSet<Vector2Int>> roomFloorData)

        {

            if (dungeonData.dungeonMonsters == null || dungeonData.dungeonMonsters.Count == 0) return;



            // 장애물이 배치된 위치를 빠르게 조회하기 위해 HashSet으로 변환합니다.

            HashSet<Vector2Int> obstaclePositions = new HashSet<Vector2Int>(obstacles.Select(o => Vector2Int.RoundToInt(o.worldPosition)));



            // '일반' 타입의 방에만 몬스터를 스폰합니다.

            foreach (Room room in roomGrid.Values.Where(r => r.type == RoomType.Normal))

            {

                if (!roomFloorData.TryGetValue(room, out HashSet<Vector2Int> floorTiles)) continue;



                // 장애물이 없는 바닥 타일만 스폰 후보 위치로 선정합니다.

                List<Vector2Int> candidatePositions = floorTiles.Where(pos => !obstaclePositions.Contains(pos)).ToList();

                if (candidatePositions.Count == 0) continue;



                // 방 크기에 비례하여 스폰할 몬스터 수를 결정합니다.

                float roomRatio = (float)(room.size.x * room.size.y) / (dungeonData.maxRoomSize.x * dungeonData.maxRoomSize.y);

                int monsterCount = Mathf.RoundToInt(Mathf.Lerp(1, 5, roomRatio));



                for (int i = 0; i < monsterCount; i++)

                {

                    if (candidatePositions.Count == 0) break;



                    // 스폰할 몬스터 종류와 위치를 무작위로 선택합니다.

                    EnemyData monsterToSpawn = dungeonData.dungeonMonsters[Random.Range(0, dungeonData.dungeonMonsters.Count)];

                    int randomIndex = Random.Range(0, candidatePositions.Count);

                    Vector2Int spawnPosition = candidatePositions[randomIndex];

                    candidatePositions.RemoveAt(randomIndex); // 중복 스폰 방지



                    if (_dungeonManager?._objectPoolManager != null)

                    {

                        // 오브젝트 풀을 사용하여 몬스터를 스폰합니다.

                        GameObject enemyObj = _dungeonManager._objectPoolManager.SpawnFromPool(monsterToSpawn.enemyName, (Vector3Int)spawnPosition + new Vector3(0.5f, 0.5f, 0), Quaternion.identity);

                        if (enemyObj != null)

                        {

                            EnemyBase enemy = enemyObj.GetComponent<EnemyBase>();

                            if (enemy != null)

                            {

                                enemy.Initialize(monsterToSpawn);

                                enemy.homeRoom = room; // 몬스터가 속한 방을 지정해줍니다.

                                room.enemies.Add(enemy);

                                enemyObj.SetActive(false); // 처음에는 비활성화 상태로 둡니다.

                            }

                        }

                    }

                }

            }

        }



        /// <summary>

        /// 그리드 상에서 인접한 방들을 찾아 포탈로 연결합니다.

        /// </summary>

        private (HashSet<Vector2Int>, List<Tuple<Room, Room>>) ConnectRoomsAndCreatePortals(Dictionary<Vector2Int, Room> roomGrid, Dictionary<Room, HashSet<Vector2Int>> roomFloorData, HashSet<Vector2Int> allFloorPositions)

        {

            HashSet<Vector2Int> portalPositions = new HashSet<Vector2Int>();

            List<Tuple<Room, Room>> connections = new List<Tuple<Room, Room>>();

            // 중복 연결을 방지하기 위한 HashSet

            HashSet<Tuple<Vector2Int, Vector2Int>> connectionsMade = new HashSet<Tuple<Vector2Int, Vector2Int>>();



            foreach (Room roomA in roomGrid.Values)

            {

                // 각 방의 상하좌우 인접 그리드를 확인합니다.

                foreach (Vector2Int direction in WallGenerator.Direction2D.cardinalDirectionsList)

                {

                    Vector2Int neighborGridPos = roomA.gridPos + direction;

                    if (roomGrid.TryGetValue(neighborGridPos, out Room roomB))

                    {

                        // 연결 정보를 정규화하여 중복 체크 (A->B 와 B->A를 동일하게 취급)

                        Tuple<Vector2Int, Vector2Int> connectionTuple = (roomA.gridPos.x < roomB.gridPos.x || (roomA.gridPos.x == roomB.gridPos.x && roomA.gridPos.y < roomB.gridPos.y))

             ? Tuple.Create(roomA.gridPos, roomB.gridPos) : Tuple.Create(roomB.gridPos, roomA.gridPos);



                        if (connectionsMade.Contains(connectionTuple)) continue;



                        // 각 방에서 연결 방향으로 가장 적합한 포탈 위치를 찾습니다.

                        List<Vector2Int> portalTilesA = FindBestPortalTiles(roomFloorData[roomA], direction, allFloorPositions);

                        List<Vector2Int> portalTilesB = FindBestPortalTiles(roomFloorData[roomB], -direction, allFloorPositions);



                        if (portalTilesA.Count > 0 && portalTilesB.Count > 0)

                        {

                            Vector3Int centerA = (Vector3Int)portalTilesA[portalTilesA.Count / 2];

                            Vector3Int centerB = (Vector3Int)portalTilesB[portalTilesB.Count / 2];



                            // DungeonManager에 포탈 쌍을 등록합니다.

                            _dungeonManager?.RegisterPortalPair(centerA, centerB);

                            connections.Add(Tuple.Create(roomA, roomB));

                        }



                        portalPositions.UnionWith(portalTilesA);

                        portalPositions.UnionWith(portalTilesB);

                        connectionsMade.Add(connectionTuple);

                    }

                }

            }

            return (portalPositions, connections);

        }



        /// <summary>

        /// 각 방에 설정값에 따라 장애물을 배치합니다.

        /// </summary>

        private List<PlacedObstacleData> PlaceObstacles(Dictionary<Room, HashSet<Vector2Int>> roomFloorData, HashSet<Vector2Int> portalPositions, HashSet<Vector2Int> allFloorTiles)

        {

            List<PlacedObstacleData> obstaclesToPlace = new List<PlacedObstacleData>();

            HashSet<Vector2Int> allWallPositions = WallGenerator.FindWalls(allFloorTiles, dungeonData.wallThickness);



            // --- [수정된 부분] ---

            // 포탈 및 포탈 바로 앞 접근 지점 주변에는 장애물이 생성되지 않도록 제외 구역을 설정합니다.

            HashSet<Vector2Int> portalExclusionZone = new HashSet<Vector2Int>();

            int portalExclusionRadius = 2; // 포탈 주변 장애물 생성 금지 반경 (2칸)



            foreach (Vector2Int portalPos in portalPositions)

            {

                // 1. 포탈 타일 자체를 중심으로 넓은 반경을 제외 구역에 추가합니다.

                for (int x = -portalExclusionRadius; x <= portalExclusionRadius; x++)

                {

                    for (int y = -portalExclusionRadius; y <= portalExclusionRadius; y++)

                    {

                        portalExclusionZone.Add(portalPos + new Vector2Int(x, y));

                    }

                }



                // 2. 포탈로 접근하는 '앞' 타일을 찾아 그 주변도 넓게 제외 구역으로 설정합니다.

                foreach (Vector2Int direction in WallGenerator.Direction2D.cardinalDirectionsList)

                {

                    Vector2Int accessPoint = portalPos + direction;

                    // 이웃 타일이 바닥 타일이라면, 그곳이 바로 포탈 접근 지점입니다.

                    if (allFloorTiles.Contains(accessPoint))

                    {

                        // 3. 접근 지점(accessPoint)을 중심으로 넓은 반경을 추가로 확보합니다.

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

                // 일반 방에만 장애물을 배치합니다.

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



                    // 장애물을 놓을 수 있는 유효한 위치 목록을 찾습니다.

                    List<Vector2Int> validPlacementSpots = new List<Vector2Int>();

                    foreach (Vector2Int potentialCenter in candidatePositions)

                    {

                        bool isSafe = true;

                        for (int x = -marginX; x <= marginX; x++)

                        {

                            for (int y = -marginY; y <= marginY; y++)

                            {

                                Vector2Int checkPos = potentialCenter + new Vector2Int(x, y);

                                // 벽, 포탈 제외 구역, 또는 해당 방의 바닥이 아닌 곳에는 배치할 수 없습니다.

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



                    // 배치 가능한 곳이 있다면 무작위로 하나를 선택하여 배치합니다.

                    if (validPlacementSpots.Count > 0)

                    {

                        Vector2Int placementCenter = validPlacementSpots[Random.Range(0, validPlacementSpots.Count)];

                        obstaclesToPlace.Add(new PlacedObstacleData

                        {

                            prefab = selectedObstacleData.prefab,

                            worldPosition = (Vector2)placementCenter + new Vector2(0.5f, 0.5f) // 타일 중앙에 위치

                        });



                        // 배치된 장애물 주변을 다음 후보지에서 제외하여 장애물이 겹치지 않게 합니다.

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



        /// <summary>

        /// 필요한 만큼의 막다른 길(dead-end)이 나올 때까지 그리드 위치 선택을 재시도합니다.

        /// </summary>

        private List<Vector2Int> RetrySelectConnectedGridPositions(Vector2Int gridSize)

        {

            List<Vector2Int> selectedGridPositions;

            int generationAttempts = 0;

            // 보스, 시작, 상점, 아이템 방은 막다른 길에 배치하는 것이 이상적입니다.

            int requiredDeadEnds = 2 + dungeonData.numberOfShopRooms + dungeonData.numberOfItemRooms;



            while (true)

            {

                generationAttempts++;

                if (generationAttempts > dungeonData.maxGenerationAttempts)

                {

                    Debug.LogError($"던전 생성 실패: {dungeonData.maxGenerationAttempts}번 시도 후에도 유효한 그리드를 생성하지 못했습니다.");

                    return new List<Vector2Int>();

                }

                selectedGridPositions = SelectConnectedGridPositions(gridSize);

                if (selectedGridPositions.Count < dungeonData.desiredNumberOfRooms) continue;



                Dictionary<Vector2Int, int> gridConnectionCount = CalculateGridConnections(selectedGridPositions);

                int deadEndCount = gridConnectionCount.Values.Count(c => c == 1); // 연결이 하나뿐인 방 = 막다른 길



                if (deadEndCount >= requiredDeadEnds) break; // 필요한 만큼의 막다른 길이 확보되면 성공

            }

            return selectedGridPositions;

        }



        /// <summary>

        /// 생성된 방들 중에서 시작, 보스, 상점, 아이템 방을 지정합니다.

        /// </summary>

        private Room DesignateAllSpecialRooms(Dictionary<Vector2Int, Room> roomGrid, Dictionary<Vector2Int, int> gridConnectionCount)

        {

            // 막다른 길에 있는 방들을 특수 방 후보로 선정합니다.

            List<Room> deadEndRooms = roomGrid.Values

        .Where(room => gridConnectionCount.ContainsKey(room.gridPos) && gridConnectionCount[room.gridPos] == 1)

        .OrderBy(r => Random.value).ToList(); // 무작위로 섞음



            // 막다른 길이 없는 예외적인 경우를 처리합니다.

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



            // 보스 방 지정 (후보 중 하나)

            Room bossRoom = deadEndRooms[0];

            bossRoom.type = RoomType.Boss;

            deadEndRooms.RemoveAt(0);



            // 시작 방 지정 (보스 방에서 가장 먼 막다른 길)

            Room startRoom = deadEndRooms.OrderByDescending(r => Vector2.Distance(r.center, bossRoom.center)).FirstOrDefault();

            if (startRoom != null)

            {

                startRoom.type = RoomType.Start;

                deadEndRooms.Remove(startRoom);

            }

            else // 만약 막다른 길이 더이상 없다면, 일반 방 중에서 가장 먼 곳을 시작방으로

            {

                startRoom = roomGrid.Values.Where(r => r.type == RoomType.Normal).OrderByDescending(r => Vector2.Distance(r.center, bossRoom.center)).FirstOrDefault();

                if (startRoom != null) startRoom.type = RoomType.Start;

            }



            // 나머지 특수 방들을 남은 막다른 길에 지정

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



        /// <summary>

        /// 각 그리드 위치가 몇 개의 다른 그리드와 연결되어 있는지 계산합니다.

        /// </summary>

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



        /// <summary>

        /// 무작위 워커(Random Walker)와 유사한 방식으로 연결된 그리드 위치들을 선택합니다.

        /// </summary>

        private List<Vector2Int> SelectConnectedGridPositions(Vector2Int gridSize)

        {

            List<Vector2Int> selectedPositions = new List<Vector2Int>();

            if (dungeonData.desiredNumberOfRooms == 0) return selectedPositions;



            List<Vector2Int> frontier = new List<Vector2Int>();

            Vector2Int startPos = new Vector2Int(Random.Range(0, gridSize.x), Random.Range(0, gridSize.y));



            selectedPositions.Add(startPos);

            // 시작점의 이웃을 탐색 후보(frontier)에 추가

            foreach (Vector2Int dir in WallGenerator.Direction2D.cardinalDirectionsList)

            {

                Vector2Int neighbor = startPos + dir;

                if (IsGridPositionValid(neighbor, gridSize)) frontier.Add(neighbor);

            }



            // 원하는 방 개수만큼 선택될 때까지 반복

            while (selectedPositions.Count < dungeonData.desiredNumberOfRooms && frontier.Count > 0)

            {

                int frontierIndex = Random.Range(0, frontier.Count);

                Vector2Int nextPos = frontier[frontierIndex];

                frontier.RemoveAt(frontierIndex);



                if (selectedPositions.Contains(nextPos)) continue;



                selectedPositions.Add(nextPos);

                // 새로 추가된 위치의 이웃들을 다시 후보에 추가

                foreach (Vector2Int dir in WallGenerator.Direction2D.cardinalDirectionsList)

                {

                    Vector2Int neighbor = nextPos + dir;

                    if (IsGridPositionValid(neighbor, gridSize)) frontier.Add(neighbor);

                }

            }

            return selectedPositions;

        }



        /// <summary>

        /// 주어진 그리드 좌표가 유효한 범위 내에 있는지 확인합니다.

        /// </summary>

        private bool IsGridPositionValid(Vector2Int pos, Vector2Int gridSize)

        {

            return pos.x >= 0 && pos.x < gridSize.x && pos.y >= 0 && pos.y < gridSize.y;

        }



        /// <summary>

        /// 선택된 그리드 위치에 따라 초기 방 데이터(크기)를 생성합니다.

        /// </summary>

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



        /// <summary>

        /// 생성된 방들을 그리드 관계에 따라 정렬하고 초기 월드 좌표를 설정합니다.

        /// </summary>

        private void PlaceAndAlignRooms(Dictionary<Vector2Int, Room> roomGrid, float roomSpacing)

        {

            Queue<Room> queue = new Queue<Room>();

            HashSet<Vector2Int> visited = new HashSet<Vector2Int>();

            Room startRoom = roomGrid.Values.First();

            startRoom.center = Vector2.zero; // 첫 방은 원점에 배치

            queue.Enqueue(startRoom);

            visited.Add(startRoom.gridPos);



            // BFS(너비 우선 탐색)를 사용하여 모든 방을 순회하며 위치를 정렬

            while (queue.Count > 0)

            {

                Room parentRoom = queue.Dequeue();

                List<Vector2Int> shuffledDirections = WallGenerator.Direction2D.cardinalDirectionsList.OrderBy(d => Random.value).ToList();

                foreach (Vector2Int direction in shuffledDirections)

                {

                    Vector2Int neighborGridPos = parentRoom.gridPos + direction;

                    if (roomGrid.TryGetValue(neighborGridPos, out Room neighborRoom) && !visited.Contains(neighborGridPos))

                    {

                        // 부모 방과의 상대적인 위치를 계산하여 자식 방의 중심 좌표를 설정

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



        /// <summary>

        /// 방들이 서로 겹치지 않도록 위치를 미세 조정하는 코루틴입니다. (간단한 물리 시뮬레이션)

        /// </summary>

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



                        // 두 방이 겹쳤다면 서로 밀어냅니다.

                        if (Mathf.Abs(delta.x) < minDistanceX && Mathf.Abs(delta.y) < minDistanceY)

                        {

                            if (delta == Vector2.zero) delta = Random.insideUnitCircle; // 완전히 겹쳤을 경우 랜덤 방향으로

                            roomA.center += delta.normalized * 0.5f;

                            roomB.center -= delta.normalized * 0.5f;

                        }

                    }

                }

                yield return null;

            }

        }



        /// <summary>

        /// 방의 가장자리에서 포탈을 놓기에 가장 적합한 3칸짜리 벽 타일 목록을 찾습니다.

        /// </summary>

        private List<Vector2Int> FindBestPortalTiles(HashSet<Vector2Int> roomFloor, Vector2Int portalDirection, HashSet<Vector2Int> allFloorPositions)

        {

            // 포탈 방향에 수직인 방향을 찾습니다. (벽 라인을 탐색할 방향)

            Vector2Int wallCheckDirection = (portalDirection.x == 0) ? Vector2Int.right : Vector2Int.up;



            // 주어진 방향으로 방의 가장자리에 있는 벽 타일 후보들을 찾습니다.

            HashSet<Vector2Int> edgeWallCandidates = new HashSet<Vector2Int>();

            foreach (Vector2Int floorPos in roomFloor)

            {

                Vector2Int wallPos = floorPos + portalDirection;

                if (!allFloorPositions.Contains(wallPos)) // 다른 방의 바닥이 아니어야 함

                {

                    edgeWallCandidates.Add(wallPos);

                }

            }



            // 가장자리의 벽들을 연속된 라인으로 그룹화합니다.

            HashSet<Vector2Int> checkedWalls = new HashSet<Vector2Int>();

            List<List<Vector2Int>> lines = new List<List<Vector2Int>>();

            foreach (Vector2Int wall in edgeWallCandidates)

            {

                if (checkedWalls.Contains(wall)) continue;

                List<Vector2Int> currentLine = new List<Vector2Int> { wall };

                checkedWalls.Add(wall);

                // 양방향으로 연속된 벽을 찾아 라인에 추가

                for (int i = 1; i < 20; i++) { Vector2Int next = wall + wallCheckDirection * i; if (edgeWallCandidates.Contains(next)) { currentLine.Add(next); checkedWalls.Add(next); } else break; }

                for (int i = 1; i < 20; i++) { Vector2Int next = wall - wallCheckDirection * i; if (edgeWallCandidates.Contains(next)) { currentLine.Add(next); checkedWalls.Add(next); } else break; }

                lines.Add(currentLine);

            }



            if (lines.Count == 0) return new List<Vector2Int>();



            // 가장 긴 라인을 포탈 위치로 선정합니다. (단, 너무 긴 라인이 여러 개면 그중 무작위 선택)

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



            // 라인이 너무 짧으면(3칸 미만) 중앙 타일을 기준으로 강제로 3칸을 만듭니다.

            if (bestLine.Count < 3)

            {

                Vector2Int fallbackCenter = bestLine.Count > 0 ? bestLine[0] : edgeWallCandidates.First();

                return new List<Vector2Int> { fallbackCenter - wallCheckDirection, fallbackCenter, fallbackCenter + wallCheckDirection };

            }



            // 라인을 정렬하고 중앙에 있는 3개의 타일을 포탈 위치로 반환합니다.

            bestLine.Sort((a, b) => (a.x == b.x) ? a.y.CompareTo(b.y) : a.x.CompareTo(b.x));

            Vector2Int centerTile = bestLine[bestLine.Count / 2];



            return new List<Vector2Int>

      {

        centerTile - wallCheckDirection,

        centerTile,

        centerTile + wallCheckDirection

      };

        }



        /// <summary>

        /// 모든 방의 바닥 타일을 생성하는 코루틴입니다.

        /// </summary>

        private IEnumerator CreateAllRoomFloorsCoroutine(Dictionary<Vector2Int, Room> roomGrid, Dictionary<Room, HashSet<Vector2Int>> roomFloorData, HashSet<Vector2Int> totalFloor, int offset)

        {

            foreach (Room room in roomGrid.Values)

            {

                HashSet<Vector2Int> roomFloor;

                // 보스 방이고 프리팹이 지정되어 있으면 프리팹 기반으로 바닥 생성

                if (room.type == RoomType.Boss && dungeonData.bossRoomPrefab != null)

                {

                    roomFloor = CreateFloorFromPrefab(room, dungeonData.bossRoomPrefab);

                }

                else // 그 외의 방은 절차적으로 생성

                {

                    roomFloor = CreateCompoundRoom(room, offset);

                }

                roomFloorData.Add(room, roomFloor);

                totalFloor.UnionWith(roomFloor);

                yield return null; // 방 하나 생성 후 한 프레임 대기

            }

        }



        /// <summary>

        /// 타일맵 프리팹을 기반으로 방의 바닥 타일 위치를 생성합니다.

        /// </summary>

        private HashSet<Vector2Int> CreateFloorFromPrefab(Room room, Tilemap prefabTilemap)

        {

            HashSet<Vector2Int> floorPositions = new HashSet<Vector2Int>();

            foreach (Vector3Int tilePos in prefabTilemap.cellBounds.allPositionsWithin)

            {

                if (prefabTilemap.HasTile(tilePos))

                {

                    // 프리팹의 로컬 타일 위치를 방의 중심을 기준으로 월드 위치로 변환

                    Vector2Int worldPos = (Vector2Int)Vector3Int.RoundToInt(room.center) + (Vector2Int)tilePos;

                    floorPositions.Add(worldPos);

                }

            }

            return floorPositions;

        }

        /*

        /// <summary>

        /// 단일 사각형이 아닌, 두 개의 사각형을 겹쳐 더 복잡한 모양의 방을 생성합니다.

        /// </summary>

        private HashSet<Vector2Int> CreateCompoundRoom(Room room, int offset)

        {

            // 설정된 확률에 따라 단순 사각형 방을 생성할 수도 있습니다.

            if (Random.value > dungeonData.compoundRoomChance)

                return CreateRectangularFloor(room.Bounds, offset);



            HashSet<Vector2Int> floor = new HashSet<Vector2Int>();

            BoundsInt mainBounds = room.Bounds;



            int numberOfRectangles = Random.Range(3, 5); // 3 또는 4를 반환



            for (int i = 0; i < numberOfRectangles; i++)

            {

                // 메인 경계 내에서 무작위 크기와 위치의 사각형을 생성합니다.

                int width = Random.Range(mainBounds.size.x / 2, mainBounds.size.x);

                int height = Random.Range(mainBounds.size.y / 2, mainBounds.size.y);

                Vector3Int position = new Vector3Int(

                    mainBounds.xMin + Random.Range(0, mainBounds.size.x - width),

                    mainBounds.yMin + Random.Range(0, mainBounds.size.y - height), 0);



                BoundsInt newRect = new BoundsInt(position, new Vector3Int(width, height, 1));



                // 생성된 사각형의 바닥 타일을 전체 바닥에 추가(UnionWith)합니다.

                floor.UnionWith(CreateRectangularFloor(newRect, offset));

            }



            return floor;

        }

        */



        /// <summary>

        /// 중심부에서 시작하여 외부로 가지를 뻗어 나가는 방식으로, 더 넓고 다양한 모양의 복합 방을 생성합니다.

        /// </summary>

        private HashSet<Vector2Int> CreateCompoundRoom(Room room, int offset)

        {

            // 설정된 확률에 따라 단순 사각형 방을 생성할 수도 있습니다.

            if (Random.value > dungeonData.compoundRoomChance)

                return CreateRectangularFloor(room.Bounds, offset);



            HashSet<Vector2Int> floor = new HashSet<Vector2Int>();

            BoundsInt mainBounds = room.Bounds;



            // 1. 방의 중심에 기본이 될 사각형을 생성합니다.

            floor.UnionWith(CreateRectangularFloor(mainBounds, offset));



            // 2. 기본 방에서 뻗어 나갈 가지(추가 사각형)의 개수를 정합니다.

            int numberOfBranches = Random.Range(2, 4); // 2 또는 3개의 가지를 추가



            for (int i = 0; i < numberOfBranches; i++)

            {

                // 3. 현재까지 만들어진 방 모양의 모든 가장자리 타일을 찾습니다.

                List<Vector2Int> edgeTiles = FindEdgeTiles(floor);

                if (edgeTiles.Count == 0) break; // 가장자리가 없으면 중단



                // 4. 가장자리 타일 중 하나를 무작위로 골라 가지가 시작될 '앵커'로 삼습니다.

                Vector2Int anchorTile = edgeTiles[Random.Range(0, edgeTiles.Count)];



                // 5. 앵커에 덧붙일 작은 사각형의 크기를 정합니다.

                int width = Random.Range(dungeonData.minRoomSize.x / 2, dungeonData.maxRoomSize.x / 2);

                int height = Random.Range(dungeonData.minRoomSize.y / 2, dungeonData.maxRoomSize.y / 2);

                width = Mathf.Max(3, width);   // 최소 크기 보장

                height = Mathf.Max(3, height); // 최소 크기 보장



                // 6. 앵커 타일의 어느 방향으로 가지를 뻗을지 결정합니다. (바깥쪽 방향)

                Vector2Int placementDirection = Vector2Int.zero;

                foreach (Vector2Int dir in WallGenerator.Direction2D.cardinalDirectionsList)

                {

                    if (!floor.Contains(anchorTile + dir))

                    {

                        placementDirection = dir;

                        break;

                    }

                }

                if (placementDirection == Vector2Int.zero) continue; // 바깥쪽 방향을 못 찾으면 건너뛰기



                // 7. 새로운 사각형의 중심 위치를 계산하여 앵커에 덧붙입니다.

                Vector3Int newRectCenter = (Vector3Int)anchorTile + new Vector3Int(

          placementDirection.x * (width / 2),

          placementDirection.y * (height / 2),

          0);



                BoundsInt newRect = new BoundsInt(newRectCenter - new Vector3Int(width / 2, height / 2, 0), new Vector3Int(width, height, 1));



                // 8. 새로 만들어진 가지를 전체 방 모양에 합칩니다.

                floor.UnionWith(CreateRectangularFloor(newRect, 0)); // 가지는 offset 없이 꽉 채워서 생성

            }



            return floor;

        }



        /// <summary>

        /// [새로 추가된 헬퍼 메서드]

        /// 주어진 바닥 타일들 중에서 가장자리에 해당하는 타일들의 리스트를 찾아 반환합니다.

        /// 가장자리 타일이란, 상하좌우 중 하나라도 바닥이 아닌 타일과 인접한 타일을 의미합니다.

        /// </summary>

        /// <param name="floor">현재까지 생성된 방의 바닥 타일 데이터</param>

        /// <returns>가장자리 타일들의 리스트</returns>

        private List<Vector2Int> FindEdgeTiles(HashSet<Vector2Int> floor)

        {

            List<Vector2Int> edgeTiles = new List<Vector2Int>();

            foreach (Vector2Int tile in floor)

            {

                // 상하좌우 4방향을 확인합니다.

                foreach (Vector2Int direction in WallGenerator.Direction2D.cardinalDirectionsList)

                {

                    // 이웃한 타일이 바닥이 아니라면, 현재 타일은 가장자리입니다.

                    if (!floor.Contains(tile + direction))

                    {

                        edgeTiles.Add(tile);

                        break; // 가장자리임을 확인했으니 다음 타일로 넘어갑니다.

                    }

                }

            }

            return edgeTiles;

        }



        /// <summary>

        /// 주어진 경계(BoundsInt) 내에 사각형 모양의 바닥 타일 위치를 생성합니다.

        /// </summary>

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
        /// <summary>
        /// [수정된 메서드]
        /// 두께가 2칸 이하인 얇은 벽을 감지하여 바닥으로 변환하고, 해당 위치에 바닥 타일을 즉시 그립니다.
        /// 플레이어의 이동을 방해할 수 있는 비좁은 벽 구조를 제거하여 맵의 품질을 개선합니다.
        /// </summary>
        /// <param name="allFloorPositions">전체 바닥 타일의 위치 데이터입니다. 이 Set은 변환된 벽 타일을 포함하도록 수정됩니다.</param>
        /// <param name="allWallPositions">전체 벽 타일의 위치 데이터입니다. 이 Set은 변환된 벽 타일이 제거되도록 수정됩니다.</param>
        /// <param name="visualizer">타일을 실제로 그리는 역할을 하는 시각화 컴포넌트입니다.</param>
        private void RemoveThinWalls(HashSet<Vector2Int> allFloorPositions, HashSet<Vector2Int> allWallPositions, TilemapVisualizer visualizer)
        {
            HashSet<Vector2Int> wallsToConvert = new HashSet<Vector2Int>();

            // 모든 벽 타일을 순회하며 검사합니다.
            foreach (Vector2Int wallPos in allWallPositions)
            {
                // 이 타일이 이미 다른 벽(2칸 두께)의 일부로 변환 대상에 포함되었다면, 중복 검사를 피합니다.
                if (wallsToConvert.Contains(wallPos))
                {
                    continue;
                }

                // --- 1칸 두께의 벽 감지 ---
                // 수직 체크: 타일의 위쪽과 아래쪽이 모두 바닥인가? (세로로 얇은 벽)
                if (allFloorPositions.Contains(wallPos + Vector2Int.up) && allFloorPositions.Contains(wallPos + Vector2Int.down))
                {
                    wallsToConvert.Add(wallPos);
                    continue; // 얇은 벽으로 확정되었으므로 다음 타일 검사로 넘어갑니다.
                }

                // 수평 체크: 타일의 왼쪽과 오른쪽이 모두 바닥인가? (가로로 얇은 벽)
                if (allFloorPositions.Contains(wallPos + Vector2Int.left) && allFloorPositions.Contains(wallPos + Vector2Int.right))
                {
                    wallsToConvert.Add(wallPos);
                    continue;
                }

                // --- 2칸 두께의 벽 감지 ---
                // 수직 체크: 아래는 바닥, 위는 벽, 그 위는 바닥인가? [바닥] [현재타일] [벽] [바닥] (세로)
                if (allFloorPositions.Contains(wallPos + Vector2Int.down) &&
                    allWallPositions.Contains(wallPos + Vector2Int.up) &&
                    allFloorPositions.Contains(wallPos + Vector2Int.up * 2))
                {
                    wallsToConvert.Add(wallPos);
                    wallsToConvert.Add(wallPos + Vector2Int.up);
                    continue;
                }

                // 수평 체크: 왼쪽은 바닥, 오른쪽은 벽, 그 오른쪽은 바닥인가? [바닥][현재타일][벽][바닥] (가로)
                if (allFloorPositions.Contains(wallPos + Vector2Int.left) &&
                    allWallPositions.Contains(wallPos + Vector2Int.right) &&
                    allFloorPositions.Contains(wallPos + Vector2Int.right * 2))
                {
                    wallsToConvert.Add(wallPos);
                    wallsToConvert.Add(wallPos + Vector2Int.right);
                    continue;
                }
            }

            // 검사를 통해 찾아낸 얇은 벽들이 있다면,
            if (wallsToConvert.Count > 0)
            {
                // 벽 목록에서는 제거하고,
                allWallPositions.ExceptWith(wallsToConvert);
                // 바닥 목록에 추가합니다.
                allFloorPositions.UnionWith(wallsToConvert);

                // --- [가장 중요한 수정!] ---
                // 새로 생긴 바닥 타일을 즉시 그려주어 구멍이 생기지 않도록 합니다.
                visualizer.PaintFloorTiles(wallsToConvert);
            }
        }
        #endregion

    }

}