using System.Collections.Generic;
using Nytherion.Core.Systems;
using Nytherion.GamePlay.Characters.Player;
using Nytherion.GamePlay.Combat;
using Nytherion.GamePlay.Skills;
using UnityEngine;

namespace Nytherion.GamePlay.Characters.Enemy
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EnemyAIController))]
    public sealed class BossDemonSlimeCombatBehavior : MonoBehaviour, IEnemyCombatBehavior
    {
        public enum AttackMode { Random, Test }

        [Header("Pattern Selection / 패턴 선택")]
        [Tooltip("Random: 패턴 쿨타임이 되면 준비된 패턴을 선택합니다. Test: 기본공격을 설정 횟수만큼 사용한 뒤 지정 패턴을 1회 사용합니다.")]
        [SerializeField] private AttackMode attackMode = AttackMode.Random;
        [SerializeField] private BossDemonSlimePatternType testPattern = BossDemonSlimePatternType.DashCleave;
        [SerializeField, Min(0)] private int testBasicAttackCount = 3;
        [SerializeField, Min(0f)] private float patternCooldown = 8f;
        [SerializeField] private MonoBehaviour[] patternBehaviors = new MonoBehaviour[0];

        [Header("Basic Cleave / 제자리 검 베기")]
        [SerializeField, Min(0.1f)] private float attackStartDistance = 2.8f;
        [SerializeField, Min(0.1f)] private float hitRadius = 3.5f;
        [SerializeField, Range(10f, 180f)] private float hitAngle = 120f;
        [SerializeField] private Vector2 attackOriginOffset = Vector2.zero;
        [Tooltip("전체 베기 애니메이션이 끝난 뒤 다음 공격까지의 시간입니다.")]
        [SerializeField, Min(0f)] private float attackInterval = 1f;
        [SerializeField, Min(0f)] private float damageMultiplier = 1f;
        [SerializeField] private LayerMask wallLayers = (1 << 9) | (1 << 11);

        [Header("Attack Range Preview / 타격 범위 표시")]
        [SerializeField] private bool showAttackPreview = true;
        [SerializeField] private Material previewMaterial;
        [SerializeField] private Color previewFillColor = new Color(1f, 0f, 0f, 0.2f);
        [SerializeField] private Color previewEdgeColor = new Color(1f, 0.2f, 0.1f, 0.8f);

        private const int PreviewSegments = 48;
        private static readonly int CleaveState = Animator.StringToHash("Base Layer.BasicCleave");
        private BossDemonSlimeEnemy body;
        private BossDemonSlimeMovementBehavior movement;
        private StatusEffectManager statusEffects;
        private EnemyAIController activeEnemy;
        private Collider2D[] hits;
        private RaycastHit2D[] wallHits;
        private HashSet<int> damaged;
        private bool attacking;
        private bool struck;
        private float nextAttackAt;
        private Vector2 origin;
        private Vector2 wallOrigin;
        private Vector2 direction = Vector2.right;
        private float activeRadius;
        private float activeAngle;
        private float activeDamageMultiplier;
        private int activeCleaveState;
        private bool activeCircularArea;
        private bool cleavePreviewActive;
        private bool activeAttackIsTest;
        private int testBasicAttacksUsed;
        private AttackMode? cycleMode;
        private BossDemonSlimePatternType cycleTestPattern;
        private bool patternClockStarted;
        private float nextPatternAt;
        private IBossDemonSlimePattern activePattern;
        private MonoBehaviour activePatternComponent;
        private BossDemonSlimePatternType? lastPattern;
        private BossDemonSlimePatternType? warnedUnavailablePattern;
        private List<int> patternCandidates;
        private HashSet<BossDemonSlimePatternType> candidateTypes;
        private GameObject previewObject;
        private Mesh previewMesh;
        private MeshRenderer previewFill;
        private LineRenderer previewEdge;

        public BossDemonSlimePatternType? CurrentPattern => activePattern?.PatternType;
        public Vector2 AttackOriginOffset => attackOriginOffset;

        private void Awake() => CacheComponents();

        private void CacheComponents()
        {
            if (body == null) body = GetComponent<BossDemonSlimeEnemy>();
            if (movement == null) movement = GetComponent<BossDemonSlimeMovementBehavior>();
            if (statusEffects == null) statusEffects = GetComponent<StatusEffectManager>();
            if (hits == null) hits = new Collider2D[64];
            if (wallHits == null) wallHits = new RaycastHit2D[64];
            if (damaged == null) damaged = new HashSet<int>();
            if (patternCandidates == null) patternCandidates = new List<int>(4);
            if (candidateTypes == null) candidateTypes = new HashSet<BossDemonSlimePatternType>();
        }

        private bool CanFight(EnemyAIController enemy)
        {
            return isActiveAndEnabled && body != null && !body.isDead &&
                body.CurrentForm == BossDemonSlimeEnemy.Form.Demon && movement != null &&
                enemy != null && enemy.CurrentTarget != null && enemy.animator != null &&
                enemy.animator.isActiveAndEnabled && enemy.animator.layerCount > 0 &&
                enemy.animator.HasState(0, CleaveState);
        }

        private Vector2 AttackOrigin() => (Vector2)transform.position + attackOriginOffset;

        public bool ShouldEnterAttackState(EnemyAIController enemy)
        {
            CacheComponents();
            if (attacking || !CanFight(enemy)) return false;
            EnsurePatternTiming();
            if (Time.time < nextAttackAt) return false;
            BuildPatternCandidates(enemy);
            return patternCandidates.Count > 0 || (CanUseBasicAttack() &&
                Vector2.Distance(AttackOrigin(), enemy.CurrentTarget.position) <= attackStartDistance);
        }

        private void EnsurePatternTiming()
        {
            if (cycleMode != attackMode || cycleTestPattern != testPattern)
            {
                cycleMode = attackMode;
                cycleTestPattern = testPattern;
                testBasicAttacksUsed = 0;
                warnedUnavailablePattern = null;
            }
            if (patternClockStarted) return;
            patternClockStarted = true;
            nextPatternAt = Time.time + Mathf.Max(0f, patternCooldown);
        }

        private bool CanUseBasicAttack() => attackMode == AttackMode.Random ||
            testBasicAttacksUsed < Mathf.Max(0, testBasicAttackCount);

        private void BuildPatternCandidates(EnemyAIController enemy)
        {
            patternCandidates.Clear();
            candidateTypes.Clear();
            if (attackMode == AttackMode.Test)
            {
                if (CanUseBasicAttack()) return;
            }
            else if (Time.time < nextPatternAt) return;

            bool registered = false;
            if (patternBehaviors != null)
            {
                for (int i = 0; i < patternBehaviors.Length; i++)
                {
                    MonoBehaviour component = patternBehaviors[i];
                    if (component == null || !component.isActiveAndEnabled ||
                        !(component is IBossDemonSlimePattern pattern)) continue;
                    if (attackMode == AttackMode.Test && pattern.PatternType != testPattern) continue;
                    registered = true;
                    if (pattern.CanStart(enemy) && candidateTypes.Add(pattern.PatternType)) patternCandidates.Add(i);
                }
            }
            if (attackMode == AttackMode.Test)
            {
                if (registered) warnedUnavailablePattern = null;
                else if (warnedUnavailablePattern != testPattern)
                {
                    warnedUnavailablePattern = testPattern;
                    Debug.LogWarning("[DemonSlime] 선택한 Test Pattern이 미구현, 미연결 또는 비활성 상태입니다: " + testPattern, this);
                }
                return;
            }
            warnedUnavailablePattern = null;
            if (patternCandidates.Count > 1 && lastPattern.HasValue)
                for (int i = patternCandidates.Count - 1; i >= 0; i--)
                    if (((IBossDemonSlimePattern)patternBehaviors[patternCandidates[i]]).PatternType == lastPattern.Value)
                        patternCandidates.RemoveAt(i);
        }

        public void EnterAttackState(EnemyAIController enemy)
        {
            CacheComponents();
            if (!CanFight(enemy))
            {
                enemy.TransitionToState(enemy.chaseState);
                return;
            }
            EnsurePatternTiming();
            activeEnemy = enemy;
            activeAttackIsTest = attackMode == AttackMode.Test;
            attacking = true;
            struck = false;
            cleavePreviewActive = false;
            SetPreviewVisible(false);
            movement.SetPhaseAnimationLocked(true);
            enemy.SetMovementAllowed(false);
            BuildPatternCandidates(enemy);
            if (patternCandidates.Count > 0)
            {
                int index = patternCandidates[attackMode == AttackMode.Test ? 0 : Random.Range(0, patternCandidates.Count)];
                activePatternComponent = patternBehaviors[index];
                activePattern = (IBossDemonSlimePattern)activePatternComponent;
                lastPattern = activePattern.PatternType;
                activePattern.Begin(enemy, this);
                return;
            }
            if (!CanUseBasicAttack() || Vector2.Distance(AttackOrigin(), enemy.CurrentTarget.position) > attackStartDistance)
            {
                enemy.TransitionToState(enemy.chaseState);
                return;
            }
            origin = AttackOrigin();
            wallOrigin = origin;
            direction = (Vector2)enemy.CurrentTarget.position - origin;
            if (direction.sqrMagnitude < 0.0001f)
                direction = movement.FacingRight ? Vector2.right : Vector2.left;
            direction.Normalize();
            activeRadius = Mathf.Max(0.1f, hitRadius);
            activeAngle = Mathf.Clamp(hitAngle, 10f, 180f);
            activeCircularArea = false;
            activeDamageMultiplier = damageMultiplier;
            activeCleaveState = CleaveState;
            cleavePreviewActive = true;
            damaged.Clear();
            movement.FaceAttackDirection(direction);
            movement.SetPhaseAnimationLocked(true);
            enemy.SetMovementAllowed(false);
            enemy.SetForcedFacingDirection(direction);
            enemy.PlayAnimation("BasicCleave");
            BuildPreview();
        }

        public void UpdateAttackState(EnemyAIController enemy)
        {
            if (!attacking || !CanFight(enemy))
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
                if (activePattern.UpdatePattern(enemy))
                {
                    nextPatternAt = Time.time + Mathf.Max(0f, patternCooldown);
                    if (activeAttackIsTest) testBasicAttacksUsed = 0;
                    EndPattern(enemy, true);
                    enemy.TransitionToState(enemy.chaseState);
                    return;
                }
                UpdateCleavePreview();
                return;
            }
            UpdateCleavePreview();
            AnimatorStateInfo state = enemy.animator.GetCurrentAnimatorStateInfo(0);
            if (state.fullPathHash != CleaveState || state.normalizedTime < 1f ||
                enemy.animator.IsInTransition(0)) return;
            enemy.TransitionToState(enemy.chaseState);
        }

        private void UpdateCleavePreview()
        {
            bool visible = cleavePreviewActive && showAttackPreview && !struck;
            if (visible && (previewObject == null || !previewObject.activeSelf))
                BuildPreview();
            SetPreviewVisible(visible);
        }

        // The BasicCleave clip invokes this when its tenth sprite is displayed.
        public void OnBasicCleaveHit()
        {
            if (activePattern != null)
            {
                if (attacking && activeEnemy != null && CanFight(activeEnemy)) activePattern.OnAnimationHit(activeEnemy);
                return;
            }
            if (ResolveCleaveHit() && activeAttackIsTest) testBasicAttacksUsed++;
        }

        public void BeginPatternCleave(IBossDemonSlimePattern source, EnemyAIController enemy,
            Vector2 forward, float radius, float angle, float multiplier)
        {
            if (!attacking || activePattern != source || !CanFight(enemy)) return;
            origin = AttackOrigin();
            wallOrigin = origin;
            direction = forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector2.right;
            activeRadius = Mathf.Max(0.1f, radius);
            activeAngle = Mathf.Clamp(angle, 10f, 180f);
            activeCircularArea = false;
            activeDamageMultiplier = multiplier;
            activeCleaveState = CleaveState;
            struck = false;
            cleavePreviewActive = true;
            damaged.Clear();
            movement.FaceAttackDirection(direction);
            enemy.SetForcedFacingDirection(direction);
            enemy.PlayAnimation("BasicCleave");
            BuildPreview();
        }

        public void BeginPatternCircularCleave(IBossDemonSlimePattern source, EnemyAIController enemy,
            Vector2 forward, Vector2 center, float radius, float multiplier, string animationState = "BasicCleave")
        {
            if (!attacking || activePattern != source || !CanFight(enemy)) return;
            origin = center;
            wallOrigin = AttackOrigin();
            direction = forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector2.right;
            activeRadius = Mathf.Max(0.1f, radius);
            activeCircularArea = true;
            activeDamageMultiplier = multiplier;
            activeCleaveState = Animator.StringToHash("Base Layer." + animationState);
            struck = false;
            cleavePreviewActive = true;
            damaged.Clear();
            movement.FaceAttackDirection(direction);
            enemy.SetForcedFacingDirection(direction);
            enemy.PlayAnimation(animationState);
            BuildPreview();
        }

        public void ResolvePatternCleaveHit(IBossDemonSlimePattern source)
        {
            if (activePattern == source) ResolveCleaveHit();
        }

        public void UpdatePatternCircularCleaveAim(IBossDemonSlimePattern source, EnemyAIController enemy,
            Vector2 forward, Vector2 center)
        {
            if (!attacking || activePattern != source || struck || !activeCircularArea || !CanFight(enemy)) return;
            origin = center;
            wallOrigin = AttackOrigin();
            if (forward.sqrMagnitude > 0.0001f) direction = forward.normalized;
            movement.FaceAttackDirection(direction);
            enemy.SetForcedFacingDirection(direction);
            // Reposition the existing warning without replaying the attack clip or clearing its hit state.
            SetPreviewVisible(cleavePreviewActive && showAttackPreview);
        }

        public void HidePatternCleavePreview(IBossDemonSlimePattern source)
        {
            if (activePattern != source) return;
            cleavePreviewActive = false;
            SetPreviewVisible(false);
        }

        private bool ResolveCleaveHit()
        {
            if (!attacking || struck || activeEnemy == null || !CanFight(activeEnemy) ||
                activeEnemy.animator.GetCurrentAnimatorStateInfo(0).fullPathHash != activeCleaveState) return false;
            struck = true;
            SetPreviewVisible(false);
            float damage = activeEnemy.enemyData != null ? activeEnemy.enemyData.damageAmount : 2f;
            damage *= activeDamageMultiplier;
            if (statusEffects != null) damage *= statusEffects.GetOutgoingDamageMultiplier();
            ContactFilter2D filter = new ContactFilter2D();
            filter.SetLayerMask(Physics2D.AllLayers);
            filter.useTriggers = true;
            int count = Physics2D.OverlapCircle(origin, activeRadius, filter, hits);
            for (int i = 0; i < count; i++)
            {
                Collider2D collider = hits[i];
                if (collider == null || collider.transform.IsChildOf(transform)) continue;
                DecoyDummy dummy = collider.GetComponentInParent<DecoyDummy>();
                PlayerHealth player = dummy == null ? collider.GetComponentInParent<PlayerHealth>() : null;
                if (dummy != null && !dummy.IsAlive) continue;
                if (dummy == null && (player == null || player.CurrentHealth <= 0f)) continue;
                Vector2 point = collider.ClosestPoint(origin);
                bool inside = activeCircularArea ? BossDemonSlimeAttackArea.ContainsCircle(point, origin, activeRadius) :
                    BossDemonSlimeAttackArea.Contains(point, origin, direction, activeRadius, activeAngle);
                if (!inside || BlockedByWall(point)) continue;
                int id = dummy != null ? dummy.GetInstanceID() : player.GetInstanceID();
                if (damaged.Add(id)) EnemyAttackTarget.TryDamage(collider, Mathf.Max(0f, damage), point);
            }
            return true;
        }

        private bool BlockedByWall(Vector2 point)
        {
            ContactFilter2D filter = new ContactFilter2D();
            filter.SetLayerMask(Physics2D.AllLayers);
            filter.useTriggers = false;
            int count = Physics2D.Linecast(wallOrigin, point, filter, wallHits);
            for (int i = 0; i < count; i++)
            {
                Collider2D collider = wallHits[i].collider;
                if (collider != null && (collider.CompareTag(Tags.Wall) ||
                    (wallLayers.value & (1 << collider.gameObject.layer)) != 0)) return true;
            }
            return false;
        }

        private void BuildPreview()
        {
            if (!showAttackPreview || previewMaterial == null) return;
            if (previewObject == null)
            {
                previewObject = new GameObject("Demon Basic Attack Preview");
                previewObject.layer = 2;
                previewObject.transform.SetParent(transform, false);
                MeshFilter filter = previewObject.AddComponent<MeshFilter>();
                previewFill = previewObject.AddComponent<MeshRenderer>();
                previewFill.sharedMaterial = previewMaterial;
                previewMesh = new Mesh { name = "Demon Attack Range" };
                filter.sharedMesh = previewMesh;
                GameObject edge = new GameObject("Attack Sector Edge");
                edge.transform.SetParent(previewObject.transform, false);
                previewEdge = edge.AddComponent<LineRenderer>();
                previewEdge.sharedMaterial = previewMaterial;
                previewEdge.useWorldSpace = false;
                previewEdge.widthMultiplier = 0.035f;
            }
            previewEdge.positionCount = activeCircularArea ? PreviewSegments + 1 : PreviewSegments + 3;
            previewObject.transform.position = new Vector3(origin.x, origin.y, transform.position.z);
            SpriteRenderer renderer = body.GetComponent<SpriteRenderer>();
            previewFill.sortingLayerID = previewEdge.sortingLayerID = renderer != null ? renderer.sortingLayerID : 0;
            previewFill.sortingOrder = renderer != null ? renderer.sortingOrder - 2 : -2;
            previewEdge.sortingOrder = previewFill.sortingOrder + 1;
            MaterialPropertyBlock properties = new MaterialPropertyBlock();
            properties.SetColor("_Color", previewFillColor);
            previewFill.SetPropertyBlock(properties);
            previewEdge.startColor = previewEdge.endColor = previewEdgeColor;
            Vector3[] vertices = new Vector3[PreviewSegments + 2];
            Color[] colors = new Color[vertices.Length];
            Vector2[] uv = new Vector2[vertices.Length];
            int[] triangles = new int[PreviewSegments * 3];
            colors[0] = Color.white;
            uv[0] = new Vector2(0.5f, 0.5f);
            if (!activeCircularArea) previewEdge.SetPosition(0, Vector3.zero);
            for (int i = 0; i <= PreviewSegments; i++)
            {
                Vector2 point = activeCircularArea ?
                    BossDemonSlimeAttackArea.CirclePoint(Vector2.zero, activeRadius, i / (float)PreviewSegments) :
                    BossDemonSlimeAttackArea.ArcPoint(Vector2.zero, direction, activeRadius, activeAngle, i / (float)PreviewSegments);
                vertices[i + 1] = point;
                uv[i + 1] = point / (activeRadius * 2f) + uv[0];
                colors[i + 1] = Color.white;
                previewEdge.SetPosition(activeCircularArea ? i : i + 1, point);
                if (i == PreviewSegments) continue;
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = i + 1;
                triangles[i * 3 + 2] = i + 2;
            }
            if (!activeCircularArea) previewEdge.SetPosition(PreviewSegments + 2, Vector3.zero);
            previewMesh.Clear();
            previewMesh.vertices = vertices;
            previewMesh.colors = colors;
            previewMesh.uv = uv;
            previewMesh.triangles = triangles;
            previewMesh.RecalculateBounds();
            SetPreviewVisible(true);
        }

        private void SetPreviewVisible(bool visible)
        {
            if (previewObject == null) return;
            if (visible) previewObject.transform.position = new Vector3(origin.x, origin.y, transform.position.z);
            previewObject.SetActive(visible);
        }

        public void ExitAttackState(EnemyAIController enemy)
        {
            if (attacking) nextAttackAt = Time.time + Mathf.Max(0f, attackInterval);
            EndPattern(enemy, false);
            attacking = false;
            struck = false;
            cleavePreviewActive = false;
            activeEnemy = null;
            SetPreviewVisible(false);
            if (movement != null) movement.SetPhaseAnimationLocked(false);
            enemy.ClearForcedFacingDirection();
            enemy.SetMovementAllowed(true);
        }

        private void EndPattern(EnemyAIController enemy, bool completed)
        {
            IBossDemonSlimePattern pattern = activePattern;
            activePattern = null;
            activePatternComponent = null;
            pattern?.End(enemy, completed);
        }

        public void ResetForReuse()
        {
            CacheComponents();
            EndPattern(activeEnemy, false);
            if (patternBehaviors != null)
                foreach (MonoBehaviour component in patternBehaviors)
                    if (component != null && component is IBossDemonSlimePattern pattern) pattern.ResetForReuse();
            attacking = false;
            struck = false;
            nextAttackAt = 0f;
            patternClockStarted = false;
            nextPatternAt = 0f;
            testBasicAttacksUsed = 0;
            cycleMode = attackMode;
            cycleTestPattern = testPattern;
            lastPattern = null;
            warnedUnavailablePattern = null;
            cleavePreviewActive = false;
            activeEnemy = null;
            activeCleaveState = CleaveState;
            damaged.Clear();
            SetPreviewVisible(false);
        }

        private void OnDisable()
        {
            if (attacking && activeEnemy != null) ExitAttackState(activeEnemy);
            attacking = false;
            activeEnemy = null;
            SetPreviewVisible(false);
        }

        private void OnDestroy()
        {
            if (previewMesh != null) Destroy(previewMesh);
        }

        private void OnDrawGizmosSelected()
        {
            if (attacking && !cleavePreviewActive) return;
            Vector2 center = attacking ? origin : AttackOrigin();
            Vector2 forward = direction;
            if (!attacking)
            {
                BossDemonSlimeMovementBehavior facing = GetComponent<BossDemonSlimeMovementBehavior>();
                forward = facing != null && !facing.FacingRight ? Vector2.left : Vector2.right;
                EnemyAIController enemy = GetComponent<EnemyAIController>();
                if (enemy != null && enemy.CurrentTarget != null &&
                    ((Vector2)enemy.CurrentTarget.position - center).sqrMagnitude > 0.0001f)
                    forward = ((Vector2)enemy.CurrentTarget.position - center).normalized;
            }
            float radius = attacking ? activeRadius : hitRadius;
            float angle = attacking ? activeAngle : hitAngle;
            Color oldColor = Gizmos.color;
            Matrix4x4 oldMatrix = Gizmos.matrix;
            Gizmos.matrix = Matrix4x4.identity;
            Gizmos.color = Color.red;
            bool circular = attacking && activeCircularArea;
            Vector2 previous = center;
            for (int i = 0; i <= PreviewSegments; i++)
            {
                Vector2 point = circular ? BossDemonSlimeAttackArea.CirclePoint(center, radius, i / (float)PreviewSegments) :
                    BossDemonSlimeAttackArea.ArcPoint(center, forward, radius, angle, i / (float)PreviewSegments);
                if (!circular || i > 0) Gizmos.DrawLine(previous, point);
                previous = point;
            }
            if (!circular) Gizmos.DrawLine(previous, center);
            Gizmos.color = oldColor;
            Gizmos.matrix = oldMatrix;
        }
    }
}
