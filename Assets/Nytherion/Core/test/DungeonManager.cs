// ScriptsArchive/DungeonManager.cs

using Nytherion.Core.Managers;
using Nytherion.Data.ScriptableObjects.Enemy;
using Nytherion.GamePlay.Characters.Enemy;
using Nytherion.UI.Controllers;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;
using Zenject; // Zenject 네임스페이스 추가

namespace Nytherion.GamePlay.Dungeon
{
    public class DungeonManager : MonoBehaviour
    {
        // public static DungeonManager Instance { get; private set; } // Instance 제거

        public List<RoomFirstDungeonGenerator.Room> AllDungeonRooms { get; private set; }
        private Dictionary<Vector3Int, Vector3Int> portalLinks = new Dictionary<Vector3Int, Vector3Int>();
        public List<GameObject> activeEnemies = new List<GameObject>();
        private Dictionary<RoomFirstDungeonGenerator.Room, List<EnemyBase>> roomEnemies = new Dictionary<RoomFirstDungeonGenerator.Room, List<EnemyBase>>();

        [Header("Component References")]
        [SerializeField] private RoomFirstDungeonGenerator roomFirstDungeonGenerator;
     
        [SerializeField] private Tilemap portalTilemap;
        [SerializeField] public TilemapVisualizer tilemapVisualizer;

        private Rigidbody2D portalRigidbody;
        private TilemapCollider2D portalCollider;
        public GameObject playerObject;

        private RoomFirstDungeonGenerator.Room currentPlayerRoom = null;
        private RoomFirstDungeonGenerator.Room previousPlayerRoom = null;

        // --- 의존성 주입 ---
        private InputManager _inputManager;
        private EventManager _eventManager;
        public ObjectPoolManager _objectPoolManager;
        public WorldmapController _worldmapController;
        [Inject]
        public void Construct(
           InputManager inputManager,
           EventManager eventManager,
           ObjectPoolManager objectPoolManager,
           Characters.Player.PlayerController playerController,
           [Inject(Id = "FloorTilemap")] Tilemap floorTilemap,
           [Inject(Id = "WallTilemap")] Tilemap wallTilemap,
           [Inject(Id = "PortalTilemap")] Tilemap portalTilemapInstance,
           WorldmapController worldmapController)
        {
            _inputManager = inputManager;
            _eventManager = eventManager;
            _objectPoolManager = objectPoolManager;
            playerObject = playerController.gameObject;
            _worldmapController= worldmapController;
            this.portalTilemap = portalTilemapInstance;
            // --- 이제 자식에게 물려주자! ---
            if (tilemapVisualizer != null)
            {
                // TilemapVisualizer에게 타일맵들을 직접 전달하는 초기화 함수 호출
                tilemapVisualizer.InitializeTilemaps(floorTilemap, wallTilemap, portalTilemapInstance);
            }
            else
            {
                Debug.LogError("DungeonManager에 TilemapVisualizer가 연결되지 않았습니다!", this.gameObject);
            }
        }

        private void Awake()
        {
           
            if (portalTilemap != null)
            {
                portalRigidbody = portalTilemap.GetComponent<Rigidbody2D>();
                portalCollider = portalTilemap.GetComponent<TilemapCollider2D>();
            }
        }

        public void Start()
        {
            // 주입받은 매니저들의 이벤트에 리스너 등록
            _eventManager.RegisterEnemyDeathListener(HandleEnemyDeath);
            _inputManager.onMap += ToggleWorldMap; 
            RoomFirstDungeonGenerator.OnDungeonGenerated += SpawnPlayerAtStart;

            // 던전 생성 시작
            roomFirstDungeonGenerator.DungeonStart();
        }

