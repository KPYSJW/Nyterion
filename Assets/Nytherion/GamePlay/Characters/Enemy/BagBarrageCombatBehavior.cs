using System.Collections;
using UnityEngine;

namespace Nytherion.GamePlay.Characters.Enemy
{
    [DisallowMultipleComponent]
    public sealed class BagBarrageCombatBehavior : MonoBehaviour, IEnemyCombatBehavior
    {
        private enum BarragePhase
        {
            None,
            Starting,
            Looping,
            Cooldown
        }

        [Header("Attack Settings")]
        [SerializeField, Min(0f)] private float attackRange = 8f;
        [SerializeField, Min(0f)] private float attackCooldown = 3f;
        [SerializeField, Min(1)] private int barrageCount = 5;
        [SerializeField, Min(1)] private int animationLoopsPerBarrage =20;

        [Header("Projectiles Per Loop")]
        [SerializeField, Min(1)] private int minProjectileCount = 2;
        [SerializeField, Min(1)] private int maxProjectileCount = 5;

        [Header("Random Target")]
        [SerializeField, Min(0f)] private float minTargetDistance = 2.5f;
        [SerializeField, Min(0f)] private float maxTargetDistance = 7f;

        [Header("Projectile")]
        [SerializeField] private GameObject projectilePrefab;
        [SerializeField] private GameObject firingEffectPrefab;
        [SerializeField, Min(0f)] private float projectileDelayAfterEffect = 0.16666667f;
        [SerializeField, Min(0f)] private float projectileSpeed = 5f;
        [SerializeField] private Transform firePoint;

        private EnemyAIController activeEnemy;
        private BarragePhase phase;
        private int completedAnimationLoops;
        private float lastBarrageEndTime = -999f;

        public bool ShouldEnterAttackState(EnemyAIController enemy)
        {
            if (enemy == null || enemy.player == null)
                return false;

            float distanceSquared =
                ((Vector2)enemy.transform.position - (Vector2)enemy.player.position).sqrMagnitude;
            return distanceSquared <= attackRange * attackRange &&
                   Time.time - lastBarrageEndTime >= attackCooldown;
        }

        public void EnterAttackState(EnemyAIController enemy)
        {
            activeEnemy = enemy;
            completedAnimationLoops = 0;
            phase = BarragePhase.Starting;
            enemy.StopMovement();
            enemy.PlayAnimation("Attack_start");
        }

        public void UpdateAttackState(EnemyAIController enemy)
        {
            enemy.StopMovement();

            switch (phase)
            {
                case BarragePhase.Starting:
                    if (enemy.IsAnimationFinished("Attack_start"))
                    {
                        phase = BarragePhase.Looping;
                        PlayAttackLoopFromStart(enemy);
                    }
                    break;

                case BarragePhase.Looping:
                    if (!enemy.IsAnimationFinished("Attack"))
                        break;

                    completedAnimationLoops++;
                    int totalAnimationLoops =
                        barrageCount * animationLoopsPerBarrage;
                    if (completedAnimationLoops < totalAnimationLoops)
                    {
                        PlayAttackLoopFromStart(enemy);
                    }
                    else
                    {
                        lastBarrageEndTime = Time.time;
                        phase = BarragePhase.Cooldown;
                        enemy.PlayAnimation("Idle");
                    }
                    break;

                case BarragePhase.Cooldown:
                    if (Time.time - lastBarrageEndTime >= attackCooldown)
                    {
                        phase = BarragePhase.None;
                        enemy.TransitionToState(enemy.chaseState);
                    }
                    break;
            }
        }

        public void ExitAttackState(EnemyAIController enemy)
        {
            activeEnemy = null;
            if (phase != BarragePhase.Cooldown)
                phase = BarragePhase.None;
        }

        public void ResetForReuse()
        {
            activeEnemy = null;
            phase = BarragePhase.None;
            completedAnimationLoops = 0;
            lastBarrageEndTime = -999f;
        }

