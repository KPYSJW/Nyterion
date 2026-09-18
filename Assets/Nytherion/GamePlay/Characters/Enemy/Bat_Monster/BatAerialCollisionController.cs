using UnityEngine;

namespace Nytherion.GamePlay.Characters.Enemy
{
    [DisallowMultipleComponent]
    public sealed class BatAerialCollisionController : MonoBehaviour
    {
        [Header("Collision Roles")]
        [Tooltip("공중에서도 플레이어 공격을 받을 수 있게 항상 유지되는 트리거입니다.")]
        [SerializeField] private Collider2D hurtbox;
        [Tooltip("착지한 공격 구간에만 플레이어와 물리 충돌하는 콜라이더입니다.")]
        [SerializeField] private Collider2D landingBodyCollider;

        [Header("Attack Preview")]
        [SerializeField] private GameObject attackRangePreview;

        [Header("Landing Projectiles")]
        [SerializeField] private GameObject landingProjectilePrefab;
        [SerializeField, Min(1)] private int minLandingProjectileCount = 2;
        [SerializeField, Min(1)] private int maxLandingProjectileCount = 4;
        [SerializeField, Min(0f)] private float minLandingTargetDistance = 1.5f;
        [SerializeField, Min(0f)] private float maxLandingTargetDistance = 4f;
        [SerializeField, Min(0f)] private float landingProjectileSpeed = 5f;

        private EnemyBase enemyBase;
        private bool firedLandingProjectiles;

        public bool IsLanded { get; private set; }

        private void Awake()
        {
            enemyBase = GetComponent<EnemyBase>();
            SetAirborne();
        }

        private void OnEnable()
        {
            SetAirborne();
        }

        private void OnDisable()
        {
            HideAttackPreview();
        }

        public void BatShowAttackPreview()
        {
            firedLandingProjectiles = false;
            if (attackRangePreview != null)
            {
                attackRangePreview.SetActive(true);
            }
        }

        public void BatLand()
        {
            HideAttackPreview();
            SetLanded();
            FireLandingProjectiles();
        }

        public void BatTakeOff()
        {
            SetAirborne();
        }

        public void SetLanded()
        {
            IsLanded = true;

            if (hurtbox != null)
            {
                hurtbox.isTrigger = true;
            }

            if (landingBodyCollider != null)
            {
                landingBodyCollider.enabled = true;
            }
        }

        public void SetAirborne()
        {
            IsLanded = false;
            firedLandingProjectiles = false;
            HideAttackPreview();

            if (hurtbox != null)
            {
                hurtbox.isTrigger = true;
            }

            if (landingBodyCollider != null)
            {
                landingBodyCollider.enabled = false;
            }
        }

        private void FireLandingProjectiles()
        {
            if (firedLandingProjectiles || landingProjectilePrefab == null ||
                attackRangePreview == null)
                return;

            firedLandingProjectiles = true;
            Vector2 origin = attackRangePreview.transform.position;
            float damage = enemyBase != null && enemyBase.enemyData != null
                ? enemyBase.enemyData.damageAmount
                : 0f;
            int minimum = Mathf.Max(1, minLandingProjectileCount);
            int maximum = Mathf.Max(minimum, maxLandingProjectileCount);
            int projectileCount = Random.Range(minimum, maximum + 1);
            float minDistance = Mathf.Max(0f, minLandingTargetDistance);
            float maxDistance = Mathf.Max(minDistance, maxLandingTargetDistance);

            for (int i = 0; i < projectileCount; i++)
            {
                float angle = Random.Range(0f, 360f);
                Vector2 direction = new Vector2(
                    Mathf.Cos(angle * Mathf.Deg2Rad),
                    Mathf.Sin(angle * Mathf.Deg2Rad));
                Vector2 target = origin + direction * Random.Range(minDistance, maxDistance);
                GameObject projectile = Instantiate(
                    landingProjectilePrefab,
                    origin,
                    Quaternion.AngleAxis(angle, Vector3.forward));

                if (projectile.TryGetComponent<BagEnemyProjectile>(out var bagProjectile))
                {
                    bagProjectile.Initialize(damage, target);
                }

                if (projectile.TryGetComponent<Rigidbody2D>(out var projectileBody))
                {
                    projectileBody.velocity = direction * landingProjectileSpeed;
                }
            }
        }

        private void HideAttackPreview()
        {
            if (attackRangePreview != null)
            {
                attackRangePreview.SetActive(false);
            }
        }
    }
}