        private void OnDestroy()
        {
            // 주입받은 매니저들이 null이 아닌지 확인하고 리스너 해제
            if (_eventManager != null)
            {
                _eventManager.UnregisterEnemyDeathListener(HandleEnemyDeath);
            }
            if (_inputManager != null)
            {
                _inputManager.onMap += ToggleWorldMap;
            }
            if (roomFirstDungeonGenerator != null)
            {
                RoomFirstDungeonGenerator.OnDungeonGenerated -= SpawnPlayerAtStart;
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

        public void RegisterPortalPair(Vector3Int portalA, Vector3Int portalB)
        {
            portalLinks[portalA] = portalB;
            portalLinks[portalB] = portalA;
        }

        public bool TryGetDestination(Vector3Int currentPortalPos, out Vector3Int destinationPos)
        {
            return portalLinks.TryGetValue(currentPortalPos, out destinationPos);
        }

        public void ClearDungeonData()
        {
            portalLinks.Clear();
            AllDungeonRooms?.Clear();
            roomEnemies.Clear();
            activeEnemies.Clear();
        }

        private void SpawnPlayerAtStart(RoomFirstDungeonGenerator.Room startRoom)
        {
            if (playerObject != null)
            {
                playerObject.transform.position = new Vector3(startRoom.center.x, startRoom.center.y, 0);
            }
            else
            {
                Debug.LogError("Player object not found! Ensure the player is instantiated and injected correctly.");
            }
        }

        public void SetAllRooms(List<RoomFirstDungeonGenerator.Room> allRooms)
        {
            AllDungeonRooms = allRooms;
        }

        void ToggleWorldMap()
        {
            Debug.Log("월드맵1");
            if (_worldmapController != null)
            {
                Debug.Log("월드맵2");
                _worldmapController.Toggle();
            }
        }

        private void SetMonstersActive(RoomFirstDungeonGenerator.Room room, bool isActive)
        {
            if (roomEnemies.TryGetValue(room, out List<EnemyBase> enemies))
            {
                foreach (var enemy in enemies)
                {
                    if (enemy != null&& !enemy.isDead) enemy.gameObject.SetActive(isActive);
                }
            }
        }

        public void SpawnMonster(EnemyData monsterData, Vector3Int position)
        {
            GameObject enemyObj = _objectPoolManager.SpawnFromPool(monsterData.enemyName, (Vector3)position + new Vector3(0.5f, 0.5f, 0), Quaternion.identity);
            if (enemyObj != null)
            {
                EnemyBase enemy = enemyObj.GetComponent<EnemyBase>();
                if (enemy != null)
                {
                    enemy.Initialize(monsterData);
                    RoomFirstDungeonGenerator.Room room = FindRoomAt(position);
                    if (room != null)
                    {
                        enemy.homeRoom = room;

                        if (!roomEnemies.ContainsKey(room))
                        {
                            roomEnemies[room] = new List<EnemyBase>();
                        }
                        roomEnemies[room].Add(enemy);
                        activeEnemies.Add(enemyObj);

                        // 방에 들어왔을 때 몬스터가 활성화/비활성화 되도록 초기 상태는 비활성화로 설정
                        enemy.gameObject.SetActive(false);
                    }
                }
            }
        }

        private void HandleEnemyDeath(EnemyBase deadEnemy)
        {
            RoomFirstDungeonGenerator.Room room = deadEnemy.homeRoom;
            if (room != null && roomEnemies.ContainsKey(room))
            {
                bool allMonstersAreDead = roomEnemies[room].All(monster => monster.isDead);

                if (allMonstersAreDead)
                {
                    UnlockPortals();
                }
            }
        }

        public RoomFirstDungeonGenerator.Room FindRoomAt(Vector3Int position)
        {
            if (AllDungeonRooms == null) return null;
            foreach (var room in AllDungeonRooms)
            {
                if (new BoundsInt(new Vector3Int(room.Bounds.min.x, room.Bounds.min.y, 0), new Vector3Int(room.Bounds.size.x, room.Bounds.size.y, 1)).Contains(position))
                {
                    return room;
                }
            }
            return null;
        }

        private void OnPlayerEnteredRoom(RoomFirstDungeonGenerator.Room newRoom, RoomFirstDungeonGenerator.Room oldRoom)
        {
            if (oldRoom != null)
            {
                SetMonstersActive(oldRoom, false);
            }
            SetMonstersActive(newRoom, true);

            if (!IsRoomCleared(newRoom))
            {
                LockPortals();
            }
            else
            {
                UnlockPortals();
            }
        }

        public RoomFirstDungeonGenerator.Room FindCurrentPlayerRoom()
        {
            if (playerObject == null || AllDungeonRooms == null) return null;
            return FindRoomAt(Vector3Int.FloorToInt(playerObject.transform.position));
        }

        public bool IsRoomCleared(RoomFirstDungeonGenerator.Room room)
        {
            if (room == null) return true;
            if (!roomEnemies.ContainsKey(room) || roomEnemies[room].Count == 0) return true;
            return roomEnemies[room].All(monster => monster.isDead);
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
    }
}