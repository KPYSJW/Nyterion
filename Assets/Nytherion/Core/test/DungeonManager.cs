using Nytherion.Core.Managers;
using Nytherion.GamePlay.Characters.Enemy;
using Nytherion.UI.Controllers;
using Nytherion.UI.Map;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;
using Zenject;

namespace Nytherion.GamePlay.Dungeon
{
    /// <summary>
    /// 생성된 던전의 전반적인 상태와 로직을 관리하는 중앙 관리자 클래스입니다.
    /// </summary>
    public class DungeonManager : MonoBehaviour
    {
        #region Public 프로퍼티

        /// <summary>
        /// 현재 던전에 생성된 모든 방의 리스트입니다.
        /// </summary>
        public List<RoomFirstDungeonGenerator.Room> AllDungeonRooms { get; private set; }

        /// <summary>
        /// 던전 내에 존재하는 모든 활성화된 적 리스트를 반환합니다.
        /// MinimapTileGenerator가 이 정보를 사용하여 적 아이콘을 표시합니다.
        /// </summary>
        public List<EnemyBase> AllActiveEnemies
        {
            get
            {
                // LINQ의 SelectMany를 사용하여 모든 방의 적 리스트를 하나의 리스트로 효율적으로 펼칩니다.
                return AllDungeonRooms?.SelectMany(room => room.enemies).ToList() ?? new List<EnemyBase>();
            }
        }

        /// <summary>
        /// 플레이어 게임 오브젝트 참조입니다.
        /// </summary>
        public GameObject playerObject;

        #endregion

        #region Private 변수

        // 포탈 쌍의 연결 정보를 저장합니다. (Key: 시작 포탈 위치, Value: 도착 포탈 위치)
        private readonly Dictionary<Vector3Int, Vector3Int> portalLinks = new Dictionary<Vector3Int, Vector3Int>();
        // 특정 타일 좌표(Vector2Int)가 어느 방에 속하는지 빠르게 찾기 위한 룩업 테이블입니다.
        private Dictionary<Vector2Int, RoomFirstDungeonGenerator.Room> roomLookup;
        // 각 방(Room)이 어떤 바닥 타일들로 구성되어 있는지 저장하는 데이터입니다.
        private Dictionary<RoomFirstDungeonGenerator.Room, HashSet<Vector2Int>> roomFloorData;

        // 컴포넌트 및 매니저 참조
        [Header("컴포넌트 참조")]
        [SerializeField] private RoomFirstDungeonGenerator roomFirstDungeonGenerator;
        [SerializeField] public TilemapVisualizer tilemapVisualizer;

        private Tilemap portalTilemap;
        private TilemapCollider2D portalCollider;

        // 플레이어의 현재 방과 이전 방을 추적하기 위한 변수
        private RoomFirstDungeonGenerator.Room currentPlayerRoom = null;
        private RoomFirstDungeonGenerator.Room previousPlayerRoom = null;

        // 의존성 주입으로 받아올 매니저들
        private EventManager _eventManager;
        public ObjectPoolManager _objectPoolManager;
        private InputManager _inputManager;
        private WorldmapController _worldmapController;

        #endregion

        #region 의존성 주입

        /// <summary>
        /// Zenject를 통해 필요한 의존성들을 주입받는 생성자 메서드입니다.
        /// </summary>
        [Inject]
        public void Construct(
           EventManager eventManager,
           ObjectPoolManager objectPoolManager,
           Characters.Player.PlayerController playerController,
           InputManager inputManager,
           WorldmapController worldmapController,
           [Inject(Id = "FloorTilemap")] Tilemap floorTilemap,
           [Inject(Id = "WallTilemap")] Tilemap wallTilemap,
           [Inject(Id = "PortalTilemap")] Tilemap portalTilemapInstance)
        {
            _eventManager = eventManager;
            _objectPoolManager = objectPoolManager;
            playerObject = playerController.gameObject;
            _inputManager = inputManager;
            _worldmapController = worldmapController;
            this.portalTilemap = portalTilemapInstance;

            // TilemapVisualizer에 필요한 타일맵들을 초기화합니다.
            if (tilemapVisualizer != null)
            {
                tilemapVisualizer.InitializeTilemaps(floorTilemap, wallTilemap, portalTilemapInstance);
            }
        }

