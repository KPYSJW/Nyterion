using UnityEngine;
using Nytherion.Core.Interfaces;

namespace Nytherion.GamePlay.Combat.Weapons
{
    public class MeteorProjectile : MonoBehaviour
    {
        [Header("Settings")]
        public float fallSpeed = 20f;
        public float explosionRadius = 2.5f;

        [Header("Indicator Settings")]
        [Tooltip("타격 범위를 표시해 줄 붉은색 반투명 원 프리팹")]
        public GameObject indicatorPrefab;
        [Tooltip("폭발 비주얼 중심과 판정 범위를 맞추기 위한 Y축 보정 오프셋")]
        public float yOffset = 0.0f;

        private Vector3 targetPosition;
        private bool isFalling = false;
        private GameObject activeIndicator;

        private Animator animator;
        private CollisionObject col;
        private CircleCollider2D circleCollider;
        private static readonly Collider2D[] meteorBuffer = new Collider2D[20];

        private void Awake()
        {
            animator = GetComponent<Animator>();
            col = GetComponent<CollisionObject>();
            circleCollider = GetComponent<CircleCollider2D>();
        }

        public void Initialize(Vector3 targetPos)
        {
            targetPosition = targetPos;
            isFalling = true;

            // 콜라이더 크기(반지름 * 로컬 스케일)를 기반으로 폭발 반경 동적 갱신
            if (circleCollider != null)
            {
                explosionRadius = circleCollider.radius * transform.localScale.x;
            }

            // 타격 범위 예고 인디케이터 생성
            if (indicatorPrefab != null)
            {
                // 인디케이터는 마우스가 조준한 원래 바닥 위치에 정확히 생성
                Vector3 indicatorPos = targetPosition;
                activeIndicator = Instantiate(indicatorPrefab, indicatorPos, Quaternion.identity);
                // 폭발 반경(explosionRadius)에 맞게 지름(Radius * 2) 크기로 원형 스케일 설정
                activeIndicator.transform.localScale = new Vector3(explosionRadius * 2f, explosionRadius * 2f, 1f);
            }
        }

        private void Update()
        {
            if (!isFalling) return;

            transform.position = Vector3.MoveTowards(transform.position, targetPosition, fallSpeed * Time.deltaTime);

            if ((transform.position - targetPosition).sqrMagnitude <= 0.01f) // 0.1 * 0.1 = 0.01
            {
                TriggerExplosion();
            }
        }

        private void TriggerExplosion()
        {
            isFalling = false; 

            RemoveIndicator();

            // 폭발 비주얼이 피벗 때문에 위로 뜨는 것을 보정하기 위해, 오브젝트 위치를 Y축 아래로 이동
            transform.position = targetPosition - new Vector3(0f, yOffset, 0f);

            if (animator != null)
            {
                animator.SetTrigger("Explode");
            }
            else
            {
                ApplyDamage();
                DisableProjectile();
            }
        }

        public void ApplyDamage()
        {
            float finalDamage = col != null ? col.damage : 10f;

            // 실제 타격 판정 중심점도 폭발 비주얼 중심(yOffset 반영)으로 일치화
            Vector3 explosionCenter = transform.position + new Vector3(0f, yOffset, 0f);
            int hitCount = Physics2D.OverlapCircleNonAlloc(explosionCenter, explosionRadius, meteorBuffer);

            for (int i = 0; i < hitCount; i++)
            {
                Collider2D hit = meteorBuffer[i];
                if (hit.CompareTag("Enemy"))
                {
                    if (hit.TryGetComponent<IDamageable>(out var damageable))
                    {
                        damageable.TakeDamage(finalDamage);
                    }
                }
            }
        }
        public void DisableProjectile()
        {
            RemoveIndicator();
            gameObject.SetActive(false);
        }

        private void OnDisable()
        {
            RemoveIndicator();
        }

        private void RemoveIndicator()
        {
            if (activeIndicator != null)
            {
                Destroy(activeIndicator);
                activeIndicator = null;
            }
        }
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            // 낙하 중일 때는 원래 목표 지점을 표시하고, 폭발 중일 때는 오프셋 보정된 중심을 표시
            Vector3 explosionCenter = isFalling ? targetPosition : transform.position + new Vector3(0f, yOffset, 0f);
            Gizmos.DrawWireSphere(explosionCenter, explosionRadius);
        }
    }
}