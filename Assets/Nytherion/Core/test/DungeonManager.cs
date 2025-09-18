using Nytherion.Core.Managers;
using Nytherion.Data.ScriptableObjects.Dungeon;
using Nytherion.GamePlay.Characters.Enemy;
using Nytherion.UI.Map;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;
using VContainer;

namespace Nytherion.GamePlay.Dungeon
{
    /// <summary>
    /// 생성된 던전의 전반적인 상태와 로직을 관리하는 중앙 관리자 클래스입니다.
    /// </summary>
    public class DungeonManager : MonoBehaviour
    {
        #region Public 프로퍼티

        public List<RoomFirstDungeonGenerator.Room> AllDungeonRooms { get; private set; }
        public List<EnemyBase> AllActiveEnemies => AllDungeonRooms?.SelectMany(room => room.enemies).ToList() ?? new List<EnemyBase>();
        public GameObject playerObject;

        #endregion

        #region Private 변수

        private readonly Dictionary<Vector3Int, Vector3Int> portalLinks = new Dictionary<Vector3Int, Vector3Int>();
        private Dictionary<Vector2Int, RoomFirstDungeonGenerator.Room> roomLookup;
        private Dictionary<RoomFirstDungeonGenerator.Room, HashSet<Vector2Int>> roomFloorData;

        [Header("컴포넌트 참조")]
        [SerializeField] public RoomFirstDungeonGenerator roomFirstDungeonGenerator;
        [SerializeField] public TilemapVisualizer tilemapVisualizer;

        private Tilemap portalTilemap;
        private TilemapCollider2D portalCollider;

        private RoomFirstDungeonGenerator.Room currentPlayerRoom = null;
        public RoomFirstDungeonGenerator.Room CurrentPlayerRoom => currentPlayerRoom;
        private RoomFirstDungeonGenerator.Room previousPlayerRoom = null;
        private bool hasBossSpawned = false;

        private EventManager _eventManager;
        public ObjectPoolManager _objectPoolManager;
        private InputManager _inputManager;
        private WorldmapController _worldmapController;
        private MinimapTileGenerator _minimapGenerator;
        public StageManager _stageManager;

        private Tilemap floorTilemap;
        private Tilemap wallTilemap;
        private Tilemap portalTilemapInstance;

        #endregion

        #region 의존성 주입

        [Inject]
        public void Construct(
           EventManager eventManager,
           ObjectPoolManager objectPoolManager,
           Characters.Player.PlayerController playerController,
           InputManager inputManager
           /*WorldmapController worldmapController*/) // 월드맵 토글 기능을 위해 주입받습니다.
        {
            _eventManager = eventManager;
            _objectPoolManager = objectPoolManager;
            playerObject = playerController.gameObject;
            _inputManager = inputManager;
            //_worldmapController = worldmapController;

            FindTilemapReferences();
        }

        private void FindTilemapReferences()
        {
            GameObject floorTilemapObj = GameObject.Find("FloorTilemap");
            GameObject wallTilemapObj = GameObject.Find("WallTilemap");
            GameObject portalTilemapObj = GameObject.Find("PortalTilemap");

            if (floorTilemapObj != null) this.floorTilemap = floorTilemapObj.GetComponent<Tilemap>();
            if (wallTilemapObj != null) this.wallTilemap = wallTilemapObj.GetComponent<Tilemap>();
            if (portalTilemapObj != null)
            {
                this.portalTilemapInstance = portalTilemapObj.GetComponent<Tilemap>();
                this.portalTilemap = this.portalTilemapInstance;
            }
        }

        public void SetStageManager(StageManager stageManager)
        {
            this._stageManager = stageManager;
            if (tilemapVisualizer != null)
            {
                tilemapVisualizer.InitializeTilemaps(floorTilemap, wallTilemap, portalTilemapInstance);
            }
        }

        public void SetControllers(WorldmapController worldmapController, MinimapTileGenerator minimapGenerator)
        {
            _worldmapController = worldmapController;
            _minimapGenerator = minimapGenerator;
        }

        #endregion

        #region Unity 생명주기 메서드

