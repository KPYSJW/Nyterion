using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Nytherion.GamePlay.Dungeon;
using VContainer;
using Nytherion.Data.ScriptableObjects.Dungeon;

namespace Nytherion.UI.Map
{
    /// <summary>
    /// 전체 던전 구조를 보여주는 월드맵 UI를 제어하는 클래스입니다.
    /// </summary>
    public class WorldmapController : MonoBehaviour
    {
        [Header("UI 참조")]
        [SerializeField] private RectTransform mapContent; // 모든 맵 아이콘과 선이 위치할 부모 RectTransform
        [SerializeField] private GameObject roomIconPrefab; // 방을 나타내는 아이콘 프리팹
        [SerializeField] private RectTransform playerIcon; // 플레이어 위치를 나타내는 아이콘
        [SerializeField] private Image linePrefab; // 방과 방을 연결하는 선 프리팹

        [Header("맵 설정")]
        [SerializeField] private float roomIconSize = 20f; // 방 아이콘의 크기
        [SerializeField] private float iconSpacing = 30f; // 아이콘 간의 간격
        [SerializeField] private float mapPadding = 50f; // 맵과 화면 가장자리 사이의 여백

        // 생성된 방 아이콘들을 그리드 위치로 빠르게 찾기 위한 딕셔너리
        private readonly Dictionary<Vector2Int, RectTransform> roomIconMap = new Dictionary<Vector2Int, RectTransform>();
        private RectTransform viewPort; // 월드맵이 표시되는 화면(보통 이 스크립트가 붙은 RectTransform)

        // --- 의존성 주입 ---
        private DungeonManager dungeonManager;

        [Inject]
        public void Construct(DungeonManager dungeonManager = null)
        {
            this.dungeonManager = dungeonManager;
            if (dungeonManager == null)
            {
                Debug.LogWarning("[WorldmapController] DungeonManager가 주입되지 않았습니다. 던전 관련 기능이 작동하지 않습니다.");
            }
            else
            {
                Debug.Log("[WorldmapController] DungeonManager가 성공적으로 주입되었습니다.");
            }
        }
        private void Awake()
        {
            viewPort = transform as RectTransform;
            Hide(); // 처음에는 숨겨진 상태로 시작
        }

        private void OnEnable()
        {
            // 월드맵이 활성화될 때 플레이어 아이콘을 맵 컨텐츠의 자식으로 설정
            if (dungeonManager != null && dungeonManager.playerObject != null && playerIcon != null)
            {
                playerIcon.SetParent(mapContent, false);
                playerIcon.transform.SetAsLastSibling(); // 항상 맨 위에 보이도록
            }
            // 맵이 이미 그려져 있다면 화면에 맞게 크기를 조절
            if (roomIconMap.Count > 0)
            {
                FitMapToView();
            }
        }

        private void LateUpdate()
        {
            // 플레이어 아이콘이 활성화되어 있다면 매 프레임 위치를 업데이트
            if (dungeonManager != null && dungeonManager.playerObject != null && playerIcon.gameObject.activeSelf)
            {
                UpdatePlayerIconPosition();
            }
        }

        #region Public 제어 메서드

        /// <summary>
        /// 월드맵 UI를 켜거나 끕니다.
        /// </summary>
        public void Toggle()
        {
            gameObject.SetActive(!gameObject.activeSelf);
        }

        /// <summary>
        /// 월드맵 UI를 강제로 켭니다.
        /// </summary>
        public void Show()
        {
            gameObject.SetActive(true);
        }

        /// <summary>
        /// 월드맵 UI를 강제로 끕니다.
        /// </summary>
        public void Hide()
        {
            gameObject.SetActive(false);
        }

        #endregion

        /// <summary>
        /// 전달받은 던전 데이터로 월드맵 UI를 생성합니다.
        /// </summary>
        public void DrawMap(
            ICollection<RoomFirstDungeonGenerator.Room> rooms,
            List<Tuple<RoomFirstDungeonGenerator.Room, RoomFirstDungeonGenerator.Room>> connections,
            DungeonData dungeonData)
        {
            ClearMap();
            if (rooms == null || rooms.Count == 0 || roomIconPrefab == null) return;

            // 각 기능을 별도의 메서드로 분리하여 가독성 향상
            CreateRoomIcons(rooms, dungeonData);
            DrawConnectionLines(connections);

            // 생성된 맵이 화면에 잘 보이도록 스케일과 위치를 조절
            FitMapToView();
        }

        /// <summary>
        /// 모든 방의 아이콘을 생성하고 딕셔너리에 저장합니다.
        /// </summary>
        private void CreateRoomIcons(ICollection<RoomFirstDungeonGenerator.Room> rooms, DungeonData dungeonData)
        {
            foreach (RoomFirstDungeonGenerator.Room room in rooms)
            {
                GameObject iconObject = Instantiate(roomIconPrefab, mapContent);
                RectTransform iconRect = iconObject.GetComponent<RectTransform>();
                Image iconImage = iconObject.GetComponent<Image>();

                // 방의 그리드 위치를 기반으로 UI 좌표를 설정
                iconRect.anchoredPosition = (Vector2)room.gridPos * iconSpacing;
                iconRect.sizeDelta = new Vector2(roomIconSize, roomIconSize);

                // 방 종류에 맞는 색상으로 아이콘을 칠함
                if (iconImage != null)
                {
                    iconImage.color = GetColorForRoom(room.type, dungeonData);
                }
                roomIconMap[room.gridPos] = iconRect;
            }
        }

