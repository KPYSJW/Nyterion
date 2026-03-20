using Nytherion.GamePlay.Characters.Player;
using Nytherion.GamePlay.Dungeon;
using Unity.VisualScripting;
using UnityEngine;
using VContainer;

namespace Nytherion.GamePlay
{
    public class FollowCamera : MonoBehaviour
    {
        private Transform target;
        /*private bool isPlayerReady = false;
        private Vector3 lastPlayerPosition = Vector3.zero;
        [Header("Initialization")]
        [Tooltip("플레이어 준비 감지를 위한 시간 (초)")]
        public float playerReadyCheckTime = 1f;
        private float timeSinceLastPositionChange = 0f;*/

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
    //   private float timeSinceLastPositionChange = 0f; 
        private bool debugLogging = false;
        [Inject]
        public void Construct(PlayerController playerController)
        {
            if (playerController != null)
            {
                target = playerController.transform;
            }
        }

        private void Start()
        {
            transform.position = new Vector3(transform.position.x, transform.position.y, offset.z);

            if (target == null)
            {
                TryFindPlayerController();
            }

            if (target != null)
            {
                /*Vector3 playerPos = target.position;
                playerPos.z = 0f;
                target.position = playerPos;*/
                TeleportToPlayer();
            }
            else
            {
                Debug.LogError("[FollowCamera] PlayerController를 찾을 수 없습니다!");
            }

            EnsureSingleAudioListener();
        }

        private void TryFindPlayerController()
        {
            var playerController = FindObjectOfType<PlayerController>();
            if (playerController != null)
            {
                target = playerController.transform;
                return;
            }

            try
            {
                var lifetimeScope = FindObjectOfType<GameSceneLifetimeScope>();
                if (lifetimeScope != null && lifetimeScope.Container != null)
                {
                    playerController = lifetimeScope.Container.Resolve<PlayerController>();
                    if (playerController != null)
                    {
                        target = playerController.transform;
                        return;
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[FollowCamera] VContainer 지연 해결 실패: {e.Message}");
            }

            var playerGameObject = GameObject.FindWithTag("Player");
            if (playerGameObject != null)
            {
                playerController = playerGameObject.GetComponent<PlayerController>();
                if (playerController != null)
                {
                    target = playerController.transform;
                    return;
                }
            }

            Debug.LogError("[FollowCamera] 모든 방법으로 PlayerController를 찾지 못했습니다!");
        }
        
        private void EnsureSingleAudioListener()
        {
            AudioListener[] listeners = FindObjectsOfType<AudioListener>();
            
            if (listeners.Length > 1)
            {
                foreach (AudioListener listener in listeners)
                {
                    if (listener.gameObject != this.gameObject)
                    {
                        DestroyImmediate(listener);
                    }
                }
            }
            
            if (GetComponent<AudioListener>() == null)
            {
                gameObject.AddComponent<AudioListener>();
            }
        }


        private void LateUpdate()
        {
          /*  if (target == null) return;

            CheckPlayerReadyState();
            if (!isPlayerReady) return;*/
            if (target == null)
            {
                TryFindPlayerController();

                
                if (target == null)
                {
                    return;
                }

              
                TeleportToPlayer();
            }

            Vector3 targetPosition = new Vector3(
                target.position.x + offset.x,
                target.position.y + offset.y,
                offset.z
            );

            if (Mathf.Abs(target.position.z) > 0.01f)
            {
                Vector3 playerPos = target.position;
                playerPos.z = 0f;
                target.position = playerPos;
            }

            transform.position = Vector3.SmoothDamp(
                transform.position,
                targetPosition,
                ref velocity,
                1f / smoothSpeed
            );
        }

        /// <summary>
        /// 플레이어가 최종 위치에 안정적으로 자리잡았는지 확인합니다.
        /// </summary>
       /* private void CheckPlayerReadyState()
        {
            if (isPlayerReady) return;

            Vector3 currentPosition = target.position;
            
            if (Vector3.Distance(currentPosition, Vector3.zero) < 1f)
            {
                lastPlayerPosition = currentPosition;
                timeSinceLastPositionChange = 0f;
                return;
            }

            if (Vector3.Distance(currentPosition, lastPlayerPosition) > 0.1f)
            {
                lastPlayerPosition = currentPosition;
                timeSinceLastPositionChange = 0f;
            }
            else
            {
                timeSinceLastPositionChange += Time.deltaTime;
                
                if (timeSinceLastPositionChange >= playerReadyCheckTime)
                {
                    isPlayerReady = true;

                    Vector3 immediatePosition = new Vector3(
                        currentPosition.x + offset.x,
                        currentPosition.y + offset.y,
                        offset.z
                    );
                    transform.position = immediatePosition;
                }
            }
        }*/

        /// <summary>
        /// 포탈 이동 시 카메라를 즉시 플레이어 위치로 이동시킵니다.
        /// </summary>
        public void TeleportToPlayer()
        {
            if (target == null) return;

            Vector3 newPosition = new Vector3(
                target.position.x + offset.x,
                target.position.y + offset.y,
                offset.z
            );

            // 경계 제한 적용
            newPosition.x = Mathf.Clamp(newPosition.x, minBounds.x, maxBounds.x);
            newPosition.y = Mathf.Clamp(newPosition.y, minBounds.y, maxBounds.y);

            transform.position = newPosition;

            if (debugLogging)
            {
                Debug.Log($"[FollowCamera] 카메라가 플레이어 위치로 즉시 이동했습니다: {newPosition}");
            }
        }

        /// <summary>
        /// 맵 경계를 설정합니다 (던전 크기에 맞춰 동적 설정용)
        /// </summary>
        public void SetBounds(Vector2 min, Vector2 max)
        {
            minBounds = min;
            maxBounds = max;

            if (debugLogging)
            {
                Debug.Log($"[FollowCamera] 카메라 경계 설정: Min{minBounds}, Max{maxBounds}");
            }
        }

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
