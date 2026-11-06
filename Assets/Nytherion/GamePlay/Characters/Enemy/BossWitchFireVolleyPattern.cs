using Nytherion.GamePlay.Combat;
using UnityEngine;

namespace Nytherion.GamePlay.Characters.Enemy
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EnemyAIController), typeof(BossWitchMovementBehavior))]
    public sealed class BossWitchFireVolleyPattern : MonoBehaviour, IBossWitchPattern
    {
        public BossWitchPatternType PatternType => BossWitchPatternType.FireballVolley;

        [Header("Fireball Fan")]
        [SerializeField] private BossWitchProjectile projectilePrefab;
        [SerializeField, Min(1)] private int projectilesPerVolley = 5;
        [SerializeField, Min(1)] private int volleyCount = 5;
        [SerializeField, Min(0.05f)] private float volleyInterval = 0.4f;
        [SerializeField, Range(0f, 360f)] private float spreadAngle = 60f;
        [SerializeField, Min(0.1f)] private float activationRange = 10f;
        [SerializeField, Min(0f)] private float damageMultiplier = 1f;
        [SerializeField] private Vector2 muzzleOffset = new Vector2(0.65f, 0.15f);

        [Header("Preparation / Recovery")]
        [SerializeField, Min(0f)] private float preparationDuration = 0.8f;
        [SerializeField, Min(0f)] private float recoveryDuration = 1f;
        [SerializeField] private bool reverseCastingFacing;
        [SerializeField] private SpriteRenderer chargeCue;

        private enum Stage { Idle, Preparation, Firing, Recovery, Finished }
        private Stage stage;
        private float stageStartedAt;
        private readonly BossWitchFireVolleySchedule schedule = new BossWitchFireVolleySchedule();
        private BossWitchMovementBehavior movement;
        private EnemyBase enemyBody;
        private StatusEffectManager statusEffects;
        private EnemyAIController activeEnemy;
        private static readonly int PrepareState = Animator.StringToHash("Base Layer.FireVolleyPrepare");
        private static readonly int CastingState = Animator.StringToHash("Base Layer.FireVolleyCasting");
        private static readonly int RecoverState = Animator.StringToHash("Base Layer.FireVolleyRecover");

        private void Awake() => CacheComponents();

        private void CacheComponents()
        {
            if (movement == null) movement = GetComponent<BossWitchMovementBehavior>();
            if (enemyBody == null) enemyBody = GetComponent<EnemyBase>();
            if (statusEffects == null) statusEffects = GetComponent<StatusEffectManager>();
        }

        public bool CanStart(EnemyAIController enemy)
        {
            CacheComponents();
            return isActiveAndEnabled && stage == Stage.Idle && projectilePrefab != null &&
                enemyBody != null && !enemyBody.isDead && enemy != null && enemy.CurrentTarget != null &&
                enemy.animator != null && enemy.animator.isActiveAndEnabled && enemy.animator.layerCount > 0 &&
                enemy.animator.HasState(0, PrepareState) && enemy.animator.HasState(0, CastingState) &&
                enemy.animator.HasState(0, RecoverState) &&
                ((Vector2)(enemy.CurrentTarget.position - transform.position)).sqrMagnitude <= activationRange * activationRange;
        }

        public void Begin(EnemyAIController enemy)
        {
            CacheComponents();
            activeEnemy = enemy;
            stage = Stage.Preparation;
            stageStartedAt = Time.time;
            movement.SetCastingFacingReversed(reverseCastingFacing);
            FaceTarget(enemy);
            enemy.PlayAnimation("FireVolleyPrepare");
            ShowCue(true);
        }

        public bool UpdatePattern(EnemyAIController enemy)
        {
            if (stage == Stage.Idle || stage == Stage.Finished) return true;
            if (enemy.CurrentTarget == null) return false;

            if (stage == Stage.Preparation)
            {
                FaceTarget(enemy);
                ShowCue(true);
                if (Time.time - stageStartedAt < preparationDuration || !AnimationFinished(enemy, PrepareState))
                    return false;
                schedule.Start(Time.time, volleyCount, projectilesPerVolley, volleyInterval, spreadAngle);
                stage = Stage.Firing;
                enemy.PlayAnimation("FireVolleyCasting");
            }

            if (stage == Stage.Firing)
            {
                if (schedule.TryAdvance(Time.time)) FireVolley(enemy);
                ShowCue(true);
                if (!schedule.IsComplete) return false;
                stage = Stage.Recovery;
                stageStartedAt = Time.time;
                ShowCue(false);
                enemy.PlayAnimation("FireVolleyRecover");
            }

            if (stage == Stage.Recovery && Time.time - stageStartedAt >= recoveryDuration &&
                AnimationFinished(enemy, RecoverState))
            {
                stage = Stage.Finished;
                return true;
            }
            return false;
        }

        private static bool AnimationFinished(EnemyAIController enemy, int stateHash)
        {
            if (enemy.animator == null || !enemy.animator.isActiveAndEnabled || enemy.animator.layerCount == 0)
                return false;
            AnimatorStateInfo state = enemy.animator.GetCurrentAnimatorStateInfo(0);
            return state.fullPathHash == stateHash && state.normalizedTime >= 1f && !enemy.animator.IsInTransition(0);
        }

        private void FaceTarget(EnemyAIController enemy)
        {
            float horizontal = enemy.CurrentTarget.position.x - transform.position.x;
            if (Mathf.Abs(horizontal) <= 0.01f) return;
            Vector2 facing = horizontal > 0f ? Vector2.right : Vector2.left;
            enemy.SetForcedFacingDirection(facing);
            movement.UpdateFacing(facing);
        }

        private Vector3 MuzzlePosition()
        {
            float sign = movement != null && !movement.FacingRight ? -1f : 1f;
            return transform.position + new Vector3(muzzleOffset.x * sign, muzzleOffset.y, 0f);
        }

        private void FireVolley(EnemyAIController enemy)
        {
            // Re-aim once per wave; all five projectiles share that wave's aim.
            FaceTarget(enemy);
            Vector3 origin = MuzzlePosition();
            Vector2 aim = enemy.CurrentTarget.position - origin;
            if (aim.sqrMagnitude < 0.0001f) aim = movement.FacingRight ? Vector2.right : Vector2.left;
            float centerAngle = Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg;
            float damage = enemy.enemyData != null ? enemy.enemyData.damageAmount : 2f;
            damage *= damageMultiplier;
            if (statusEffects != null) damage *= statusEffects.GetOutgoingDamageMultiplier();
            for (int i = 0; i < schedule.ProjectilesPerVolley; i++)
            {
                float angle = (centerAngle + schedule.GetAngleOffset(i)) * Mathf.Deg2Rad;
                Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                BossWitchProjectile projectile = Instantiate(projectilePrefab, origin, Quaternion.identity);
                projectile.Initialize(enemyBody, direction, Mathf.Max(0f, damage));
            }
        }

        private void ShowCue(bool visible)
        {
            if (chargeCue == null) return;
            chargeCue.enabled = visible;
            if (!visible) return;
            chargeCue.sprite = projectilePrefab.PreviewSprite;
            chargeCue.color = new Color(1f, 0.7f, 0.35f, 1f);
            chargeCue.transform.position = MuzzlePosition();
        }

        public void End(EnemyAIController enemy, bool completed)
        {
            ShowCue(false);
            if (movement != null) movement.SetCastingFacingReversed(false);
            enemy.ClearForcedFacingDirection();
            stage = Stage.Idle;
            activeEnemy = null;
        }

        public void ResetForReuse()
        {
            CacheComponents();
            ShowCue(false);
            if (movement != null) movement.SetCastingFacingReversed(false);
            stage = Stage.Idle;
            activeEnemy = null;
        }

        private void OnDisable()
        {
            if (activeEnemy != null) activeEnemy.ClearForcedFacingDirection();
            ShowCue(false);
            if (movement != null) movement.SetCastingFacingReversed(false);
            stage = Stage.Idle;
            activeEnemy = null;
        }
    }
}