        #endregion

        #region Unity 생명주기 메서드

        private void Awake()
        {
            // 포탈 타일맵에서 콜라이더 컴포넌트를 가져옵니다.
            if (portalTilemap != null)
            {
                portalCollider = portalTilemap.GetComponent<TilemapCollider2D>();
            }
        }

        private void Start()
        {
            // 필요한 이벤트 리스너들을 등록합니다.
            _eventManager.RegisterEnemyDeathListener(HandleEnemyDeath);
            RoomFirstDungeonGenerator.OnDungeonGenerated += SpawnPlayerAtStart;

            if (_inputManager != null)
            {
                _inputManager.onMap += ToggleWorldMap;
            }

            // 던전 생성을 시작합니다.
            roomFirstDungeonGenerator.DungeonStart();
        }

        private void OnDestroy()
        {
            // 게임 오브젝트가 파괴될 때 등록했던 이벤트 리스너들을 모두 해제합니다.
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
            // 필수 데이터가 없으면 업데이트 로직을 실행하지 않습니다.
            if (playerObject == null || AllDungeonRooms == null || AllDungeonRooms.Count == 0) return;

            // 플레이어의 현재 위치를 기반으로 어느 방에 있는지 확인합니다.
            RoomFirstDungeonGenerator.Room detectedRoom = FindCurrentPlayerRoom();

            // 플레이어가 새로운 방으로 이동했다면
            if (detectedRoom != null && detectedRoom != currentPlayerRoom)
            {
                previousPlayerRoom = currentPlayerRoom;
                currentPlayerRoom = detectedRoom;
                // 방 이동에 따른 로직을 처리합니다.
                OnPlayerEnteredRoom(currentPlayerRoom, previousPlayerRoom);
            }
        }

        #endregion

        #region Public 데이터 설정 메서드

        /// <summary>
        /// 던전 생성기가 생성한 모든 방 리스트를 설정합니다.
        /// </summary>
        public void SetAllRooms(List<RoomFirstDungeonGenerator.Room> allRooms)
        {
            AllDungeonRooms = allRooms;
        }

        /// <summary>
        /// 타일 좌표로 방을 빠르게 찾기 위한 룩업 테이블을 설정합니다.
        /// </summary>
        public void SetRoomLookup(Dictionary<Vector2Int, RoomFirstDungeonGenerator.Room> newLookup)
        {
            roomLookup = newLookup;
        }

        /// <summary>
        /// 각 방의 바닥 타일 구성 정보를 설정합니다.
        /// </summary>
        public void SetRoomFloorData(Dictionary<RoomFirstDungeonGenerator.Room, HashSet<Vector2Int>> newFloorData)
        {
            roomFloorData = newFloorData;
        }

        /// <summary>
        /// 모든 던전 관련 데이터를 초기화합니다.
        /// </summary>
        public void ClearDungeonData()
        {
            portalLinks.Clear();
            AllDungeonRooms?.Clear();
            roomLookup?.Clear();
            roomFloorData?.Clear();
        }

        #endregion

        #region 포탈 관련 메서드

        /// <summary>
        /// 두 포탈을 서로 연결하여 등록합니다.
        /// </summary>
        public void RegisterPortalPair(Vector3Int portalA, Vector3Int portalB)
        {
            portalLinks[portalA] = portalB;
            portalLinks[portalB] = portalA;
        }

        /// <summary>
        /// 현재 포탈 위치를 기반으로 연결된 목적지 포탈 위치를 찾습니다.
        /// </summary>
        /// <returns>목적지를 찾았으면 true, 아니면 false를 반환합니다.</returns>
        public bool TryGetDestination(Vector3Int currentPortalPos, out Vector3Int destinationPos)
        {
            return portalLinks.TryGetValue(currentPortalPos, out destinationPos);
        }

