using Nytherion.Core.Managers;
using Nytherion.Data.ScriptableObjects.Enemy;
using Nytherion.GamePlay.Characters.Enemy;
using Nytherion.GamePlay.Dungeon;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;

public class DungeonManager : MonoBehaviour
{
    public static DungeonManager Instance { get; private set; }
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



    private void Awake()
    {
        if (worldMapUI != null) worldMapUI.SetActive(false);
        if (Instance != null && Instance != this) Destroy(gameObject);
        else
        {
            Instance = this;
            // [추가] portalTilemap에서 Rigidbody2D 컴포넌트를 찾아옴
            if (portalTilemap != null)
            {
                portalRigidbody = portalTilemap.GetComponent<Rigidbody2D>();
                portalCollider = portalTilemap.GetComponent<TilemapCollider2D>();
            }
        }
    }

    public void Start()
    {
       
        StartCoroutine(RegisterEventListeners());
        RoomFirstDungeonGenerator.DungeonStart();
    }
    private IEnumerator RegisterEventListeners()
    {
        // EventManager.Instance가 null이 아닐 때까지 매 프레임 기다립니다.
        yield return new WaitUntil(() => EventManager.Instance != null);

        // EventManager가 준비되었으므로 안전하게 리스너를 등록합니다.
        EventManager.Instance.RegisterEnemyDeathListener(HandleEnemyDeath);
        Debug.Log("DungeonManager: EnemyDeath 리스너 등록 성공!");

        // InputManager 리스너도 여기서 등록하는 것이 더 안전합니다.
        if (InputManager.Instance != null)
        {
            InputManager.Instance.onMap += WorldMapUI;
        }

        // 던전 생성 이벤트 리스너 등록
        RoomFirstDungeonGenerator.OnDungeonGenerated += SpawnPlayerAtStart;
    }
    private void OnDestroy()
    {
        if (EventManager.Instance != null)
        {
            EventManager.Instance.UnregisterEnemyDeathListener(HandleEnemyDeath);
        }
        if (InputManager.Instance != null)
        {
            InputManager.Instance.onMap -= WorldMapUI;
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
        // 죽은 몬스터가 속한 방을 찾는다.
        RoomFirstDungeonGenerator.Room room = deadEnemy.homeRoom;
        Debug.Log($"sdsdsdd.");
        // 방 정보가 있고, 그 방에 몬스터 목록이 있다면
        if (room != null && roomEnemies.ContainsKey(room))
        {
            // [핵심 수정] 이 방의 모든 몬스터가 사망 상태인지 확인한다.
            // LINQ의 All() 메서드를 사용해서 리스트의 모든 요소가 특정 조건을 만족하는지 검사
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
        // [추가] 게임 시작 시 포탈이 열려 있도록 보장
        UnlockPortals();
    }
    private void OnPlayerEnteredRoom(RoomFirstDungeonGenerator.Room newRoom, RoomFirstDungeonGenerator.Room oldRoom)
    {
        if (oldRoom != null)
        {
            SetMonstersActive(oldRoom, false);
        }
        SetMonstersActive(newRoom, true);

        // [수정] 방에 몬스터가 남아있다면 모든 포탈을 잠근다.
        if (!IsRoomCleared(newRoom))
        {
            LockPortals();
        }
    }
    // [수정] 플레이어가 현재 있는 방을 찾는 메서드
    public RoomFirstDungeonGenerator.Room FindCurrentPlayerRoom()
    {
        if (playerObject == null || AllDungeonRooms == null) return null;
        return FindRoomAt(Vector3Int.FloorToInt(playerObject.transform.position));
    }

    public bool IsRoomCleared(RoomFirstDungeonGenerator.Room room)
    {
        if (room == null) return true;
        if (!roomEnemies.ContainsKey(room) || roomEnemies[room].Count == 0) return true;

        // 방에 있는 모든 몬스터의 isDead 상태를 확인
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