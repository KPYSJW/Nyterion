using UnityEngine;
using System.Linq;
using Nytherion.GamePlay.Dungeon;
using Zenject;

namespace Nytherion.UI.Controllers
{
    public class MinimapController : MonoBehaviour
    {
        public Transform playerTransform;
        private Camera minimapCamera;


        private RoomFirstDungeonGenerator.Room currentRoom;

        public float padding = 2f;
        private DungeonManager dungeonManager;
        [Inject]
        public void Construct(DungeonManager dungeonManager)
        {
            this.dungeonManager = dungeonManager;
        }
        void Start()
        {
            minimapCamera = GetComponent<Camera>();
        }

        void LateUpdate()
        {
            if (playerTransform == null || dungeonManager == null || dungeonManager.AllDungeonRooms == null) return;

            RoomFirstDungeonGenerator.Room roomPlayerIsIn = FindCurrentPlayerRoom();

            if (roomPlayerIsIn != null && roomPlayerIsIn != currentRoom)
            {
                currentRoom = roomPlayerIsIn;

                UpdateCameraToFitRoom(currentRoom);
            }
        }


        public RoomFirstDungeonGenerator.Room FindCurrentPlayerRoom()
        {
            return dungeonManager.AllDungeonRooms
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

            Debug.Log($"{room.type}  !  .");
        }
    }
}