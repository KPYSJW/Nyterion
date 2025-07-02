/* DungeonData.cs (미니맵 설정 추가)

    [역할]
    던전 생성에 필요한 모든 설정값을 담는 ScriptableObject.

    [핵심 변경점]
    - (신규) MinimapRoomColor 구조체: 미니맵에 표시될 방의 종류(Type)와 색상(Color)을
      한 쌍으로 묶는 새로운 데이터 구조를 정의했습니다.
    - (신규) minimapRoomColors 필드: MinimapRoomColor의 배열을 사용하여,
      에디터에서 각 특수방의 미니맵 색상을 직접 지정할 수 있습니다.
*/
using System; // System.Serializable을 사용하기 위해 필요
using UnityEngine;
using UnityEngine.Tilemaps;

// --- 추가된 부분 ---
/// <summary>
/// 미니맵에 표시될 방의 타입과 색상을 한 쌍으로 묶는 데이터 구조체
/// </summary>
[Serializable]
public struct MinimapRoomColor
{
    public RoomFirstDungeonGenerator.RoomType type;
    public Color color;
}
// --- 여기까지 ---


[CreateAssetMenu(fileName = "DungeonGenerationData", menuName = "Procedural Generation/Dungeon Generation Data")]
public class DungeonData : ScriptableObject
{
    [Header("Room Settings")]
    public int desiredNumberOfRooms = 15;
    public Vector2Int minRoomSize = new Vector2Int(8, 8);
    public Vector2Int maxRoomSize = new Vector2Int(15, 15);
    [Range(0, 1)]
    public float compoundRoomChance = 0.7f;

    [Header("Special Room Settings")]
    public int numberOfShopRooms = 1;
    public int numberOfItemRooms = 2;

    [Header("Prefabricated Rooms")]
    public Tilemap bossRoomPrefab;

    [Header("Obstacle Settings")]
    [Tooltip("방에 배치될 장애물 목록입니다. 각 장애물에 맞는 프리팹과 크기를 설정해주세요.")]
    public ObstacleData[] obstacles;
    [Tooltip("일반 방 하나에 생성될 장애물의 최소 개수입니다.")]
    public int minObstaclesPerRoom = 1;
    [Tooltip("일반 방 하나에 생성될 장애물의 최대 개수입니다.")]
    public int maxObstaclesPerRoom = 3;

    // --- 추가된 부분 ---
    [Header("Minimap Settings")]
    [Tooltip("미니맵에 표시될 각 방 타입의 색상입니다.")]
    public MinimapRoomColor[] minimapRoomColors;
    // --- 여기까지 ---
}

/// <summary>
/// 장애물 프리팹과 그 크기 정보를 한 쌍으로 묶는 데이터 클래스
/// </summary>
[Serializable]
public class ObstacleData
{
    [Tooltip("장애물로 사용할 게임 오브젝트 프리팹입니다.")]
    public GameObject prefab;
    [Tooltip("이 장애물의 크기입니다 (타일 단위).")]
    public Vector2Int size = Vector2Int.one;
}
