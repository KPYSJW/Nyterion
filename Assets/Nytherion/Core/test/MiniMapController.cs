// MinimapController.cs (새로운 버전)
using UnityEngine;
using System.Linq; // OrderBy, FirstOrDefault를 사용하기 위해 추가

// 이 스크립트는 MinimapCamera에 붙어있어야 해
public class MinimapController : MonoBehaviour
{
    public Transform playerTransform;
    private Camera minimapCamera;

    // 현재 플레이어가 어떤 방에 있는지 기억하기 위한 변수
    private RoomFirstDungeonGenerator.Room currentRoom;

    [Tooltip("미니맵에 방을 표시할 때, 방 테두리와 미니맵 경계 사이의 여백")]
    public float padding = 2f; // 방이 너무 꽉 껴 보이지 않게 여백을 좀 주자

    void Start()
    {
        minimapCamera = GetComponent<Camera>();

   
        
    }

    void LateUpdate()
    {
        // 필요한 정보가 없으면 아무것도 하지 않음
        if (playerTransform == null || DungeonManager.Instance == null || DungeonManager.Instance.AllDungeonRooms == null) return;

        // 플레이어가 현재 어느 방에 있는지 찾는다
        RoomFirstDungeonGenerator.Room roomPlayerIsIn = FindCurrentPlayerRoom();

        // 플레이어가 새로운 방에 들어갔거나, 아직 현재 방이 설정되지 않았다면
        if (roomPlayerIsIn != null && roomPlayerIsIn != currentRoom)
        {
            // 현재 방을 새로 찾은 방으로 업데이트하고
            currentRoom = roomPlayerIsIn;
            // 카메라가 새 방에 맞게 위치와 크기를 조절하도록 한다
            UpdateCameraToFitRoom(currentRoom);
        }
    }

    /// <summary>
    /// 플레이어와 가장 가까운 방을 찾아서 반환하는 함수
    /// </summary>
    private RoomFirstDungeonGenerator.Room FindCurrentPlayerRoom()
    {
        return DungeonManager.Instance.AllDungeonRooms
            .OrderBy(room => Vector2.Distance(playerTransform.position, room.center))
            .FirstOrDefault();
    }

    /// <summary>
    /// 카메라의 위치와 크기를 주어진 방에 딱 맞게 조절하는 함수
    /// </summary>
    private void UpdateCameraToFitRoom(RoomFirstDungeonGenerator.Room room)
    {
        // 1. 카메라 위치를 방의 중심으로 이동
        Vector3 targetPosition = new Vector3(room.center.x, room.center.y, transform.position.z);
        transform.position = targetPosition;

        // 2. 방의 가로, 세로 크기를 기반으로 카메라의 크기(줌)를 계산
        // 가로, 세로 중 더 긴 쪽을 기준으로 카메라 크기를 맞춰야 방 전체가 보임
        float roomWidth = room.Bounds.size.x + padding;
        float roomHeight = room.Bounds.size.y + padding;

        // 화면 비율을 고려해서 필요한 카메라 크기 계산
        float sizeForWidth = roomWidth * minimapCamera.pixelHeight / minimapCamera.pixelWidth * 0.5f;
        float sizeForHeight = roomHeight * 0.5f;

        // 둘 중 더 큰 값으로 카메라 크기를 설정해야 방이 잘리지 않아
        minimapCamera.orthographicSize = Mathf.Max(sizeForWidth, sizeForHeight);

        Debug.Log($"{room.type} 방으로 이동! 미니맵 업데이트 완료.");
    }
}