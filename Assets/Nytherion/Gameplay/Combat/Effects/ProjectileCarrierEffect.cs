using UnityEngine;
using Nytherion.Core.Managers;

namespace Nytherion.GamePlay.Combat
{
    public class ProjectileCarrierEffect : MonoBehaviour
    {
        [Header("Sub-Projectile Settings")]
        public string subProjectileTag = "Arrow";
        [Tooltip("파편 발사 간격 (초)")]
        public float fireInterval = 0.2f;
        [Tooltip("파편의 속도")]
        public float subProjectileSpeed = 6f;
        public float subDamageMultiplier = 0.3f;

        [Header("Firing Mode")]
        [Tooltip("True: 본체 양옆으로 발사 False: 무작위 방향으로 발사")]
        public bool fireSides = true;

        private float fireTimer = 0f;
        private CollisionObject myCol;
        private Rigidbody2D myRb;

        private void Awake()
        {
            myCol = GetComponent<CollisionObject>();
            myRb = GetComponent<Rigidbody2D>();
        }

        private void OnEnable()
        {
            fireTimer = 0f;
        }

        private void Update()
        {
            if (myRb == null || myCol == null) return;

            if (myRb.velocity.sqrMagnitude < 0.1f) return;

            fireTimer += Time.deltaTime;

            if (fireTimer >= fireInterval)
            {
                if (fireSides)
                {
                    FireToSides();
                }
                else
                {
                    FireRandomly();
                }
                fireTimer = 0f;
            }
        }

        // 본체 진행 방향 양 옆으로 발사
        private void FireToSides()
        {
            Vector2 currentDir = myRb.velocity.normalized;
            Vector2 sideDir1 = transform.up;
            Vector2 sideDir2 = -transform.up;

            SpawnSubProjectile(sideDir1);
            SpawnSubProjectile(sideDir2);
        }

        // 완전히 무작위 방향으로 발사
        private void FireRandomly()
        {
            Vector2 randomDir = Random.insideUnitCircle.normalized;
            SpawnSubProjectile(randomDir);
        }

        private void SpawnSubProjectile(Vector2 direction)
        {
            GameObject subProj = ObjectPoolManager.Instance.SpawnFromPool(subProjectileTag, transform.position, Quaternion.identity);

            if (subProj != null)
            {
                float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                subProj.transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);

                if (subProj.TryGetComponent<Rigidbody2D>(out var rb))
                {
                    rb.velocity = direction * subProjectileSpeed;
                }

                if (subProj.TryGetComponent<CollisionObject>(out var fragCol))
                {
                    fragCol.damage = myCol.damage * subDamageMultiplier;

                    if (subProj.TryGetComponent<ProjectileCarrierEffect>(out var otherCarrier))
                    {
                        otherCarrier.enabled = false;
                    }
                }
            }
        }
    }
}