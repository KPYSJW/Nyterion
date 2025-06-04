using UnityEngine;

namespace Nytherion.GamePlay
{
    /// <summary>
    /// 플레이어를 부드럽게 따라다니는 카메라 컨트롤러
    /// </summary>
    public class FollowCamera : MonoBehaviour
    {
        [Header("Target")]
        [Tooltip("따라갈 타겟 (플레이어) Transform")]
        public Transform target;

        [Header("Follow Settings")]
        [Tooltip("카메라가 타겟을 따라가는 속도 (높을수록 빠르게 따라감)")]
        [Range(1f, 50f)]
        public float smoothSpeed = 15f;

        [Tooltip("카메라와 타겟 사이의 오프셋 (Z축은 -10으로 고정)")]
        public Vector3 offset = new Vector3(0f, 0f, -10f);

        [Header("Boundary")]
        [Tooltip("카메라가 이동할 수 있는 최소 위치")]
        public Vector2 minBounds = new Vector2(-100f, -100f);
        
        [Tooltip("카메라가 이동할 수 있는 최대 위치")]
        public Vector2 maxBounds = new Vector2(100f, 100f);

        [Header("Debug")]
        [Tooltip("즉시 이동 모드 (테스트용)")]
        public bool useSmoothMovement = true;

        private Vector3 velocity = Vector3.zero;

        private void Start()
        {
            // 카메라 Z 위치 강제 고정
            transform.position = new Vector3(transform.position.x, transform.position.y, offset.z);

            if (target == null)
            {
                // 플레이어 태그를 가진 객체를 자동으로 찾아 타겟으로 설정
                GameObject player = GameObject.FindGameObjectWithTag("Player");
                if (player != null)
                {
                    target = player.transform;
                    // 플레이어 Z 위치 강제 고정
                    Vector3 playerPos = target.position;
                    playerPos.z = 0f;
                    target.position = playerPos;
                }
                else
                {
                    Debug.LogError("FollowCamera: Player not found! Please assign a target or tag a GameObject as 'Player'.");
                }
            }
        }

        private void LateUpdate()
        {
            if (target == null) return;

            // 타겟 위치 계산 (Z축은 항상 0으로 고정)
            Vector3 targetPosition = new Vector3(
                target.position.x + offset.x,
                target.position.y + offset.y,
                offset.z // Z축은 항상 고정
            );

            // 플레이어 Z 위치 강제 고정 (혹시 모를 문제 방지)
            if (Mathf.Abs(target.position.z) > 0.01f)
            {
                Vector3 playerPos = target.position;
                playerPos.z = 0f;
                target.position = playerPos;
            }

            if (useSmoothMovement)
            {
                // 부드러운 이동 (SmoothDamp 사용)
                transform.position = Vector3.SmoothDamp(
                    transform.position,
                    targetPosition,
                    ref velocity,
                    1f / smoothSpeed
                );
            }
            else
            {
                // 즉시 이동 (테스트용)
                transform.position = new Vector3(
                    Mathf.Clamp(targetPosition.x, minBounds.x, maxBounds.x),
                    Mathf.Clamp(targetPosition.y, minBounds.y, maxBounds.y),
                    offset.z
                );
            }

            // 경계 제한 (보험용)
            Vector3 clampedPosition = transform.position;
            clampedPosition.x = Mathf.Clamp(clampedPosition.x, minBounds.x, maxBounds.x);
            clampedPosition.y = Mathf.Clamp(clampedPosition.y, minBounds.y, maxBounds.y);
            clampedPosition.z = offset.z; // Z축은 항상 고정
            transform.position = clampedPosition;
        }

        /// <summary>
        /// 카메라의 경계를 설정합니다.
        /// </summary>
        public void SetBounds(Vector2 min, Vector2 max)
        {
            minBounds = min;
            maxBounds = max;
        }

        // 디버그용 Gizmo 그리기
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.green;
            Vector3 size = new Vector3(
                maxBounds.x - minBounds.x,
                maxBounds.y - minBounds.y,
                1f
            );
            Vector3 center = new Vector3(
                (minBounds.x + maxBounds.x) * 0.5f,
                (minBounds.y + maxBounds.y) * 0.5f,
                0f
            );
            Gizmos.DrawWireCube(center, size);
        }
    }
}
