using UnityEngine;
using Cinemachine; 

namespace Nytherion.GamePlay
{
    public class CameraManager : MonoBehaviour
    {
        [Header("Cinemachine Settings")]
        [Tooltip("씬에 있는 시네머신 가상 카메라를 연결해주세요.")]
        public CinemachineVirtualCamera virtualCamera; 

        private CinemachineConfiner confiner;

        private void Awake()
        {
            if (virtualCamera != null)
            {
                confiner = virtualCamera.GetComponent<CinemachineConfiner>();
            }
        }

        /// <summary>
        /// 던전 맵이 생성된 직후, 바닥(또는 맵 전체) 콜라이더를 전달하여 카메라를 가둡니다.
        /// </summary>
        /// <param name="boundingShape">PolygonCollider2D 또는 CompositeCollider2D</param>
        public void SetCameraBounds(Collider2D boundingShape)
        {
            if (confiner != null && boundingShape != null)
            {
                if (virtualCamera.Follow != null)
                {
                    Vector3 targetPos = virtualCamera.Follow.position;
                    targetPos.z = virtualCamera.transform.position.z;
                    virtualCamera.transform.position = targetPos;
                }

                confiner.m_BoundingShape2D = boundingShape;
                confiner.InvalidatePathCache();

                virtualCamera.PreviousStateIsValid = false;

                Debug.Log(" [CameraManager] 시네머신 카메라 강제 이동 및 경계 설정 완료!");
            }
        }
    }
}