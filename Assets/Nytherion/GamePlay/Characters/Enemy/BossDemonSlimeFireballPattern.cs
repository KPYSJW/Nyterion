using System.Collections.Generic;
using Nytherion.GamePlay.Combat;
using UnityEngine;

namespace Nytherion.GamePlay.Characters.Enemy
{
    [DisallowMultipleComponent]
    public sealed class BossDemonSlimeFireballPattern : MonoBehaviour, IBossDemonSlimePattern
    {
        [Header("Tracking Fireball Fan / 추적 화염구 탄막")]
        [SerializeField] private BossWitchProjectile projectilePrefab;
        [SerializeField, Min(1)] private int projectileCount = 30;
        [SerializeField, Min(0.01f)] private float minimumShotInterval = 0.04f;
        [SerializeField, Min(0.01f)] private float maximumShotInterval = 0.12f;
        [SerializeField, Range(0f, 180f)] private float spreadAngle = 70f;
        [SerializeField, Min(0.1f)] private float projectileSpeed = 7f;
        [SerializeField, Min(0.1f)] private float projectileScale = 1.5f;
        [Tooltip("크기 배율 1일 때의 판정 반경입니다. Projectile Scale과 함께 조절됩니다.")]
        [SerializeField, Min(0.01f)] private float projectileHitRadius = 0.22f;
        [SerializeField, Min(0.1f)] private float activationRange = 12f;
        [SerializeField, Min(0f)] private float damageMultiplier = 1f;
        [SerializeField] private Vector2 muzzleOffset = new Vector2(2.55f, 2.3f);
        [Header("Casting / 시전 시간")]
        [SerializeField, Min(0f)] private float preparationDuration = 0.6f;
        [SerializeField, Min(0f)] private float recoveryDuration = 0.55f;

        private enum Phase { None, Preparing, Firing, Recovering }
        private static readonly int CastState = Animator.StringToHash("Base Layer.FireballCast");
        private Phase phase;
        private BossDemonSlimeEnemy body;
        private BossDemonSlimeMovementBehavior movement;
        private StatusEffectManager statusEffects;
        private float preparationEndsAt, nextShotAt, recoveryEndsAt;
        private int shotsFired, requestedShots;
        private readonly List<BossWitchProjectile> projectiles = new List<BossWitchProjectile>();

        public BossDemonSlimePatternType PatternType => BossDemonSlimePatternType.FireballBarrage;

        private void CacheComponents()
        {
            if (body == null) body = GetComponent<BossDemonSlimeEnemy>();
            if (movement == null) movement = GetComponent<BossDemonSlimeMovementBehavior>();
            if (statusEffects == null) statusEffects = GetComponent<StatusEffectManager>();
        }

        public bool CanStart(EnemyAIController enemy)
        {
            CacheComponents();
            return isActiveAndEnabled && phase == Phase.None && body != null && !body.isDead &&
                body.CurrentForm == BossDemonSlimeEnemy.Form.Demon && movement != null &&
                projectilePrefab != null && enemy != null && enemy.CurrentTarget != null &&
                enemy.animator != null && enemy.animator.isActiveAndEnabled && enemy.animator.layerCount > 0 &&
                enemy.animator.HasState(0, CastState) &&
                Vector2.Distance(transform.position, enemy.CurrentTarget.position) <= Mathf.Max(0.1f, activationRange);
        }

        public void Begin(EnemyAIController enemy, BossDemonSlimeCombatBehavior combat)
        {
            CacheComponents();
            projectiles.RemoveAll(projectile => projectile == null);
            phase = Phase.Preparing;
            shotsFired = 0;
            requestedShots = Mathf.Max(1, projectileCount);
            preparationEndsAt = Time.time + Mathf.Max(0f, preparationDuration);
            // The combat controller owns the movement lock until this pattern's recovery has ended.
            movement.SetPhaseAnimationLocked(true);
            enemy.SetMovementAllowed(false);
            FaceTarget(enemy);
            enemy.PlayAnimation("FireballCast");
        }

