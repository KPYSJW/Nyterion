using UnityEngine;
using Nytherion.Core.Interfaces;

namespace Nytherion.GamePlay.Skills
{
    [RequireComponent(typeof(Rigidbody2D))]
    public class HomingProjectile : MonoBehaviour
    {
        [Header("Movement")]
        public float maxSpeed = 12f;      
        public float acceleration = 15f;  
        public float rotateSpeed = 400f;  

        [Header("Tracking")]
        public float trackingRadius = 10f;
        public LayerMask enemyLayer;

        private Rigidbody2D rb;
        private Transform target;
        private float currentSpeed;

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
        }

        private void Start()
        {
            currentSpeed = rb.velocity.magnitude;
            FindClosestEnemy();
        }

        private void FixedUpdate()
        {
            if (target != null && target.gameObject.activeInHierarchy)
            {
                // 타겟 방향 계산
                Vector2 direction = (Vector2)target.position - rb.position;
                direction.Normalize();

                // 타겟 방향으로 회전
                float rotateAmount = Vector3.Cross(direction, transform.right).z;
                rb.angularVelocity = -rotateAmount * rotateSpeed;
            }
            else
            {
                rb.angularVelocity = 0f; // 타겟이 없으면 직진
            }

            currentSpeed = Mathf.MoveTowards(currentSpeed, maxSpeed, acceleration * Time.fixedDeltaTime);

            // 현재 바라보는 방향으로 날리기
            rb.velocity = transform.right * currentSpeed;
        }

        private void FindClosestEnemy()
        {
            Collider2D[] hitColliders = Physics2D.OverlapCircleAll(transform.position, trackingRadius, enemyLayer);
            float closestDistance = Mathf.Infinity;

            foreach (var hit in hitColliders)
            {
                if (hit.GetComponent<IDamageable>() != null)
                {
                    float distance = Vector2.Distance(transform.position, hit.transform.position);
                    if (distance < closestDistance)
                    {
                        closestDistance = distance;
                        target = hit.transform;
                    }
                }
            }
        }
    }
}