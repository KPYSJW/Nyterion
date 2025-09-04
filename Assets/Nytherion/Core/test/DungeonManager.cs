using Nytherion.Core.Managers;
using Nytherion.Data.ScriptableObjects.Enemy;
using Nytherion.GamePlay.Characters.Enemy;
using Nytherion.GamePlay.Dungeon;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;
using Zenject;

public class DungeonManager : MonoBehaviour
{
    public RoomFirstDungeonGenerator RoomFirstDungeonGenerator;
    public List<RoomFirstDungeonGenerator.Room> AllDungeonRooms { get; private set; }
    private Dictionary<Vector3Int, Vector3Int> portalLinks = new Dictionary<Vector3Int, Vector3Int>();
    public MinimapTileGenerator minimapGenerator;
    public GameObject playerObject;
    public List<GameObject> activeEnemies = new List<GameObject>();
    private Dictionary<RoomFirstDungeonGenerator.Room, List<EnemyBase>> roomEnemies = new Dictionary<RoomFirstDungeonGenerator.Room, List<EnemyBase>>();
    [SerializeField] private GameObject worldMapUI;

    [Header("Tilemap Components")] // [수정] 헤더 변경
    [SerializeField] private Tilemap portalTilemap; // [수정] Portal 타일맵 자체를 연결
    private Rigidbody2D portalRigidbody;
    private TilemapCollider2D portalCollider;

    private RoomFirstDungeonGenerator.Room currentPlayerRoom = null;
    private RoomFirstDungeonGenerator.Room previousPlayerRoom = null;

    private EventManager eventManager;
    public InputManager inputManager;

    [Inject]
    public void Construct(EventManager eventManager, InputManager inputManager)
    {
        this.eventManager = eventManager;
        this.inputManager = inputManager;
    }

    private void Awake()
    {
        if (worldMapUI != null) worldMapUI.SetActive(false);

        if (portalTilemap != null)
        {
            portalRigidbody = portalTilemap.GetComponent<Rigidbody2D>();
            portalCollider = portalTilemap.GetComponent<TilemapCollider2D>();
        }
    }

    public void Start()
    {

        StartCoroutine(RegisterEventListeners());
        RoomFirstDungeonGenerator.DungeonStart();
    }
    private IEnumerator RegisterEventListeners()
    {
        yield return new WaitUntil(() => eventManager != null);
        eventManager.RegisterEnemyDeathListener(HandleEnemyDeath);
        Debug.Log("DungeonManager: EnemyDeath 리스너 등록 성공!");

        if (inputManager != null)
        {
            inputManager.onMap += WorldMapUI;
        }

        // 던전 생성 이벤트 리스너 등록
        RoomFirstDungeonGenerator.OnDungeonGenerated += SpawnPlayerAtStart;
    }
    private void OnDestroy()
    {
        if (eventManager != null)
        {
            eventManager.UnregisterEnemyDeathListener(HandleEnemyDeath);
        }
        if (inputManager != null)
        {
            inputManager.onMap -= WorldMapUI;
        }
        RoomFirstDungeonGenerator.OnDungeonGenerated -= SpawnPlayerAtStart;
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
    }

    private void SpawnPlayerAtStart(RoomFirstDungeonGenerator.Room startRoom)
    {
        if (playerObject == null)
        {
            playerObject = GameObject.FindGameObjectWithTag("Player");
        }

        if (playerObject != null)
        {

            playerObject.transform.position = new Vector3(startRoom.center.x, startRoom.center.y, 0);
            Debug.Log($"÷̾ {startRoom.center} ̵!");
        }
        else
        {
            Debug.LogError("Player  ã  ! 'Player'  Ȯ.");
        }
    }
    public void SetAllRooms(List<RoomFirstDungeonGenerator.Room> allRooms)
    {
        AllDungeonRooms = allRooms;

    }

    void WorldMapUI()
    {
        if (worldMapUI != null)
        {
            worldMapUI.SetActive(!worldMapUI.activeSelf);

        }
    }
    private void SetMonstersActive(RoomFirstDungeonGenerator.Room room, bool isActive)
    {
        if (roomEnemies.TryGetValue(room, out List<EnemyBase> enemies))
        {
            foreach (var enemy in enemies)
            {
                if (enemy != null) enemy.gameObject.SetActive(isActive);
            }
        }
    }

    public void SpawnMonster(EnemyData monsterData, Vector3Int position)
    {
        GameObject enemyObj = ObjectPoolManager.Instance.SpawnFromPool(monsterData.enemyName, (Vector3)position + new Vector3(0.5f, 0.5f, 0), Quaternion.identity);
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
                }
            }
        }
    }

    private void HandleEnemyDeath(EnemyBase deadEnemy)
    {
        RoomFirstDungeonGenerator.Room room = deadEnemy.homeRoom;
        Debug.Log($"sdsdsdd.");
        if (room != null && roomEnemies.ContainsKey(room))
        {
            bool allMonstersAreDead = roomEnemies[room].All(monster => monster.isDead);

            if (allMonstersAreDead)
            {
                Debug.Log($"Room at {room.gridPos} is cleared! All portals are now unlocked.");
                UnlockPortals();
            }

            // 죽은 몬스터가 보스인지 확인 (보스전 로직은 그대로 유지)
            // if (deadEnemy == bossInstance)
            // {
            //     OnBossDefeated(deadEnemy);
            // }
        }
    }

    public RoomFirstDungeonGenerator.Room FindRoomAt(Vector3Int position)
    {
        foreach (var room in AllDungeonRooms)
        {
            if (new BoundsInt(new Vector3Int(room.Bounds.min.x, room.Bounds.min.y, 0), new Vector3Int(room.Bounds.size.x, room.Bounds.size.y, 1)).Contains(position))
            {
                return room;
            }
        }
        return null;
    }
    private void OnDungeonReady(RoomFirstDungeonGenerator.Room startRoom)
    {
        SpawnPlayerAtStart(startRoom);
        foreach (var roomEntry in roomEnemies)
        {
            SetMonstersActive(roomEntry.Key, false);
        }
        UnlockPortals();
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
        if (portalRigidbody != null)
        {
            portalRigidbody.bodyType = RigidbodyType2D.Static; // 단단한 벽으로 만듦
            portalCollider.isTrigger = false;
            Debug.Log("모든 포탈이 잠겼습니다.");
        }
    }

    // [추가] 모든 포탈을 여는 메서드
    private void UnlockPortals()
    {
        if (portalRigidbody != null)
        {
            portalRigidbody.bodyType = RigidbodyType2D.Kinematic; // 통과 가능하게 만듦
            portalCollider.isTrigger = true;
            Debug.Log("모든 포탈이 열렸습니다.");
        }
    }
}