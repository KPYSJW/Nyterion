using UnityEngine;
using Nytherion.Core.Interfaces;
using Nytherion.GamePlay.Combat;

namespace Nytherion.GamePlay.Combat
{
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(CollisionObject))]
    [RequireComponent(typeof(Collider2D))]
    public class PoisonProjectile : MonoBehaviour, IProjectile, IProjectileEffect
    {
        [Header("Poison Settings")]
        [SerializeField] private float poisonDamagePerSecond = 3f;
        [SerializeField] private float poisonDuration = 5f;

        [Header("Visual Settings")]
        [SerializeField] private float spriteRotationOffset = -90f; // 이미지가 위쪽(Up)을 향하고 있으므로 로컬 X축 정렬을 위한 회전 오프셋

        private Rigidbody2D rb;
        private CollisionObject collisionObj;
        private Collider2D myCollider;

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            collisionObj = GetComponent<CollisionObject>();
            myCollider = GetComponent<Collider2D>();
        }

        private void OnEnable()
        {
            if (myCollider != null)
            {
                myCollider.enabled = true;
            }
        }

        // IProjectile 구현: RangedWeapon이 발사 각도를 지정하고 속도를 인가할 때 호출
        public void SetSpeed(float speed)
        {
            if (rb != null)
            {
                rb.velocity = (Vector2)transform.right * speed;
            }

            // 이미지 회전 보정
            transform.rotation *= Quaternion.Euler(0, 0, spriteRotationOffset);
        }

        // IProjectileEffect 구현: CollisionObject가 충돌 시 OnHit를 호출
        public bool OnHit(Collider2D target)
        {
            if (target.CompareTag("Enemy"))
            {
                // 적 충돌 시 독 상태 이상 부여
                StatusEffectManager effectManager = target.GetComponent<StatusEffectManager>();
                if (effectManager == null)
                {
                    effectManager = target.gameObject.AddComponent<StatusEffectManager>();
                }

                effectManager.ApplyEffect(new PoisonEffect(poisonDamagePerSecond, poisonDuration));

                // 관통되도록 true 반환
                return true;
            }
            else if (target.CompareTag("Wall"))
            {
                // 벽 충돌 시 관통하지 않고 소멸(ReturnToPool)되도록 false 반환
                return false;
            }

            return true;
        }
    }
}
