using UnityEngine;
using UnityEngine.Tilemaps;

[CreateAssetMenu(fileName = "DungeonGenerationData_Refactored", menuName = "Data/Dungeon Generation Data (Refactored)")]
public class DungeonData : ScriptableObject
{
    [Header("Room Settings")]
    [Tooltip("생성할 방의 총 개수입니다.")]
    public int desiredNumberOfRooms = 15;

    [Tooltip("생성될 방의 최소 크기입니다 (가로, 세로).")]
    public Vector2Int minRoomSize = new Vector2Int(8, 8);

    [Tooltip("생성될 방의 최대 크기입니다 (가로, 세로).")]
    public Vector2Int maxRoomSize = new Vector2Int(15, 15);

    [Header("Special Room Settings")]
    [Tooltip("생성할 보스 방의 개수입니다.")]
    public int numberOfBossRooms = 1;
    [Tooltip("생성할 상점 방의 개수입니다.")]
    public int numberOfShopRooms = 1;
    [Tooltip("생성할 아이템 방의 개수입니다.")]
    public int numberOfItemRooms = 2;

    [Header("Room Style")]
    [Tooltip("방이 단순한 사각형이 아닌, 복합적인 모양이 될 확률입니다.")]
    [Range(0, 1)]
    public float compoundRoomChance = 0.7f;


    [Header("Prefabricated Rooms")]
    [Tooltip("미리 디자인된 보스방 프리팹을 할당합니다.")]
    public Tilemap bossRoomPrefab;

    [Tooltip("보스방 프리팹의 크기입니다. 다른 방과의 간격 계산에 사용됩니다.")]
    public Vector2Int bossRoomSize = new Vector2Int(30, 30);
}
