using System.Collections.Generic;
using UnityEngine;

namespace Nytherion.GamePlay.Characters.Enemy
{
    public enum BossGameMasterPatternType { OrbLinks = 0, RotatingBeams = 2, DelayedHomingProjectiles = 3, ShieldCounter = 4 }

    public interface IBossGameMasterPattern
    {
        BossGameMasterPatternType PatternType { get; }
        bool CanStart();
        void Begin();
        bool Tick();
        void Clear();
    }

    [DisallowMultipleComponent]
    public sealed class BossGameMasterCombatBehavior : MonoBehaviour
    {
        public enum SelectionMode { Random, Test }
        [SerializeField] private SelectionMode selectionMode = SelectionMode.Test;
        [SerializeField] private BossGameMasterPatternType testPattern = BossGameMasterPatternType.OrbLinks;
        [SerializeField, Min(0f)] private float patternCooldown = 6f;
        [SerializeField, Min(0.1f)] private float basicAttackInterval = 2f;
        [SerializeField, Min(0.1f)] private float activationRange = 20f;
        private readonly List<IBossGameMasterPattern> available = new List<IBossGameMasterPattern>();
        private BossGameMasterIdleBehavior movement;
        private BossGameMasterEnemy owner;
        private EnemyAIController ai;
        private IBossGameMasterPattern active;
        private BossGameMasterReturningProjectilePattern basicAttack;
        private BossGameMasterPatternType? lastPattern;
        private bool attacking, castFinished, patternStarted, patternFinished;
        private bool movementReleased;
        private bool usingBasic;
        private float nextPatternAt, nextAttackAt;
        private int castState;
        private float castReleasePoint;
        private string CastAnimation => owner.IsUpgraded ? "UpgradedAttack" : "BasicAttack";

        private void Cache()
        {
            if (owner == null) owner = GetComponent<BossGameMasterEnemy>();
            if (movement == null) movement = GetComponent<BossGameMasterIdleBehavior>();
            if (ai == null) ai = GetComponent<EnemyAIController>();
            if (basicAttack == null) basicAttack = GetComponent<BossGameMasterReturningProjectilePattern>();
        }

        private bool Ready(EnemyAIController enemy)
        {
            Cache();
            return isActiveAndEnabled && owner != null && !owner.isDead && !owner.IsUpgrading &&
                enemy != null && enemy.CurrentTarget != null && enemy.animator != null &&
                enemy.animator.isActiveAndEnabled && enemy.animator.layerCount > 0;
        }

        private void Collect()
        {
            available.Clear();
            foreach (MonoBehaviour component in GetComponents<MonoBehaviour>())
                if (component.isActiveAndEnabled && component is IBossGameMasterPattern pattern &&
                    (selectionMode != SelectionMode.Test || pattern.PatternType == testPattern) && pattern.CanStart())
                    available.Add(pattern);
            if (selectionMode == SelectionMode.Random && available.Count > 1 && lastPattern.HasValue)
                available.RemoveAll(pattern => pattern.PatternType == lastPattern.Value);
        }

        public bool ShouldEnterAttackState(EnemyAIController enemy)
        {
            if (!Ready(enemy) || attacking || Time.time < nextAttackAt ||
                Vector2.Distance(transform.position, enemy.CurrentTarget.position) > activationRange) return false;
            int hash = Animator.StringToHash("Base Layer." + CastAnimation);
            if (!enemy.animator.HasState(0, hash)) return false;
            Collect();
            return (Time.time >= nextPatternAt && available.Count > 0) || (basicAttack != null && basicAttack.CanStart());
        }

        public void EnterAttackState(EnemyAIController enemy)
        {
            if (!ShouldEnterAttackState(enemy)) { enemy.TransitionToState(enemy.chaseState); return; }
            ai = enemy;
            usingBasic = Time.time < nextPatternAt || available.Count == 0;
            active = usingBasic ? null : available[Random.Range(0, available.Count)];
            attacking = true;
            castFinished = patternStarted = patternFinished = false;
            movementReleased = false;
            movement.SetPatternLocked(true);
            Vector2 aim = enemy.CurrentTarget.position - transform.position;
            movement.FacePattern(aim);
            enemy.SetForcedFacingDirection(aim);
            enemy.SetMovementAllowed(false);
            string clip = CastAnimation;
            castState = Animator.StringToHash("Base Layer." + clip);
            castReleasePoint = 39f / (owner.IsUpgraded ? 40f : 48f);
            enemy.PlayAnimation(clip);
        }

