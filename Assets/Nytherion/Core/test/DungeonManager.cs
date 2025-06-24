/* DungeonManager.cs (새로운 스크립트)

    [역할]
    이 스크립트는 생성된 던전의 '상태 정보'를 전역적으로 관리하는 싱글턴 매니저입니다.
    게임 플레이 중에 던전의 데이터를 기억하고 다른 스크립트들이 참조할 수 있도록 합니다.
    - 포탈들이 서로 어디로 연결되어 있는지 기억합니다. (A포탈 -> B포탈)
    - (미래 확장) 각 방의 몬스터가 모두 처리되었는지 여부를 관리합니다.

    [사용법]
    - 씬에 빈 게임 오브젝트를 하나 만들고 이 스크립트를 붙여줍니다.
    - 다른 스크립트에서는 'DungeonManager.Instance'를 통해 언제든지 접근할 수 있습니다.
*/
using System.Collections.Generic;
using UnityEngine;

public class DungeonManager : MonoBehaviour
{
    // 싱글턴 인스턴스: 게임 내에서 오직 하나만 존재하도록 보장
    public static DungeonManager Instance { get; private set; }

    // 포탈 연결 정보를 저장하는 딕셔너리
    // Key: 한쪽 포탈의 위치, Value: 연결된 반대편 포탈의 위치
    private Dictionary<Vector3Int, Vector3Int> portalLinks = new Dictionary<Vector3Int, Vector3Int>();

    // TODO: 방의 몬스터 클리어 여부를 관리할 데이터 구조 (나중에 구현)
    // private Dictionary<Vector2Int, bool> roomClearStatus = new Dictionary<Vector2Int, bool>();

    public GameObject playerObject;

    // 스크립트가 활성화될 때 이벤트 구독을 시작합니다.
    private void OnEnable()
    {
        RoomFirstDungeonGenerator.OnDungeonGenerated += SpawnPlayerAtStart;
    }

    // 스크립트가 비활성화될 때 이벤트 구독을 해제합니다. (메모리 누수 방지)
    private void OnDisable()
    {
        RoomFirstDungeonGenerator.OnDungeonGenerated -= SpawnPlayerAtStart;
    }


    private void Awake()
    {
        // 싱글턴 패턴 구현
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
            // DontDestroyOnLoad(gameObject); // 씬이 바뀌어도 파괴되지 않게 하려면 주석 해제
        }
    }

    /// <summary>
    /// 두 포탈의 위치를 서로 연결하여 딕셔너리에 등록합니다.
    /// </summary>
    public void RegisterPortalPair(Vector3Int portalA, Vector3Int portalB)
    {
        // 양방향으로 등록해야 어느 쪽에서든 반대편을 찾을 수 있습니다.
        portalLinks[portalA] = portalB;
        portalLinks[portalB] = portalA;
    }

    /// <summary>
    /// 주어진 포탈 위치에 연결된 목적지 포탈의 위치를 반환합니다.
    /// </summary>
    public bool TryGetDestination(Vector3Int currentPortalPos, out Vector3Int destinationPos)
    {
        return portalLinks.TryGetValue(currentPortalPos, out destinationPos);
    }

    /// <summary>
    /// 특정 방이 클리어되었는지 확인합니다. (미래 기능)
    /// </summary>
    public bool IsRoomCleared(Vector2Int roomCoord) // 방을 식별할 방법이 필요 (예: 방의 그리드 좌표)
    {
        // TODO: 나중에 몬스터 시스템이 구현되면, 해당 방의 몬스터가 모두 죽었는지 확인하는 로직 추가
        // 예: return roomClearStatus.ContainsKey(roomCoord) && roomClearStatus[roomCoord];

        // 지금은 항상 true를 반환하여 포탈이 즉시 작동하도록 합니다.
        return true;
    }

    /// <summary>
    /// 새로운 던전이 생성될 때 기존 데이터를 초기화합니다.
    /// </summary>
    public void ClearDungeonData()
    {
        portalLinks.Clear();
        // roomClearStatus.Clear();
    }

    private void SpawnPlayerAtStart(Vector2 startRoomCenter)
    {
        // 아직 플레이어 참조가 없다면, 씬에서 "Player" 태그로 찾아옵니다.
        if (playerObject == null)
        {
            playerObject = GameObject.FindGameObjectWithTag("Player");
        }

        // 플레이어를 찾았다면 시작 위치로 이동시킵니다.
        if (playerObject != null)
        {
            // Vector2를 Vector3로 변환하여 위치를 설정합니다.
            playerObject.transform.position = new Vector3(startRoomCenter.x, startRoomCenter.y, 0);
            Debug.Log($"플레이어를 시작 지점 {startRoomCenter}에 스폰했습니다!");
        }
        else
        {
            Debug.LogError("Player 오브젝트를 찾을 수 없습니다! 플레이어에 'Player' 태그가 있는지 확인해주세요.");
        }
    }
}