        /// <summary>
        /// 방과 방 사이를 연결하는 선 UI를 생성합니다.
        /// </summary>
        private void DrawConnectionLines(List<Tuple<RoomFirstDungeonGenerator.Room, RoomFirstDungeonGenerator.Room>> connections)
        {
            if (linePrefab == null) return;

            foreach (Tuple<RoomFirstDungeonGenerator.Room, RoomFirstDungeonGenerator.Room> connection in connections)
            {
                // 딕셔너리에서 두 방의 아이콘을 찾아서 연결
                if (roomIconMap.TryGetValue(connection.Item1.gridPos, out RectTransform iconA) &&
                    roomIconMap.TryGetValue(connection.Item2.gridPos, out RectTransform iconB))
                {
                    DrawLine(iconA.anchoredPosition, iconB.anchoredPosition);
                }
            }
        }

        /// <summary>
        /// 플레이어의 현재 방을 DungeonManager로부터 직접 받아와 아이콘 위치를 업데이트합니다.
        /// 이 방식은 모든 방을 순회하는 것보다 훨씬 효율적입니다.
        /// </summary>
        private void UpdatePlayerIconPosition()
        {
            // DungeonManager가 없으면 업데이트할 수 없음
            if (dungeonManager == null) return;
            
            // DungeonManager에게 현재 플레이어가 있는 방을 직접 물어봅니다. (O(1) 시간 복잡도)
            RoomFirstDungeonGenerator.Room currentPlayerRoom = dungeonManager.FindCurrentPlayerRoom();

            // 현재 방이 있고, 해당 방의 아이콘이 맵에 존재한다면
            if (currentPlayerRoom != null && roomIconMap.TryGetValue(currentPlayerRoom.gridPos, out RectTransform roomIcon))
            {
                // 플레이어 아이콘의 위치를 현재 방 아이콘의 위치로 바로 설정합니다.
                playerIcon.anchoredPosition = roomIcon.anchoredPosition;
            }
        }

        /// <summary>
        /// 생성된 맵의 전체 크기에 맞춰 UI가 화면에 잘 보이도록 스케일과 위치를 조절합니다.
        /// </summary>
        private void FitMapToView()
        {
            if (roomIconMap.Count == 0 || viewPort == null) return;

            // 모든 아이콘을 포함하는 경계를 계산
            Vector2 min = roomIconMap.First().Value.anchoredPosition;
            Vector2 max = min;
            foreach (RectTransform icon in roomIconMap.Values)
            {
                min.x = Mathf.Min(min.x, icon.anchoredPosition.x);
                min.y = Mathf.Min(min.y, icon.anchoredPosition.y);
                max.x = Mathf.Max(max.x, icon.anchoredPosition.x);
                max.y = Mathf.Max(max.y, icon.anchoredPosition.y);
            }

            Vector2 mapSize = max - min;
            Vector2 mapCenter = min + mapSize / 2;

            // 화면 크기와 맵 크기를 비교하여 최적의 스케일 값을 계산
            float scaleX = (viewPort.rect.width - mapPadding) / (mapSize.x + roomIconSize);
            float scaleY = (viewPort.rect.height - mapPadding) / (mapSize.y + roomIconSize);
            float optimalScale = Mathf.Min(scaleX, scaleY);
            optimalScale = Mathf.Min(optimalScale, 2.0f); // 너무 커지는 것을 방지

            // 계산된 스케일과 위치를 적용
            mapContent.localScale = new Vector3(optimalScale, optimalScale, 1f);
            mapContent.anchoredPosition = -mapCenter * optimalScale;
        }

        /// <summary>
        /// 두 지점 사이에 선 UI를 그립니다.
        /// </summary>
        private void DrawLine(Vector2 posA, Vector2 posB)
        {
            Image lineImage = Instantiate(linePrefab, mapContent);
            RectTransform lineRect = lineImage.rectTransform;
            lineRect.SetAsFirstSibling(); // 선이 아이콘 뒤에 그려지도록

            Vector2 diff = posB - posA;
            lineRect.sizeDelta = new Vector2(diff.magnitude, lineRect.sizeDelta.y); // 길이를 두 점 사이의 거리로 설정
            lineRect.anchoredPosition = posA + diff / 2; // 위치를 두 점의 중앙으로 설정
            lineRect.localEulerAngles = new Vector3(0, 0, Mathf.Atan2(diff.y, diff.x) * Mathf.Rad2Deg); // 각도를 계산하여 회전
        }

        /// <summary>
        /// 방 종류에 맞는 색상을 DungeonData에서 찾아 반환합니다.
        /// </summary>
        private Color GetColorForRoom(RoomFirstDungeonGenerator.RoomType type, DungeonData dungeonData)
        {
            // LINQ를 사용하여 더 간결하게 표현
            return dungeonData.minimapRoomColors
                              .FirstOrDefault(colorMapping => colorMapping.type == type).color;
        }

        /// <summary>
        /// 월드맵의 모든 아이콘과 선을 지웁니다.
        /// </summary>
        private void ClearMap()
        {
            if (mapContent == null) return;
            foreach (Transform child in mapContent)
            {
                // 플레이어 아이콘은 지우지 않도록 예외 처리
                if (playerIcon != null && child == playerIcon.transform) continue;
                Destroy(child.gameObject);
            }
            roomIconMap.Clear();
        }
    }
}
