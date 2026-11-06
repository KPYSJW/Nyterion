using System.Collections.Generic;
using Nytherion.Core.Systems;
using Nytherion.GamePlay.Combat;
using UnityEngine;
using UnityEngine.AI;

namespace Nytherion.GamePlay.Characters.Enemy
{
    [DisallowMultipleComponent]
    public sealed class BossDemonSlimeFireOrbPattern : MonoBehaviour, IBossDemonSlimePattern
    {
        [Header("Summon / 화염 구체 소환")]
        [SerializeField] private BossDemonSlimeFireOrb orbPrefab;
        [SerializeField, Min(1)] private int orbCount = 1;
        [SerializeField, Min(0.1f)] private float orbLifetime = 4f;
        [SerializeField] private Vector2 summonOffset = new Vector2(3f, 2.3f);
        [SerializeField, Min(0f)] private float orbSpacing = 2.2f;
        [SerializeField, Min(0.1f)] private float wallClearance = 0.65f;
        [SerializeField, Min(0.1f)] private float activationRange = 12f;
        [SerializeField] private LayerMask wallLayers = (1 << 9) | (1 << 11);
        [Header("Rotating Fireballs / 회전 탄막")]
        [SerializeField, Min(1)] private int directionCount = 6;
        [SerializeField, Min(1)] private int volleyCount = 8;
        [SerializeField, Min(0.01f)] private float volleyInterval = 0.35f;
        [SerializeField] private float rotationPerVolley = 12f;
        [SerializeField, Min(0.1f)] private float projectileSpeed = 5f;
        [SerializeField, Min(0.1f)] private float projectileScale = 1.25f;
        [SerializeField, Min(0.01f)] private float projectileHitRadius = 0.22f;
        [SerializeField, Min(0f)] private float damageMultiplier = 1f;

        private static readonly int SummonState = Animator.StringToHash("Base Layer.FireOrbSummon");
        private BossDemonSlimeEnemy body;
        private BossDemonSlimeMovementBehavior movement;
        private StatusEffectManager statusEffects;
        private bool casting;
        private readonly List<BossDemonSlimeFireOrb> orbs = new List<BossDemonSlimeFireOrb>();
        private readonly List<Collider2D> overlaps = new List<Collider2D>();
        private readonly List<RaycastHit2D> wallHits = new List<RaycastHit2D>();

        public BossDemonSlimePatternType PatternType => BossDemonSlimePatternType.FireOrbSummon;

        private void CacheComponents()
        {
            if (body == null) body = GetComponent<BossDemonSlimeEnemy>();
            if (movement == null) movement = GetComponent<BossDemonSlimeMovementBehavior>();
            if (statusEffects == null) statusEffects = GetComponent<StatusEffectManager>();
        }

        public bool CanStart(EnemyAIController enemy)
        {
            CacheComponents();
            return isActiveAndEnabled && !casting && body != null && !body.isDead &&
                body.CurrentForm == BossDemonSlimeEnemy.Form.Demon && movement != null &&
                orbPrefab != null && orbPrefab.IsConfigured && enemy != null && enemy.CurrentTarget != null &&
                enemy.agent != null && enemy.agent.isActiveAndEnabled && enemy.agent.isOnNavMesh &&
                enemy.animator != null && enemy.animator.isActiveAndEnabled && enemy.animator.layerCount > 0 &&
                enemy.animator.HasState(0, SummonState) &&
                Vector2.Distance(transform.position, enemy.CurrentTarget.position) <= Mathf.Max(0.1f, activationRange);
        }

        public void Begin(EnemyAIController enemy, BossDemonSlimeCombatBehavior combat)
        {
            CacheComponents();
            orbs.RemoveAll(orb => orb == null);
            casting = true;
            movement.SetPhaseAnimationLocked(true);
            enemy.SetMovementAllowed(false);
            FaceTarget(enemy);
            enemy.PlayAnimation("FireOrbSummon");
        }

