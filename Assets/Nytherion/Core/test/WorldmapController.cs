/* WorldmapController.cs (전체 맵 표시 버전)

    [역할]
    UI 캔버스 위에 생성된 던전 전체를 한눈에 볼 수 있도록 그리고 관리합니다.

    [핵심 변경점]
    - (기능 변경) 맵이 플레이어를 따라다니는 대신, 생성된 모든 방 아이콘들이
      화면에 꽉 차도록 맵의 중심 위치와 스케일(줌)을 자동으로 조절합니다.
    - (로직 추가) FitMapToView() 함수를 추가하여 모든 방 아이콘을 포함하는
      경계(Bounds)를 계산하고, 이를 기반으로 맵을 중앙 정렬 및 스케일링합니다.
    - (플레이어 아이콘) 플레이어 아이콘은 더 이상 화면 중앙에 고정되지 않고,
      맵 위에서 플레이어가 위치한 방을 따라 움직입니다.
*/
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public class WorldmapController : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("맵 아이콘과 선들이 담길 부모 UI 패널입니다. 이 패널이 움직이고 스케일됩니다.")]
    [SerializeField] private RectTransform mapContent;
    [Tooltip("방 하나를 나타내는 UI 아이콘 프리팹입니다.")]
    [SerializeField] private GameObject roomIconPrefab;
    [Tooltip("플레이어의 현재 위치를 나타내는 UI 아이콘입니다. 맵 위에서 움직입니다.")]
    [SerializeField] private RectTransform playerIcon;
    [Tooltip("방과 방을 잇는 경로를 표시할 선 이미지 프리팹입니다.")]
    [SerializeField] private Image linePrefab;

    [Header("Map Settings")]
    [Tooltip("맵에 표시될 각 방 아이콘의 크기입니다.")]
    [SerializeField] private float roomIconSize = 20f;
    [Tooltip("맵에서 방 아이콘들 사이의 간격입니다.")]
    [SerializeField] private float iconSpacing = 30f;
    [Tooltip("맵 가장자리와 화면 테두리 사이의 여백입니다.")]
    [SerializeField] private float mapPadding = 50f;

    // 내부 데이터
  
    private List<RoomFirstDungeonGenerator.Room> allRooms;
    private Dictionary<Vector2Int, RectTransform> roomIconMap = new Dictionary<Vector2Int, RectTransform>();
    private RectTransform viewPort; // 맵이 보여질 영역 (이 스크립트의 부모)

    private void Awake()
    {
        viewPort = transform as RectTransform;
     
    }

    // 맵이 활성화될 때마다 맵을 화면에 다시 맞춥니다.
    // (화면 크기가 바뀌었을 수도 있기 때문)
    private void OnEnable()
    {
        if (DungeonManager.Instance != null && DungeonManager.Instance.playerObject != null && playerIcon != null)
        {
            // 플레이어 아이콘을 mapContent의 자식으로 만들고 활성화
            playerIcon.SetParent(mapContent, false);
            // playerIcon.gameObject.SetActive(true);
            playerIcon.transform.SetAsLastSibling();
        }
        if (roomIconMap.Count > 0)
        {
            FitMapToView();
        }
    }

    private void LateUpdate()
    {
        // 플레이어 아이콘 위치만 업데이트합니다.
        if (DungeonManager.Instance != null && DungeonManager.Instance.playerObject != null && playerIcon.gameObject.activeSelf)
        {
            UpdatePlayerIconPosition();
        }
    }

    /// <summary>
    /// 전달받은 방 정보로 맵 전체를 그리고, 화면에 맞게 조절합니다.
    /// </summary>
    public void DrawMap(
        ICollection<RoomFirstDungeonGenerator.Room> rooms,
        List<Tuple<RoomFirstDungeonGenerator.Room, RoomFirstDungeonGenerator.Room>> connections,
        DungeonData dungeonData)
    {
        ClearMap();
        if (rooms == null || rooms.Count == 0 || roomIconPrefab == null) return;

        allRooms = new List<RoomFirstDungeonGenerator.Room>(rooms);

        // 1. 방 아이콘과 연결선 그리기
        foreach (var room in rooms)
        {
            GameObject icon = Instantiate(roomIconPrefab, mapContent);
            RectTransform iconRect = icon.GetComponent<RectTransform>();
            Image iconImage = icon.GetComponent<Image>();

            iconRect.anchoredPosition = (Vector2)room.gridPos * iconSpacing;
            iconRect.sizeDelta = new Vector2(roomIconSize, roomIconSize);

            if (iconImage != null)
            {
                iconImage.color = GetColorForRoom(room.type, dungeonData);
            }
            roomIconMap[room.gridPos] = iconRect;
        }

        if (linePrefab != null)
        {
            foreach (var connection in connections)
            {
                var posA = roomIconMap[connection.Item1.gridPos].anchoredPosition;
                var posB = roomIconMap[connection.Item2.gridPos].anchoredPosition;
                DrawLine(posA, posB);
            }
        }

        // 2. 그린 맵을 화면에 맞게 중앙 정렬 및 스케일 조절
        FitMapToView();
    }

    /// <summary>
    /// 생성된 모든 방 아이콘들이 뷰포트에 꽉 차도록 맵의 위치와 스케일을 조절합니다.
    /// </summary>
    private void FitMapToView()
    {
        if (roomIconMap.Count == 0 || viewPort == null) return;

        // 1. 모든 방 아이콘을 포함하는 경계(Bounds) 계산
        Vector2 min = roomIconMap.First().Value.anchoredPosition;
        Vector2 max = min;

        foreach (var icon in roomIconMap.Values)
        {
            min.x = Mathf.Min(min.x, icon.anchoredPosition.x);
            min.y = Mathf.Min(min.y, icon.anchoredPosition.y);
            max.x = Mathf.Max(max.x, icon.anchoredPosition.x);
            max.y = Mathf.Max(max.y, icon.anchoredPosition.y);
        }

        // 2. 맵의 실제 크기와 중심점 계산
        Vector2 mapSize = max - min;
        Vector2 mapCenter = min + mapSize / 2;

        // 3. 뷰포트 크기를 기반으로 최적의 스케일 계산
        //    (아이콘 크기와 패딩을 고려하여 여백 확보)
        float scaleX = (viewPort.rect.width - mapPadding) / (mapSize.x + roomIconSize);
        float scaleY = (viewPort.rect.height - mapPadding) / (mapSize.y + roomIconSize);
        float optimalScale = Mathf.Min(scaleX, scaleY);

        // 스케일이 너무 커지는 것을 방지 (예: 방이 하나일 때)
        optimalScale = Mathf.Min(optimalScale, 2.0f);


        // 4. 계산된 스케일과 위치를 적용
        mapContent.localScale = new Vector3(optimalScale, optimalScale, 1f);
        mapContent.anchoredPosition = -mapCenter * optimalScale;
    }

    /// <summary>
    /// 플레이어 아이콘을 현재 플레이어가 있는 방의 아이콘 위치로 이동시킵니다.
    /// </summary>
    private void UpdatePlayerIconPosition()
    {
        if (allRooms == null || allRooms.Count == 0) return;

        // DungeonManager에서 직접 플레이어의 Transform 정보를 가져온다
        Transform playerTransform = DungeonManager.Instance.playerObject.transform;

        // 플레이어와 가장 가까운 방 찾기
        RoomFirstDungeonGenerator.Room closestRoom = allRooms
            .OrderBy(room => ((Vector2)playerTransform.position - room.center).sqrMagnitude)
            .FirstOrDefault();

        if (closestRoom != null && roomIconMap.ContainsKey(closestRoom.gridPos))
        {
            // 플레이어 아이콘을 해당 방 아이콘 위치로 이동
            playerIcon.anchoredPosition = roomIconMap[closestRoom.gridPos].anchoredPosition;
        }
    }



    private void DrawLine(Vector2 posA, Vector2 posB)
    {
        Image line = Instantiate(linePrefab, mapContent);
        RectTransform lineRect = line.rectTransform;

        lineRect.SetAsFirstSibling();

        Vector2 diff = posB - posA;
        lineRect.sizeDelta = new Vector2(diff.magnitude, lineRect.sizeDelta.y);
        lineRect.anchoredPosition = posA + diff / 2;
        lineRect.localEulerAngles = new Vector3(0, 0, Mathf.Atan2(diff.y, diff.x) * Mathf.Rad2Deg);
    }

    private Color GetColorForRoom(RoomFirstDungeonGenerator.RoomType type, DungeonData dungeonData)
    {
        foreach (var colorMapping in dungeonData.minimapRoomColors)
        {
            if (colorMapping.type == type)
            {
                return colorMapping.color;
            }
        }
        // 설정된 색상이 없으면 기본 회색을 반환합니다.
        return Color.gray;
    }

    private void ClearMap()
    {
        if (mapContent == null) return;

        foreach (Transform child in mapContent)
        {
            // 플레이어 아이콘은 삭제하지 않도록 예외 처리
            if (playerIcon != null && child == playerIcon.transform) continue;
            Destroy(child.gameObject);
        }
        roomIconMap.Clear();
    }
}