        /// <summary>
        /// 포탈을 잠가 플레이어가 통과할 수 없게 만듭니다. (전투 중)
        /// </summary>
        private void LockPortals()
        {
            if (portalCollider != null)
            {
                portalCollider.isTrigger = false;
            }
        }

        /// <summary>
        /// 포탈을 열어 플레이어가 통과할 수 있게 만듭니다. (전투 종료)
        /// </summary>
        private void UnlockPortals()
        {
            if (portalCollider != null)
            {
                portalCollider.isTrigger = true;
            }
        }

        #endregion

        #region 방 탐색 및 상태 관련 메서드

        /// <summary>
        /// 플레이어의 현재 월드 좌표를 기반으로 어느 방에 있는지 찾습니다.
        /// </summary>
        public RoomFirstDungeonGenerator.Room FindCurrentPlayerRoom()
        {
            if (playerObject == null) return null;
            // 플레이어의 월드 좌표를 타일 좌표로 변환합니다.
            Vector2Int playerTilePos = Vector2Int.FloorToInt(playerObject.transform.position);
            return FindRoomAt(playerTilePos);
        }

        /// <summary>
        /// 특정 타일 좌표에 어떤 방이 있는지 룩업 테이블을 통해 찾습니다.
        /// </summary>
        public RoomFirstDungeonGenerator.Room FindRoomAt(Vector2Int position)
        {
            if (roomLookup != null && roomLookup.TryGetValue(position, out RoomFirstDungeonGenerator.Room room))
            {
                return room;
            }
            return null; // 해당 위치에 방이 없으면 null 반환
        }

        /// <summary>
        /// 해당 방의 모든 몬스터가 처치되었는지 확인합니다.
        /// </summary>
        public bool IsRoomCleared(RoomFirstDungeonGenerator.Room room)
        {
            if (room == null) return true; // 방 정보가 없으면 클리어된 것으로 간주
            // 방에 몬스터가 없거나, 모든 몬스터의 isDead 플래그가 true이면 클리어된 것입니다.
            return room.enemies.Count == 0 || room.enemies.All(monster => monster.isDead);
        }

        #endregion

        #region 이벤트 핸들러 및 콜백

        /// <summary>
        /// 월드맵 UI를 켜고 끄는 토글 메서드입니다.
        /// </summary>
        private void ToggleWorldMap()
        {
            _worldmapController?.Toggle();
        }

        /// <summary>
        /// 던전 생성이 완료되었을 때 호출되는 콜백 메서드입니다.
        /// </summary>
        private void SpawnPlayerAtStart(RoomFirstDungeonGenerator.Room startRoom)
        {
            if (playerObject != null && startRoom != null)
            {
                // 플레이어를 시작 방의 중앙으로 이동시킵니다.
                playerObject.transform.position = startRoom.center;
            }
        }

        /// <summary>
        /// 적이 죽었을 때 호출되는 이벤트 핸들러입니다.
        /// </summary>
        private void HandleEnemyDeath(EnemyBase deadEnemy)
        {
            RoomFirstDungeonGenerator.Room room = deadEnemy.homeRoom;
            // 죽은 적이 속한 방이 있고, 그 방의 모든 적이 죽었다면
            if (room != null && IsRoomCleared(room))
            {
                // 포탈의 잠금을 해제합니다.
                UnlockPortals();
            }
        }

        /// <summary>
        /// 플레이어가 새로운 방에 진입했을 때 호출되는 메서드입니다.
        /// </summary>
        private void OnPlayerEnteredRoom(RoomFirstDungeonGenerator.Room newRoom, RoomFirstDungeonGenerator.Room oldRoom)
        {
            // 이전에 있던 방의 몬스터는 비활성화하여 리소스를 절약합니다.
            oldRoom?.DeactivateEnemies();
            // 새로 들어온 방의 몬스터는 활성화합니다.
            newRoom?.ActivateEnemies();

            // 새로 들어온 방이 아직 클리어되지 않았다면 포탈을 잠급니다.
            if (!IsRoomCleared(newRoom))
            {
                LockPortals();
            }
            else // 이미 클리어된 방이라면 포탈을 엽니다.
            {
                UnlockPortals();
            }
        }

        #endregion
    }
}