        // Called by frame 039 of both cast clips. The transition-only Upgrade state must never emit attacks.
        public void OnGameMasterCastRelease()
        {
            if (!attacking || patternStarted || !usingBasic || basicAttack == null || !Ready(ai)) return;
            AnimatorStateInfo state = ai.animator.GetCurrentAnimatorStateInfo(0);
            if (state.fullPathHash != castState || ai.animator.IsInTransition(0) ||
                state.normalizedTime + 0.00001f < castReleasePoint) return;
            StartPattern();
        }

        private void StartPattern()
        {
            if (patternStarted) return;
            patternStarted = true;
            if (usingBasic) basicAttack.Begin();
            else active.Begin();
        }

        public void UpdateAttackState(EnemyAIController enemy)
        {
            if (!Ready(enemy) || !attacking || (!usingBasic && active == null))
            {
                Cancel();
                if (owner != null && !owner.isDead && !owner.IsUpgrading) enemy.TransitionToState(enemy.chaseState);
                return;
            }
            if (!castFinished)
            {
                AnimatorStateInfo state = enemy.animator.GetCurrentAnimatorStateInfo(0);
                if (state.fullPathHash != castState || enemy.animator.IsInTransition(0)) return;
                // Covers a skipped animation event after a reload or a long frame, without duplicating a volley.
                if (!patternStarted && state.normalizedTime + 0.00001f >= castReleasePoint)
                    OnGameMasterCastRelease();
                if (state.normalizedTime >= 1f)
                {
                    castFinished = true;
                    StartPattern();
                    enemy.PlayAnimation("Idle");
                }
            }
            if (patternStarted && !patternFinished) patternFinished = usingBasic ? castFinished : active.Tick();
            bool canMoveDuringPattern = (active is BossGameMasterRotatingBeamPattern rotation && rotation.HasFired) ||
                (active is BossGameMasterHomingProjectilePattern homing && homing.HasSpawned) ||
                (active is BossGameMasterShieldPattern shield && shield.HasStarted);
            if (castFinished && !patternFinished && canMoveDuringPattern)
            {
                if (!movementReleased)
                {
                    movementReleased = true;
                    enemy.ClearForcedFacingDirection();
                    movement.SetPatternLocked(false);
                    enemy.SetMovementAllowed(true);
                }
                // Stay in the attack state to keep ticking the pattern until its off animation ends.
                movement.MoveTowards(transform.position);
            }
            if (!castFinished || !patternFinished) return;
            if (!usingBasic) lastPattern = active.PatternType;
            enemy.TransitionToState(enemy.chaseState);
        }

        public void ExitAttackState(EnemyAIController enemy)
        {
            bool wasBasic = usingBasic;
            Cancel(wasBasic && castFinished);
            nextAttackAt = Time.time + Mathf.Max(0.1f, basicAttackInterval);
            if (!wasBasic) nextPatternAt = Time.time + Mathf.Max(0f, patternCooldown);
        }

        public void RestartCooldown()
        {
            nextPatternAt = Time.time + Mathf.Max(0f, patternCooldown);
            nextAttackAt = Time.time + Mathf.Max(0.1f, basicAttackInterval);
        }

        public void Cancel() => Cancel(false);

        private void Cancel(bool preserveBasicProjectiles)
        {
            Cache();
            active?.Clear();
            if (!preserveBasicProjectiles && basicAttack != null) basicAttack.Clear();
            active = null;
            attacking = castFinished = patternStarted = patternFinished = false;
            movementReleased = false;
            usingBasic = false;
            if (movement != null) movement.SetPatternLocked(false);
            if (ai != null)
            {
                ai.ClearForcedFacingDirection();
                ai.SetMovementAllowed(owner != null && !owner.IsUpgrading && !owner.isDead);
            }
        }

        public void ResetForReuse()
        {
            Cancel();
            foreach (MonoBehaviour component in GetComponents<MonoBehaviour>())
                if (component is IBossGameMasterPattern pattern) pattern.Clear();
            lastPattern = null;
            RestartCooldown();
        }

        private void Update()
        {
            if (attacking && (owner == null || owner.isDead || owner.IsUpgrading || ai == null || !ai.enabled)) Cancel();
        }

        private void OnDisable() => ResetForReuse();
    }
}
