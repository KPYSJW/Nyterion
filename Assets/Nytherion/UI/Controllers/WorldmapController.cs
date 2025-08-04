// ScriptsArchive/WorldmapController.cs

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Nytherion.GamePlay.Dungeon;
using Zenject; // Zenject 네임스페이스 추가

namespace Nytherion.UI.Controllers
{
    public class WorldmapController : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private RectTransform mapContent;
        [SerializeField] private GameObject roomIconPrefab;
        [SerializeField] private RectTransform playerIcon;
        [SerializeField] private Image linePrefab;

        [Header("Map Settings")]
        [SerializeField] private float roomIconSize = 20f;
        [SerializeField] private float iconSpacing = 30f;
        [SerializeField] private float mapPadding = 50f;

        private Dictionary<Vector2Int, RectTransform> roomIconMap = new Dictionary<Vector2Int, RectTransform>();
        private RectTransform viewPort;

        // --- 의존성 주입 ---
        private DungeonManager _dungeonManager;

        [Inject]
        public void Construct(DungeonManager dungeonManager)
        {
            _dungeonManager = dungeonManager;
        }

        private void Awake()
        {
            viewPort = transform as RectTransform;
        }

        private void OnEnable()
        {
            if (_dungeonManager != null && _dungeonManager.playerObject != null && playerIcon != null)
            {
                playerIcon.SetParent(mapContent, false);
                playerIcon.transform.SetAsLastSibling();
            }
            if (roomIconMap.Count > 0)
            {
                FitMapToView();
            }
        }

        private void LateUpdate()
        {
            if (_dungeonManager != null && _dungeonManager.playerObject != null && playerIcon.gameObject.activeSelf)
            {
                UpdatePlayerIconPosition();
            }
        }

        public void DrawMap(
            ICollection<RoomFirstDungeonGenerator.Room> rooms,
            List<Tuple<RoomFirstDungeonGenerator.Room, RoomFirstDungeonGenerator.Room>> connections,
            DungeonData dungeonData)
        {
            ClearMap();
            if (rooms == null || rooms.Count == 0 || roomIconPrefab == null) return;

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
            FitMapToView();
        }

        private void UpdatePlayerIconPosition()
        {
            var allRooms = _dungeonManager.AllDungeonRooms;
            if (allRooms == null || allRooms.Count == 0) return;

            Transform playerTransform = _dungeonManager.playerObject.transform;

            RoomFirstDungeonGenerator.Room closestRoom = allRooms
                .OrderBy(room => ((Vector2)playerTransform.position - room.center).sqrMagnitude)
                .FirstOrDefault();

            if (closestRoom != null && roomIconMap.ContainsKey(closestRoom.gridPos))
            {
                playerIcon.anchoredPosition = roomIconMap[closestRoom.gridPos].anchoredPosition;
            }
        }

        private void FitMapToView()
        {
            if (roomIconMap.Count == 0 || viewPort == null) return;

            Vector2 min = roomIconMap.First().Value.anchoredPosition;
            Vector2 max = min;

            foreach (var icon in roomIconMap.Values)
            {
                min.x = Mathf.Min(min.x, icon.anchoredPosition.x);
                min.y = Mathf.Min(min.y, icon.anchoredPosition.y);
                max.x = Mathf.Max(max.x, icon.anchoredPosition.x);
                max.y = Mathf.Max(max.y, icon.anchoredPosition.y);
            }

            Vector2 mapSize = max - min;
            Vector2 mapCenter = min + mapSize / 2;

            float scaleX = (viewPort.rect.width - mapPadding) / (mapSize.x + roomIconSize);
            float scaleY = (viewPort.rect.height - mapPadding) / (mapSize.y + roomIconSize);
            float optimalScale = Mathf.Min(scaleX, scaleY);
            optimalScale = Mathf.Min(optimalScale, 2.0f);

            mapContent.localScale = new Vector3(optimalScale, optimalScale, 1f);
            mapContent.anchoredPosition = -mapCenter * optimalScale;
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
                if (colorMapping.type == type) return colorMapping.color;
            }
            return Color.gray;
        }

        private void ClearMap()
        {
            if (mapContent == null) return;
            foreach (Transform child in mapContent)
            {
                if (playerIcon != null && child == playerIcon.transform) continue;
                Destroy(child.gameObject);
            }
            roomIconMap.Clear();
        }
    }
}