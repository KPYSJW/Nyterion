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
        private static readonly Collider2D[] homingBuffer = new Collider2D[10];

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
                // Ÿ  
                Vector2 direction = (Vector2)target.position - rb.position;
                direction.Normalize();

                // Ÿ  ȸ
                float rotateAmount = Vector3.Cross(direction, transform.right).z;
                rb.angularVelocity = -rotateAmount * rotateSpeed;
            }
            else
            {
                rb.angularVelocity = 0f; // Ÿ  
            }

            currentSpeed = Mathf.MoveTowards(currentSpeed, maxSpeed, acceleration * Time.fixedDeltaTime);

            //  ٶ󺸴  
            rb.velocity = transform.right * currentSpeed;
        }

        private void FindClosestEnemy()
        {
            int hitCount = Physics2D.OverlapCircleNonAlloc(transform.position, trackingRadius, homingBuffer, enemyLayer);
            float closestDistanceSqr = Mathf.Infinity;

            for (int i = 0; i < hitCount; i++)
            {
                Collider2D hit = homingBuffer[i];
                if (hit.GetComponent<IDamageable>() != null)
                {
                    float distanceSqr = (transform.position - hit.transform.position).sqrMagnitude;
                    if (distanceSqr < closestDistanceSqr)
                    {
                        closestDistanceSqr = distanceSqr;
                        target = hit.transform;
                    }
                }
            }
        }
    }
}