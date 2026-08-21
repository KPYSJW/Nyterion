using UnityEngine;
using Nytherion.Core.Interfaces;

namespace Nytherion.GamePlay.Combat
{
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(CollisionObject))]
    [RequireComponent(typeof(Collider2D))]
    public class FireWaveProj : MonoBehaviour, IProj, IProjModifier
    {
        [Header("Projectile Settings")]
        [SerializeField] private bool pierceEnemies = false;
        [SerializeField] private float spriteRotationOffset = 0f;

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

        // IProj 구현: RangedWeapon 등에서 속도를 인가할 때 호출
        public void SetSpeed(float speed)
        {
            if (rb != null)
            {
                rb.velocity = (Vector2)transform.right * speed;
            }

            if (spriteRotationOffset != 0f)
            {
                transform.rotation *= Quaternion.Euler(0f, 0f, spriteRotationOffset);
            }
        }

        // IProjModifier 구현: CollisionObject 충돌 시 호출
        public bool OnHit(Collider2D target)
        {
            if (target.CompareTag("Enemy"))
            {
                // 기본적으로 관통 여부 필드 리턴 (기본값 false이므로 충돌 즉시 소멸)
                // 유물에 의해 추가된 PiercingModifier가 있을 시, 그 컴포넌트가 true를 리턴하므로 이 값과 무관하게 관통 가능
                return pierceEnemies;
            }
            else if (target.CompareTag("Wall"))
            {
                // 벽 충돌 시 즉시 소멸
                return false;
            }

            return true;
        }
    }
}
