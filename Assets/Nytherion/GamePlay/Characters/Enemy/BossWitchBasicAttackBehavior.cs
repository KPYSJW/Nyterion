using Nytherion.GamePlay.Combat;
using UnityEngine;
using UnityEngine.Serialization;
using System.Collections.Generic;

namespace Nytherion.GamePlay.Characters.Enemy
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EnemyAIController), typeof(BossWitchMovementBehavior))]
    public sealed class BossWitchBasicAttackBehavior : MonoBehaviour, IEnemyCombatBehavior
    {
        public enum AttackMode { Random, Test }

        [Header("Pattern Selection / 패턴 선택")]
        [Tooltip("Random: 기본공격과 패턴 쿨타임 사용. Test: 기본공격을 설정 횟수만큼 발사한 뒤 지정 패턴을 1회 사용합니다. 변경은 다음 공격부터 적용됩니다.")]
        [SerializeField] private AttackMode attackMode = AttackMode.Random;
        [SerializeField] private BossWitchPatternType testPattern = BossWitchPatternType.FireballVolley;
        [Tooltip("Test 모드에서 패턴 전 발사할 기본공격 횟수입니다. 회전 구체는 제외하며, 0이면 패턴만 반복합니다.")]
        [SerializeField, Min(0)] private int testBasicAttackCount = 3;

        [Header("Attack Timing")]
        [FormerlySerializedAs("cooldown")]
        [SerializeField, Min(0.05f)] private float attackInterval = 1.1f;
        [SerializeField, Min(0f)] private float patternCooldown = 8f;
        [Tooltip("실제로 구현한 IBossWitchPattern 컴포넌트를 연결합니다. 미연결 패턴은 실행하지 않습니다.")]
        [SerializeField] private MonoBehaviour[] patternBehaviors = new MonoBehaviour[0];

        [Header("Basic Attack")]
        [SerializeField, Min(0.1f)] private float attackRange = 10f;
        [SerializeField] private BossWitchProjectile fireballPrefab;
        [SerializeField] private BossWitchProjectile lightningPrefab;
        [SerializeField] private Vector2 muzzleOffset = new Vector2(0.65f, 0.15f);
        [SerializeField, Min(0f)] private float damageMultiplier = 1f;

        [Header("Element Cue")]
        [SerializeField] private SpriteRenderer castCue;
        [SerializeField] private Color fireCueColor = new Color(1f, 0.7f, 0.35f, 1f);
        [SerializeField] private Color lightningCueColor = new Color(1f, 1f, 0.6f, 1f);

        private BossWitchMovementBehavior movement;
        private EnemyBase enemyBody;
        private StatusEffectManager statusEffects;
        private BossWitchOrbitController orbitController;
        private EnemyAIController activeEnemy;
        private bool casting;
        private bool fired;
        private bool castingLightning;
        private int sequenceIndex;
        private int testBasicAttacksFired;
        private bool activeAttackIsTest;
        private AttackMode? cycleMode;
        private BossWitchPatternType cycleTestPattern;
        private readonly BossWitchAttackTiming timing = new BossWitchAttackTiming();
        private bool timingInitialized;
        private IBossWitchPattern activePattern;
        private MonoBehaviour activePatternComponent;
        private readonly List<int> patternCandidates = new List<int>(4);
        private readonly HashSet<BossWitchPatternType> candidateTypes = new HashSet<BossWitchPatternType>();
        private BossWitchPatternType? lastPattern;
        private BossWitchPatternType? warnedUnavailableTestPattern;
        public BossWitchPatternType? CurrentPattern => activePattern?.PatternType;
        private static readonly int FastCastState = Animator.StringToHash("Base Layer.FastCast");

        private void Awake() => CacheComponents();

        private void CacheComponents()
        {
            if (movement == null) movement = GetComponent<BossWitchMovementBehavior>();
            if (enemyBody == null) enemyBody = GetComponent<EnemyBase>();
            if (statusEffects == null) statusEffects = GetComponent<StatusEffectManager>();
            if (orbitController == null) orbitController = GetComponent<BossWitchOrbitController>();
        }

        private bool CanFight(EnemyAIController enemy)
        {
            return isActiveAndEnabled && enemy != null && enemy.CurrentTarget != null &&
                enemyBody != null && !enemyBody.isDead &&
                fireballPrefab != null && lightningPrefab != null;
        }

        private bool InRange(EnemyAIController enemy, float range)
        {
            return ((Vector2)(enemy.CurrentTarget.position - transform.position)).sqrMagnitude <= range * range;
        }

        public bool ShouldEnterAttackState(EnemyAIController enemy)
        {
            CacheComponents();
            EnsureTiming();
            if (!CanFight(enemy) || !timing.IsAttackReady(Time.time)) return false;
            BuildPatternCandidates(enemy);
            return patternCandidates.Count > 0 ||
                (CanUseBasicAttack() && InRange(enemy, attackRange));
        }

        private void EnsureTiming()
        {
            if (cycleMode != attackMode || cycleTestPattern != testPattern)
            {
                cycleMode = attackMode;
                cycleTestPattern = testPattern;
                testBasicAttacksFired = 0;
                warnedUnavailableTestPattern = null;
            }
            if (timingInitialized) return;
            timing.Reset(Time.time, attackInterval, patternCooldown);
            timingInitialized = true;
        }

        private bool CanUseBasicAttack()
        {
            return attackMode == AttackMode.Random ||
                testBasicAttacksFired < Mathf.Max(0, testBasicAttackCount);
        }

        private void BuildPatternCandidates(EnemyAIController enemy)
        {
            patternCandidates.Clear();
            candidateTypes.Clear();
            if (attackMode == AttackMode.Random)
            {
                warnedUnavailableTestPattern = null;
                if (!timing.IsPatternReady(Time.time)) return;
            }
            else if (CanUseBasicAttack()) return;

            bool testPatternRegistered = false;
            if (patternBehaviors != null)
            {
                for (int i = 0; i < patternBehaviors.Length; i++)
                {
                    MonoBehaviour component = patternBehaviors[i];
                    if (component == null || !component.isActiveAndEnabled ||
                        !(component is IBossWitchPattern pattern)) continue;
                    if (attackMode == AttackMode.Test)
                    {
                        if (pattern.PatternType != testPattern) continue;
                        testPatternRegistered = true;
                    }
                    if (pattern.CanStart(enemy) && candidateTypes.Add(pattern.PatternType))
                        patternCandidates.Add(i);
                }
            }

            if (attackMode == AttackMode.Test)
            {
                if (testPatternRegistered) warnedUnavailableTestPattern = null;
                else if (warnedUnavailableTestPattern != testPattern)
                {
                    warnedUnavailableTestPattern = testPattern;
                    Debug.LogWarning($"[WitchBoss] 테스트 패턴 '{testPattern}'은 미구현, 미연결 또는 비활성 상태입니다. " +
                        "실행 가능한 Test Pattern을 선택하거나 Attack Mode를 Random으로 바꿔주세요.", this);
                }
                return;
            }

            // Keep one ready pattern repeatable; with alternatives, exclude the previous type.
            if (patternCandidates.Count > 1 && lastPattern.HasValue)
                for (int i = patternCandidates.Count - 1; i >= 0; i--)
                    if (((IBossWitchPattern)patternBehaviors[patternCandidates[i]]).PatternType == lastPattern.Value)
                        patternCandidates.RemoveAt(i);
        }

        public void EnterAttackState(EnemyAIController enemy)
        {
            CacheComponents();
            EnsureTiming();
            activeEnemy = enemy;
            activeAttackIsTest = attackMode == AttackMode.Test;
            movement.SetCombatAnimationLocked(true);
            enemy.SetMovementAllowed(false);
            BuildPatternCandidates(enemy);
            // Randomize only once, when an attack actually begins.
            int patternIndex = patternCandidates.Count == 0 ? -1 :
                patternCandidates[attackMode == AttackMode.Test ? 0 : Random.Range(0, patternCandidates.Count)];
            if (patternIndex >= 0)
            {
                activePatternComponent = patternBehaviors[patternIndex];
                activePattern = (IBossWitchPattern)activePatternComponent;
                lastPattern = activePattern.PatternType;
                orbitController?.FireForAttack(enemy);
                activePattern.Begin(enemy);
            }
            else if (CanUseBasicAttack() && InRange(enemy, attackRange))
            {
                BeginCast(enemy);
            }
            else
            {
                enemy.TransitionToState(enemy.chaseState);
            }
        }

        private void BeginCast(EnemyAIController enemy)
        {
            castingLightning = sequenceIndex == 2;
            casting = true;
            fired = false;
            movement.SetCastingFacingReversed(false);
            FaceTarget(enemy);
            enemy.PlayAnimation("FastCast");
            UpdateCue(true);
        }

        private void FaceTarget(EnemyAIController enemy)
        {
            Vector2 direction = enemy.CurrentTarget.position - transform.position;
            if (Mathf.Abs(direction.x) > 0.01f)
            {
                Vector2 facing = direction.x > 0f ? Vector2.right : Vector2.left;
                enemy.SetForcedFacingDirection(facing);
                movement.UpdateFacing(facing);
            }
        }

        private Vector3 MuzzlePosition()
        {
            float sign = movement != null && !movement.FacingRight ? -1f : 1f;
            return transform.position + new Vector3(muzzleOffset.x * sign, muzzleOffset.y, 0f);
        }

        public void UpdateAttackState(EnemyAIController enemy)
        {
            if (!CanFight(enemy))
            {
                enemy.TransitionToState(enemy.chaseState);
                return;
            }

            if (activePattern != null)
            {
                if (activePatternComponent == null || !activePatternComponent.isActiveAndEnabled)
                {
                    enemy.TransitionToState(enemy.chaseState);
                    return;
                }
                if (!activePattern.UpdatePattern(enemy)) return;

                EndPattern(enemy, true);
                CompleteAttack(enemy, true);
                return;
            }

            if (casting)
            {
                if (!fired)
                {
                    FaceTarget(enemy);
                    UpdateCue(true);
                }
                if (enemy.animator == null || !enemy.animator.isActiveAndEnabled ||
                    enemy.animator.layerCount == 0) return;
                AnimatorStateInfo state = enemy.animator.GetCurrentAnimatorStateInfo(0);
                if (state.fullPathHash != FastCastState || state.normalizedTime < 1f ||
                    enemy.animator.IsInTransition(0)) return;

                casting = false;
                CompleteAttack(enemy, false);
                return;
            }

            enemy.TransitionToState(enemy.chaseState);
        }

        private void CompleteAttack(EnemyAIController enemy, bool usedPattern)
        {
            if (usedPattern && activeAttackIsTest) testBasicAttacksFired = 0;
            timing.CompleteAttack(Time.time, attackInterval, patternCooldown, usedPattern);
            // Leave the attack state so movement is available throughout the interval.
            enemy.TransitionToState(enemy.chaseState);
        }

        private void EndPattern(EnemyAIController enemy, bool completed)
        {
            IBossWitchPattern pattern = activePattern;
            MonoBehaviour component = activePatternComponent;
            activePattern = null;
            activePatternComponent = null;
            if (component != null) pattern.End(enemy, completed);
        }

        private void Fire(EnemyAIController enemy)
        {
            Vector3 origin = MuzzlePosition();
            Vector2 direction = (enemy.CurrentTarget.position - origin);
            if (direction.sqrMagnitude < 0.0001f)
                direction = movement.FacingRight ? Vector2.right : Vector2.left;
            float damage = enemy.enemyData != null ? enemy.enemyData.damageAmount : 2f;
            damage *= damageMultiplier;
            if (statusEffects != null) damage *= statusEffects.GetOutgoingDamageMultiplier();

            BossWitchProjectile prefab = castingLightning ? lightningPrefab : fireballPrefab;
            BossWitchProjectile projectile = Instantiate(prefab, origin, Quaternion.identity);
            projectile.Initialize(enemyBody, direction.normalized, Mathf.Max(0f, damage));
            if (activeAttackIsTest) testBasicAttacksFired++;
            orbitController?.FireForAttack(enemy);
            sequenceIndex = (sequenceIndex + 1) % 3;
            fired = true;
            UpdateCue(false);
        }

        // Invoked by the FastCast clip when its eighth sprite is displayed.
        public void OnFastCastRelease()
        {
            if (!casting || activePattern != null || fired || activeEnemy == null || !CanFight(activeEnemy)) return;
            FaceTarget(activeEnemy);
            Fire(activeEnemy);
        }

        private void UpdateCue(bool visible)
        {
            if (castCue == null) return;
            castCue.enabled = visible;
            if (!visible) return;
            BossWitchProjectile projectile = castingLightning ? lightningPrefab : fireballPrefab;
            castCue.sprite = projectile.PreviewSprite;
            castCue.color = castingLightning ? lightningCueColor : fireCueColor;
            castCue.transform.position = MuzzlePosition();
        }

        public void ExitAttackState(EnemyAIController enemy)
        {
            EndPattern(enemy, false);
            casting = false;
            fired = false;
            UpdateCue(false);
            movement.SetCastingFacingReversed(false);
            movement.SetCombatAnimationLocked(false);
            enemy.ClearForcedFacingDirection();
            enemy.SetMovementAllowed(true);
            activeEnemy = null;
        }

        public void ResetForReuse()
        {
            CacheComponents();
            if (activeEnemy != null) EndPattern(activeEnemy, false);
            activePattern = null;
            activePatternComponent = null;
            if (patternBehaviors != null)
            {
                foreach (MonoBehaviour component in patternBehaviors)
                    if (component != null && component is IBossWitchPattern pattern)
                        pattern.ResetForReuse();
            }
            casting = false;
            fired = false;
            sequenceIndex = 0;
            testBasicAttacksFired = 0;
            activeAttackIsTest = false;
            cycleMode = attackMode;
            cycleTestPattern = testPattern;
            lastPattern = null;
            warnedUnavailableTestPattern = null;
            patternCandidates.Clear();
            candidateTypes.Clear();
            timing.Reset(Time.time, attackInterval, patternCooldown);
            timingInitialized = true;
            UpdateCue(false);
            movement.SetCastingFacingReversed(false);
            movement.SetCombatAnimationLocked(false);
            activeEnemy = null;
        }

        private void OnDisable()
        {
            if (activeEnemy != null) EndPattern(activeEnemy, false);
            activePattern = null;
            activePatternComponent = null;
            timingInitialized = false;
            testBasicAttacksFired = 0;
            activeAttackIsTest = false;
            cycleMode = null;
            if (activeEnemy != null) activeEnemy.ClearForcedFacingDirection();
            if (movement != null)
            {
                movement.SetCastingFacingReversed(false);
                movement.SetCombatAnimationLocked(false);
            }
            casting = false;
            UpdateCue(false);
            activeEnemy = null;
        }
    }
}