        public void FireBarrage()
        {
            if (phase != BarragePhase.Looping || activeEnemy == null ||
                projectilePrefab == null || firePoint == null)
                return;

            int currentAnimationLoop = completedAnimationLoops + 1;
            if (currentAnimationLoop % animationLoopsPerBarrage != 0)
                return;

            int minimum = Mathf.Max(1, minProjectileCount);
            int maximum = Mathf.Max(minimum, maxProjectileCount);
            int projectileCount = Random.Range(minimum, maximum + 1);

            if (firingEffectPrefab != null)
            {
                Instantiate(firingEffectPrefab, firePoint.position, Quaternion.identity);
            }

            float damage = activeEnemy.enemyData != null
                ? activeEnemy.enemyData.damageAmount
                : 0f;

            if (projectileDelayAfterEffect > 0f)
            {
                StartCoroutine(SpawnBarrageAfterDelay(projectileCount, damage));
                return;
            }

            SpawnProjectiles(projectileCount, damage);
        }

        private IEnumerator SpawnBarrageAfterDelay(int projectileCount, float damage)
        {
            yield return new WaitForSeconds(projectileDelayAfterEffect);
            SpawnProjectiles(projectileCount, damage);
        }

        private void SpawnProjectiles(int projectileCount, float damage)
        {
            if (projectilePrefab == null || firePoint == null)
                return;

            for (int i = 0; i < projectileCount; i++)
            {
                SpawnProjectile(GetRandomTarget(), damage);
            }
        }

        private Vector2 GetRandomTarget()
        {
            float minimum = Mathf.Max(0f, minTargetDistance);
            float maximum = Mathf.Max(minimum, maxTargetDistance);
            Vector2 direction = Random.insideUnitCircle.normalized;
            if (direction.sqrMagnitude <= 0.001f)
                direction = Vector2.right;

            return (Vector2)firePoint.position +
                   direction * Random.Range(minimum, maximum);
        }

        private void SpawnProjectile(Vector2 target, float damage)
        {
            Vector2 direction = (target - (Vector2)firePoint.position).normalized;
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            GameObject projectile = Instantiate(
                projectilePrefab,
                firePoint.position,
                Quaternion.AngleAxis(angle, Vector3.forward));

            if (projectile.TryGetComponent<BagEnemyProjectile>(out var bagProjectile))
            {
                bagProjectile.Initialize(damage, target);
            }
            else if (projectile.TryGetComponent<MushroomEnemyProjectile>(out var ballisticProjectile))
            {
                ballisticProjectile.Initialize(damage, target);
            }

            if (projectile.TryGetComponent<Rigidbody2D>(out var projectileBody))
            {
                projectileBody.velocity = direction * projectileSpeed;
            }
        }

        private static void PlayAttackLoopFromStart(EnemyAIController enemy)
        {
            if (enemy == null || enemy.animator == null ||
                !enemy.animator.isActiveAndEnabled)
                return;

            int stateHash = Animator.StringToHash("Attack");
            if (enemy.animator.HasState(0, stateHash))
            {
                enemy.animator.Play(stateHash, 0, 0f);
                return;
            }

            int fullPathHash = Animator.StringToHash("Base Layer.Attack");
            if (enemy.animator.HasState(0, fullPathHash))
            {
                enemy.animator.Play(fullPathHash, 0, 0f);
            }
        }

        private void OnValidate()
        {
            attackRange = Mathf.Max(0f, attackRange);
            attackCooldown = Mathf.Max(0f, attackCooldown);
            barrageCount = Mathf.Max(1, barrageCount);
            animationLoopsPerBarrage = Mathf.Max(1, animationLoopsPerBarrage);
            minProjectileCount = Mathf.Max(1, minProjectileCount);
            maxProjectileCount = Mathf.Max(minProjectileCount, maxProjectileCount);
            minTargetDistance = Mathf.Max(0f, minTargetDistance);
            maxTargetDistance = Mathf.Max(minTargetDistance, maxTargetDistance);
            projectileDelayAfterEffect = Mathf.Max(0f, projectileDelayAfterEffect);
            projectileSpeed = Mathf.Max(0f, projectileSpeed);
        }
    }
}
