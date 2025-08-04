using Nytherion.GamePlay.Characters.Player;
using Nytherion.GamePlay.Dungeon;
using Unity.VisualScripting;
using UnityEngine;
using Zenject;

namespace Nytherion.GamePlay
{
    public class FollowCamera : MonoBehaviour
    {
        private Transform target;

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

        [Inject]
        public void Construct(PlayerController playerController)
        {
            
            target = playerController.transform;
        }

        private void Start()
        {
            transform.position = new Vector3(transform.position.x, transform.position.y, offset.z);

            if (target != null)
            {
                Vector3 playerPos = target.position;
                playerPos.z = 0f;
                target.position = playerPos;
            }
            
            EnsureSingleAudioListener();
        }
        
        private void EnsureSingleAudioListener()
        {
            AudioListener[] listeners = FindObjectsOfType<AudioListener>();
            
            if (listeners.Length > 1)
            {
                Debug.LogWarning($"발견된 Audio Listener 수: {listeners.Length}");
                
                foreach (AudioListener listener in listeners)
                {
                    if (listener.gameObject != this.gameObject)
                    {
                        Debug.Log($"Audio Listener 제거: {listener.gameObject.name}");
                        DestroyImmediate(listener);
                    }
                }
            }
            
            if (GetComponent<AudioListener>() == null)
            {
                gameObject.AddComponent<AudioListener>();
                Debug.Log("FollowCamera에 Audio Listener 추가");
            }
        }

        /* private void LateUpdate()
         {
             if (target == null) return;

            Debug.Log(target.transform.position.x);
             Debug.Log(target.transform.position.y);
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

             if (useSmoothMovement)
             {
                 transform.position = Vector3.SmoothDamp(
                     transform.position,
                     targetPosition,
                     ref velocity,
                     1f / smoothSpeed
                 );
             }
             else
             {
                 transform.position = new Vector3(
                     Mathf.Clamp(targetPosition.x, minBounds.x, maxBounds.x),
                     Mathf.Clamp(targetPosition.y, minBounds.y, maxBounds.y),
                     offset.z
                 );
             }

             Vector3 clampedPosition = transform.position;
             clampedPosition.x = Mathf.Clamp(clampedPosition.x, minBounds.x, maxBounds.x);
             clampedPosition.y = Mathf.Clamp(clampedPosition.y, minBounds.y, maxBounds.y);
             clampedPosition.z = offset.z; 
             transform.position = clampedPosition;
         }*/

        private void LateUpdate()
        {
            if (target == null) return;

         
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

        public void SetBounds(Vector2 min, Vector2 max)
        {
            minBounds = min;
            maxBounds = max;
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