        private void Awake()
        {
            if (portalTilemap != null)
            {
                portalCollider = portalTilemap.GetComponent<TilemapCollider2D>();
            }
            roomFirstDungeonGenerator=GetComponentInChildren<RoomFirstDungeonGenerator>();
          //  roomFirstDungeonGenerator.SetControllers(_worldmapController, _minimapGenerator);
        }

        private void Start()
        {
            if (_eventManager != null)
            {
                _eventManager.RegisterEnemyDeathListener(HandleEnemyDeath);
            }
            RoomFirstDungeonGenerator.OnDungeonGenerated += SpawnPlayerAtStart;

            if (_inputManager != null)
            {
                _inputManager.onMap += ToggleWorldMap;
            }
        }

        private void OnDestroy()
        {
            if (_eventManager != null)
            {
                _eventManager.UnregisterEnemyDeathListener(HandleEnemyDeath);
            }
            RoomFirstDungeonGenerator.OnDungeonGenerated -= SpawnPlayerAtStart;

            if (_inputManager != null)
            {
                _inputManager.onMap -= ToggleWorldMap;
            }
        }

        private void Update()
        {
            if (playerObject == null || AllDungeonRooms == null || AllDungeonRooms.Count == 0) return;
            RoomFirstDungeonGenerator.Room detectedRoom = FindCurrentPlayerRoom();
            if (detectedRoom != null && detectedRoom != currentPlayerRoom)
            {
                previousPlayerRoom = currentPlayerRoom;
                currentPlayerRoom = detectedRoom;
                OnPlayerEnteredRoom(currentPlayerRoom, previousPlayerRoom);
            }
        }

        public void StartDungeonGeneration()
        {
            if (_stageManager != null && _stageManager.CurrentStage != null)
            {
               
                roomFirstDungeonGenerator.dungeonData = _stageManager.CurrentStage.dungeonData;
                roomFirstDungeonGenerator.DungeonStart();
            }
            else
            {
                Debug.LogError("StageManager 또는 현재 스테이지 데이터가 없습니다! 던전을 생성할 수 없습니다.");
            }
        }

        public void RegenerateDungeon()
        {
            Debug.Log("[DungeonManager] 던전 재생성을 시작합니다...");
            StartDungeonGeneration();
        }

        #endregion

        // ... (이 아래로 기존 DungeonManager.cs의 나머지 코드를 그대로 붙여넣으면 돼) ...
        // (SetAllRooms, SetRoomLookup, RegisterPortalPair, FindCurrentPlayerRoom 등 모든 메서드 포함)

        #region Public 데이터 설정 메서드

        public void SetAllRooms(List<RoomFirstDungeonGenerator.Room> allRooms)
        {
            AllDungeonRooms = allRooms;
        }

        public void SetRoomLookup(Dictionary<Vector2Int, RoomFirstDungeonGenerator.Room> newLookup)
        {
            roomLookup = newLookup;
        }

        public void SetRoomFloorData(Dictionary<RoomFirstDungeonGenerator.Room, HashSet<Vector2Int>> newFloorData)
        {
            roomFloorData = newFloorData;
        }

        public void ClearDungeonData()
        {
            portalLinks.Clear();
            AllDungeonRooms?.Clear();
            roomLookup?.Clear();
            roomFloorData?.Clear();
            hasBossSpawned = false;
        }

        #endregion

        #region 포탈 관련 메서드

        public void RegisterPortalPair(Vector3Int portalA, Vector3Int portalB)
        {
            Debug.Log($"<color=cyan>[포탈 등록]</color> A: {portalA} <-> B: {portalB}");

            portalLinks[portalA] = portalB;
            portalLinks[portalB] = portalA;
        }

        public bool TryGetDestination(Vector3Int currentPortalPos, out Vector3Int destinationPos)
        {
            Debug.Log($"[포탈 확인] 플레이어가 밟은 타일: {currentPortalPos}. 등록된 포탈 목록과 비교합니다.");
            if (portalLinks.Count == 0)
            {
                Debug.LogError("[포탈 확인] DungeonManager에 등록된 포탈이 하나도 없습니다!");
            }
            foreach (var portal in portalLinks)
            {
                Debug.Log($"- 등록된 포탈: {portal.Key} -> {portal.Value}");
            }
            return portalLinks.TryGetValue(currentPortalPos, out destinationPos);

        }

