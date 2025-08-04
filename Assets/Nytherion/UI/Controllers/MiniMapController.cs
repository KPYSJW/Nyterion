// ScriptsArchive/MinimapController.cs

using UnityEngine;
using System.Linq;
using Nytherion.GamePlay.Dungeon;
using Zenject; // Zenject 네임스페이스 추가

namespace Nytherion.UI.Controllers
{
    public class MinimapController : MonoBehaviour
    {
        public Transform playerTransform;
        private Camera minimapCamera;

        private RoomFirstDungeonGenerator.Room currentRoom;

        [Tooltip("카메라와 방 경계 사이의 여백, 뷰를 더 넓게 보여줍니다.")]
        public float padding = 2f;

        // --- 의존성 주입 ---
        private DungeonManager _dungeonManager;

        [Inject]
        public void Construct(DungeonManager dungeonManager)
        {
            _dungeonManager = dungeonManager;
        }

        void Start()
        {
            minimapCamera = GetComponent<Camera>();
        }

        void LateUpdate()
        {
            // DungeonManager.Instance 대신 주입받은 _dungeonManager 사용
            if (playerTransform == null || _dungeonManager == null || _dungeonManager.AllDungeonRooms == null) return;

            RoomFirstDungeonGenerator.Room roomPlayerIsIn = FindCurrentPlayerRoom();

            if (roomPlayerIsIn != null && roomPlayerIsIn != currentRoom)
            {
                currentRoom = roomPlayerIsIn;
                UpdateCameraToFitRoom(currentRoom);
            }
        }

        public RoomFirstDungeonGenerator.Room FindCurrentPlayerRoom()
        {
            // DungeonManager.Instance 대신 주입받은 _dungeonManager 사용
            return _dungeonManager.AllDungeonRooms
                .OrderBy(room => Vector2.Distance(playerTransform.position, room.center))
                .FirstOrDefault();
        }

        private void UpdateCameraToFitRoom(RoomFirstDungeonGenerator.Room room)
        {
            Vector3 targetPosition = new Vector3(room.center.x, room.center.y, transform.position.z);
            transform.position = targetPosition;

            float roomWidth = room.Bounds.size.x + padding;
            float roomHeight = room.Bounds.size.y + padding;

            float sizeForWidth = roomWidth * minimapCamera.pixelHeight / minimapCamera.pixelWidth * 0.5f;
            float sizeForHeight = roomHeight * 0.5f;

            minimapCamera.orthographicSize = Mathf.Max(sizeForWidth, sizeForHeight);
        }
    }
}