        public bool UpdatePattern(EnemyAIController enemy)
        {
            if (!casting || body == null || body.isDead || !body.isActiveAndEnabled ||
                enemy == null || enemy.CurrentTarget == null) return true;
            FaceTarget(enemy);
            AnimatorStateInfo state = enemy.animator.GetCurrentAnimatorStateInfo(0);
            if (state.fullPathHash != SummonState || state.normalizedTime < 1f || enemy.animator.IsInTransition(0)) return false;
            int count = Mathf.Max(1, orbCount);
            Vector2 aim = (Vector2)enemy.CurrentTarget.position - (Vector2)transform.position;
            if (aim.sqrMagnitude < 0.0001f) aim = movement.FacingRight ? Vector2.right : Vector2.left;
            Vector2 side = new Vector2(-aim.normalized.y, aim.normalized.x);
            float sign = movement.FacingRight ? 1f : -1f;
            Vector3 origin = transform.position + new Vector3(summonOffset.x * sign, summonOffset.y, 0f);
            float angle = Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg;
            float damage = (enemy.enemyData != null ? enemy.enemyData.damageAmount : 2f) * Mathf.Max(0f, damageMultiplier);
            if (statusEffects != null) damage *= statusEffects.GetOutgoingDamageMultiplier();
            int spawned = 0;
            for (int i = 0; i < count; i++)
            {
                Vector3 intended = origin + (Vector3)(side * ((i - (count - 1) * 0.5f) * Mathf.Max(0f, orbSpacing)));
                if (!TrySpawnPosition(enemy, intended, out Vector3 point)) continue;
                BossDemonSlimeFireOrb orb = Instantiate(orbPrefab, point, Quaternion.identity);
                orb.Initialize(body, directionCount, volleyCount, volleyInterval, rotationPerVolley, orbLifetime,
                    projectileSpeed, projectileScale, projectileHitRadius, damage,
                    angle + i * 360f / (Mathf.Max(1, directionCount) * (float)count));
                orbs.Add(orb);
                spawned++;
            }
            if (spawned < count) Debug.LogWarning($"[DemonSlime] 벽과 바닥을 확인하여 화염 구체 {count}개 중 {spawned}개를 소환했습니다.", this);
            casting = false;
            enemy.PlayAnimation("Idle");
            return true;
        }

        private void FaceTarget(EnemyAIController enemy)
        {
            Vector2 aim = (Vector2)enemy.CurrentTarget.position - (Vector2)transform.position;
            if (aim.sqrMagnitude < 0.0001f) aim = movement.FacingRight ? Vector2.right : Vector2.left;
            movement.FaceAttackDirection(aim);
            enemy.SetForcedFacingDirection(aim.normalized);
        }

        private bool TrySpawnPosition(EnemyAIController enemy, Vector3 intended, out Vector3 point)
        {
            point = transform.position;
            if (enemy.agent == null || !enemy.agent.isActiveAndEnabled || !enemy.agent.isOnNavMesh) return false;
            NavMeshQueryFilter navigation = new NavMeshQueryFilter { agentTypeID = enemy.agent.agentTypeID, areaMask = enemy.agent.areaMask };
            ContactFilter2D filter = new ContactFilter2D();
            filter.SetLayerMask(Physics2D.AllLayers);
            filter.useTriggers = true;
            for (int step = 0; step <= 12; step++)
            {
                Vector3 candidate = Vector3.Lerp(intended, transform.position, step / 12f);
                if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, 0.3f, navigation)) continue;
                Physics2D.OverlapCircle(hit.position, Mathf.Max(0.1f, wallClearance), filter, overlaps);
                bool blocked = false;
                foreach (Collider2D collider in overlaps)
                    if (IsWall(collider)) { blocked = true; break; }
                if (blocked) continue;
                Physics2D.Linecast(transform.position, hit.position, filter, wallHits);
                foreach (RaycastHit2D wall in wallHits)
                    if (IsWall(wall.collider)) { blocked = true; break; }
                if (blocked) continue;
                point = new Vector3(hit.position.x, hit.position.y, transform.position.z);
                return true;
            }
            return false;
        }

        private bool IsWall(Collider2D collider) => collider != null && (collider.CompareTag(Tags.Wall) ||
            (wallLayers.value & (1 << collider.gameObject.layer)) != 0);

        public void OnAnimationHit(EnemyAIController enemy) { }
        public void End(EnemyAIController enemy, bool completed)
        {
            casting = false;
            if (!completed) ClearOrbs();
        }
        private void ClearOrbs()
        {
            foreach (BossDemonSlimeFireOrb orb in orbs)
                if (orb != null) orb.Cancel();
            orbs.Clear();
        }
        public void ResetForReuse() { casting = false; ClearOrbs(); }
        private void OnDisable() => ResetForReuse();
    }
}
