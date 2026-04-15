using Nytherion.Core.Interfaces;
using Nytherion.Core.Managers;
using Nytherion.GamePlay.Skills;
using UnityEngine;
using VContainer;

namespace Nytherion.GamePlay.Characters.Skill
{
    public class DroneSkill : SkillBase
    {
        [Header("Drone Visuals")]
        public GameObject droneVisual;

        [Header("Drone Settings")]
        [Tooltip("드론 유지 시간")]
        public float activeDuration = 10f;
        [Tooltip("드론 투사체 발사 간격")]
        public float attackInterval = 1f;
        [Tooltip("ObjectPoolManager에 등록된 투사체 태그")]
        public string projectilePoolTag = "Arrow";
        [Tooltip("투사체 발사 속도")]
        public float projectileSpeed = 10f;

        [Header("Orbit Settings")]
        public float orbitRadius = 1.5f;
        public float orbitSpeed = 90f;
        public float bobbingAmount = 0.2f;
        public float bobbingSpeed = 3f;

        private bool isActive = false;
        private float currentDurationTimer = 0f;
        private float autoAttackTimer = 0f;
        private float currentOrbitAngle = 0f;

        private ObjectPoolManager poolManager;

        [Inject]
        public void Construct(ObjectPoolManager poolManager)
        {
            this.poolManager = poolManager;
        }
        private void Start()
        {
            if (droneVisual != null) droneVisual.SetActive(false);
        }

        protected override void Activate()
        {
            if (isActive)
            {
                currentDurationTimer = 0f;
                return;
            }

            isActive = true;
            currentDurationTimer = 0f;
            autoAttackTimer = 0f; 

            currentOrbitAngle = 0f;
            transform.position = caster.position + new Vector3(orbitRadius, 0, 0);

            if (droneVisual != null) droneVisual.SetActive(true);
        }

        private void Update()
        {
            if (!isActive || caster == null) return;

            currentDurationTimer += Time.deltaTime;
            if (currentDurationTimer >= activeDuration)
            {
                DeactivateDrone();
                return;
            }

            MoveDrone();

            AutoAttackLogic();
        }

        private void MoveDrone()
        {
            currentOrbitAngle += Time.deltaTime * orbitSpeed;
            float rad = currentOrbitAngle * Mathf.Deg2Rad;
            float bobbingOffset = Mathf.Sin(Time.time * bobbingSpeed) * bobbingAmount;

            Vector3 targetPosition = caster.position + new Vector3(Mathf.Cos(rad) * orbitRadius, Mathf.Sin(rad) * orbitRadius + bobbingOffset, 0);

            transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * 5f);
        }

        private void AutoAttackLogic()
        {
            if (skillData == null) return;

            autoAttackTimer += Time.deltaTime;
            if (autoAttackTimer >= attackInterval)
            {
                Transform target = FindClosestEnemy();
                if (target != null)
                {
                    FireAtTarget(target);
                    autoAttackTimer = 0f;
                }
            }
        }

        private Transform FindClosestEnemy()
        {
            float range = skillData.range;

            Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, range);
            Transform closestTarget = null;
            float closestDistance = Mathf.Infinity;

            foreach (var hit in hits)
            {
                if (hit.CompareTag("Enemy") && hit.GetComponent<IDamageable>() != null)
                {
                    float distance = Vector2.Distance(transform.position, hit.transform.position);
                    if (distance < closestDistance)
                    {
                        closestDistance = distance;
                        closestTarget = hit.transform;
                    }
                }
            }
            return closestTarget;
        }

        private void FireAtTarget(Transform target)
        {
            Vector2 direction = (target.position - transform.position).normalized;

            GameObject projectile = poolManager.SpawnFromPool(projectilePoolTag, transform.position, Quaternion.identity);

            if (projectile != null)
            {
                float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                projectile.transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);

                if (projectile.TryGetComponent<Rigidbody2D>(out var rb))
                {
                    rb.velocity = direction * projectileSpeed;
                }

                if (projectile.TryGetComponent<Combat.PiercingEffect>(out var pierce))
                {
                    pierce.enabled = false;
                }
            }
        }

        private void DeactivateDrone()
        {
            isActive = false;
            if (droneVisual != null) droneVisual.SetActive(false);
        }
    }
}