        public bool UpdatePattern(EnemyAIController enemy)
        {
            if (phase == Phase.None || body == null || body.isDead || !body.isActiveAndEnabled ||
                enemy == null || enemy.CurrentTarget == null) return true;
            if (phase == Phase.Preparing || phase == Phase.Firing) FaceTarget(enemy);
            if (phase == Phase.Preparing && Time.time >= preparationEndsAt)
            {
                phase = Phase.Firing;
                nextShotAt = Time.time;
            }
            if (phase == Phase.Firing && Time.time >= nextShotAt)
            {
                FireOne(enemy);
                shotsFired++;
                if (shotsFired >= requestedShots)
                {
                    phase = Phase.Recovering;
                    recoveryEndsAt = Time.time + Mathf.Max(0f, recoveryDuration);
                    enemy.PlayAnimation("Idle");
                }
                else
                {
                    float low = Mathf.Max(0.01f, Mathf.Min(minimumShotInterval, maximumShotInterval));
                    float high = Mathf.Max(low, Mathf.Max(minimumShotInterval, maximumShotInterval));
                    // Never catch up overdue shots in a burst: at most one projectile per update.
                    nextShotAt = Time.time + Random.Range(low, high);
                }
            }
            return phase == Phase.Recovering && Time.time >= recoveryEndsAt;
        }

        private void FaceTarget(EnemyAIController enemy)
        {
            Vector2 aim = (Vector2)enemy.CurrentTarget.position - (Vector2)transform.position;
            if (aim.sqrMagnitude < 0.0001f) aim = movement.FacingRight ? Vector2.right : Vector2.left;
            movement.FaceAttackDirection(aim);
            enemy.SetForcedFacingDirection(aim.normalized);
        }

        private Vector3 MuzzlePosition()
        {
            float sign = movement.FacingRight ? 1f : -1f;
            return transform.position + new Vector3(muzzleOffset.x * sign, muzzleOffset.y, 0f);
        }

        private void FireOne(EnemyAIController enemy)
        {
            FaceTarget(enemy);
            Vector3 origin = MuzzlePosition();
            Vector2 aim = (Vector2)enemy.CurrentTarget.position - (Vector2)origin;
            if (aim.sqrMagnitude < 0.0001f) aim = movement.FacingRight ? Vector2.right : Vector2.left;
            float halfSpread = Mathf.Clamp(spreadAngle, 0f, 180f) * 0.5f;
            float angle = Mathf.Atan2(aim.y, aim.x) + Random.Range(-halfSpread, halfSpread) * Mathf.Deg2Rad;
            Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            float damage = (enemy.enemyData != null ? enemy.enemyData.damageAmount : 2f) * Mathf.Max(0f, damageMultiplier);
            if (statusEffects != null) damage *= statusEffects.GetOutgoingDamageMultiplier();
            BossWitchProjectile projectile = Instantiate(projectilePrefab, origin, Quaternion.identity);
            float scale = Mathf.Max(0.1f, projectileScale);
            projectile.transform.localScale = Vector3.one * scale;
            projectile.ConfigureFlight(projectileSpeed, Mathf.Max(0.01f, projectileHitRadius) * scale);
            projectile.Initialize(body, direction, Mathf.Max(0f, damage));
            projectiles.Add(projectile);
        }

        public void OnAnimationHit(EnemyAIController enemy) { }
        public void End(EnemyAIController enemy, bool completed)
        {
            phase = Phase.None;
            if (!completed) ClearProjectiles();
        }
        private void ClearProjectiles()
        {
            foreach (BossWitchProjectile projectile in projectiles)
                if (projectile != null) Destroy(projectile.gameObject);
            projectiles.Clear();
        }
        public void ResetForReuse()
        {
            phase = Phase.None;
            shotsFired = requestedShots = 0;
            ClearProjectiles();
        }
        private void OnDisable() => ResetForReuse();
    }
}
