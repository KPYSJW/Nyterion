using UnityEngine;
using System.Linq;
using Nytherion.GamePlay.Dungeon;

namespace Nytherion.UI.Controllers
{
    public class MinimapController : MonoBehaviour
    {
        public Transform playerTransform;
        private Camera minimapCamera;


        private RoomFirstDungeonGenerator.Room currentRoom;

        [Tooltip(" ǥ ,  ׵ ϴ.")]
        public float padding = 2f;

        void Start()
        {
            minimapCamera = GetComponent<Camera>();
        }

        void LateUpdate()
        {
            if (playerTransform == null || DungeonManager.Instance == null || DungeonManager.Instance.AllDungeonRooms == null) return;

            RoomFirstDungeonGenerator.Room roomPlayerIsIn = FindCurrentPlayerRoom();

            if (roomPlayerIsIn != null && roomPlayerIsIn != currentRoom)
            {
                currentRoom = roomPlayerIsIn;

                UpdateCameraToFitRoom(currentRoom);
            }
        }


        public RoomFirstDungeonGenerator.Room FindCurrentPlayerRoom()
        {
            return DungeonManager.Instance.AllDungeonRooms
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