        private void LockPortals()
        {
            if (portalCollider != null)
            {
                portalCollider.isTrigger = false;
            }
        }

        private void UnlockPortals()
        {
            if (portalCollider != null)
            {
                portalCollider.isTrigger = true;
            }
        }

        #endregion

        #region 방 탐색 및 상태 관련 메서드

        public RoomFirstDungeonGenerator.Room FindCurrentPlayerRoom()
        {
            if (playerObject == null) return null;
            Vector2Int playerTilePos = Vector2Int.FloorToInt(playerObject.transform.position);
            return FindRoomAt(playerTilePos);
        }

        public RoomFirstDungeonGenerator.Room FindRoomAt(Vector2Int position)
        {
            if (roomLookup != null && roomLookup.TryGetValue(position, out RoomFirstDungeonGenerator.Room room))
            {
                return room;
            }
            return null;
        }

        public bool IsRoomCleared(RoomFirstDungeonGenerator.Room room)
        {
            if (room == null) return true;
            return room.enemies.Count == 0 || room.enemies.All(monster => monster.isDead);
        }

        #endregion

        #region 이벤트 핸들러 및 콜백

        private void ToggleWorldMap()
        {
            _worldmapController?.Toggle();
        }

        private void SpawnPlayerAtStart(RoomFirstDungeonGenerator.Room startRoom)
        {
            if (playerObject != null && startRoom != null)
            {
                playerObject.transform.position = startRoom.center;
            }
        }

        private void HandleEnemyDeath(EnemyBase deadEnemy)
        {
            RoomFirstDungeonGenerator.Room room = deadEnemy.homeRoom;
            if (room != null && IsRoomCleared(room))
            {
                if (room.type == RoomFirstDungeonGenerator.RoomType.Boss)
                {
                    _stageManager?.SpawnBossPortal(deadEnemy.transform.position);
                }
                else
                {
                    UnlockPortals();
                }
            }
        }

        private void OnPlayerEnteredRoom(RoomFirstDungeonGenerator.Room newRoom, RoomFirstDungeonGenerator.Room oldRoom)
        {
            oldRoom?.DeactivateEnemies();
            newRoom?.ActivateEnemies();

            if (newRoom.type == RoomFirstDungeonGenerator.RoomType.Boss && !hasBossSpawned)
            {
                SpawnBoss(newRoom);
            }

            if (!IsRoomCleared(newRoom))
            {
                LockPortals();
            }
            else
            {
                UnlockPortals();
            }
        }

        private void SpawnBoss(RoomFirstDungeonGenerator.Room bossRoom)
        {
            DungeonData dungeonData = roomFirstDungeonGenerator.GetComponent<RoomFirstDungeonGenerator>().dungeonData;
            if (dungeonData.bossMonsterData == null)
            {
                Debug.LogWarning("DungeonData에 보스 몬스터 데이터가 할당되지 않았습니다.");
                return;
            }

            if (!bossRoom.bossSpawnPoint.HasValue)
            {
                Debug.LogError("보스 방에 스폰 위치가 지정되지 않았습니다! RoomFirstDungeonGenerator를 확인하세요.");
                return;
            }

            Vector3 spawnPosition = bossRoom.bossSpawnPoint.Value;
            GameObject bossObj = _objectPoolManager.SpawnFromPool(
                dungeonData.bossMonsterData.enemyName,
                spawnPosition,
                Quaternion.identity);

            if (bossObj != null && bossObj.TryGetComponent<EnemyBase>(out var bossEnemy))
            {
                bossEnemy.Initialize(dungeonData.bossMonsterData);
                bossEnemy.homeRoom = bossRoom;
                bossRoom.enemies.Add(bossEnemy);
                hasBossSpawned = true;

                Debug.Log($"<color=red><b>보스 등장!</b></color> '{dungeonData.bossMonsterData.enemyName}' 스폰 완료!");
            }
            else
            {
                Debug.LogError($"오브젝트 풀에서 '{dungeonData.bossMonsterData.enemyName}' 태그를 가진 오브젝트를 스폰하는데 실패했습니다. ObjectPoolManager 설정을 확인하세요.");
            }
        }
        #endregion
    }
}