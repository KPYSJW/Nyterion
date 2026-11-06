using Nytherion.Core.Systems;
using UnityEngine;
using UnityEngine.AI;

namespace Nytherion.GamePlay.Characters.Enemy
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BossDemonSlimeMovementBehavior))]
    public sealed class BossDemonSlimeDashCleavePattern : MonoBehaviour, IBossDemonSlimePattern
    {
        [Header("Dash Cleave / 돌진 베기")]
        [SerializeField, Min(0f)] private float preparationDuration = 0.65f;
        [SerializeField, Min(0.01f)] private float dashDuration = 0.3f;
        [Tooltip("칼을 들어 올린 마지막 자세를 유지한 뒤 내려치기까지의 시간입니다.")]
        [SerializeField, Min(0f)] private float preImpactPause = 0.15f;
        [SerializeField, Min(0.1f)] private float activationRange = 8f;
        [SerializeField, Min(0f)] private float recoveryDuration = 0.55f;
        [SerializeField, Min(0.01f)] private float wallPadding = 0.05f;

        [Header("Circular Ground Impact / 원형 지면 타격")]
        [SerializeField, Min(0.1f)] private float hitRadius = 3.5f;
        [SerializeField] private float impactForwardOffset = 2.5f;
        [SerializeField] private float impactLateralOffset;
        [SerializeField] private BossDemonSlimeImpactEffect impactEffectPrefab;
        [SerializeField, Min(0f)] private float damageMultiplier = 1f;
        [SerializeField] private LayerMask wallLayers = (1 << 9) | (1 << 11);

        [Header("Dash Path Preview")]
        [SerializeField] private bool showPathPreview = true;
        [SerializeField] private Material previewMaterial;
        [SerializeField] private Color pathFillColor = new Color(1f, 0f, 0f, 0.15f);
        [SerializeField] private Color pathEdgeColor = new Color(1f, 0.2f, 0.1f, 0.8f);

        private enum Phase { None, Preparing, Dashing, DashFinished, Raising, Holding, Striking, Recovering }
        private Phase phase;
        private static readonly int CleaveState = Animator.StringToHash("Base Layer.BasicCleave");
        private static readonly int DashRunState = Animator.StringToHash("Base Layer.DashRun");
        private static readonly int RaiseState = Animator.StringToHash("Base Layer.DashCleaveRaise");
        private static readonly int StrikeState = Animator.StringToHash("Base Layer.DashCleaveStrike");
        private BossDemonSlimeEnemy body;
        private BossDemonSlimeMovementBehavior movement;
        private Collider2D bodyCollider;
        private BossDemonSlimeCombatBehavior combat;
        private EnemyAIController activeEnemy;
        private RaycastHit2D[] wallHits;
        private Vector2 direction;
        private Vector2 dashStart;
        private float intendedDistance;
        private float plannedDistance;
        private float preparationEndsAt;
        private float recoveryEndsAt;
        private float holdEndsAt;
        private float remainingDashTime;
        private float dashSpeed;
        private bool finishOnNextFixedUpdate;
        private bool impactPlayed;
        private Vector2 impactCenter;
        private float impactRadius;

        private NavMeshAgent controlledAgent;
        private Rigidbody2D controlledBody;
        private Transform controlledTransform;
        private Vector3 bodyToAgentOffset;
        private bool changedAgentPosition;
        private bool originalAgentUpdatePosition;
        private GameObject pathPreview;
        private LineRenderer pathFill;
        private LineRenderer pathEdge;

        public BossDemonSlimePatternType PatternType => BossDemonSlimePatternType.DashCleave;

        private void CacheComponents()
        {
            if (body == null) body = GetComponent<BossDemonSlimeEnemy>();
            if (movement == null) movement = GetComponent<BossDemonSlimeMovementBehavior>();
            if (bodyCollider == null) bodyCollider = GetComponent<Collider2D>();
            if (wallHits == null) wallHits = new RaycastHit2D[64];
        }

        private bool OwnerAlive => activeEnemy != null && activeEnemy.isActiveAndEnabled &&
            body != null && !body.isDead && body.CurrentForm == BossDemonSlimeEnemy.Form.Demon;

        public bool CanStart(EnemyAIController enemy)
        {
            CacheComponents();
            return isActiveAndEnabled && phase == Phase.None && body != null && !body.isDead &&
                body.CurrentForm == BossDemonSlimeEnemy.Form.Demon && enemy != null &&
                enemy.CurrentTarget != null && enemy.rb != null && enemy.rb.simulated &&
                enemy.agent != null && enemy.agent.isActiveAndEnabled && enemy.agent.isOnNavMesh &&
                enemy.animator != null && enemy.animator.isActiveAndEnabled && enemy.animator.layerCount > 0 &&
                enemy.animator.HasState(0, DashRunState) && enemy.animator.HasState(0, CleaveState) &&
                enemy.animator.HasState(0, RaiseState) && enemy.animator.HasState(0, StrikeState) &&
                Vector2.Distance(transform.position, enemy.CurrentTarget.position) <= activationRange;
        }

        public void Begin(EnemyAIController enemy, BossDemonSlimeCombatBehavior owner)
        {
            CacheComponents();
            activeEnemy = enemy;
            combat = owner;
            phase = Phase.Preparing;
            impactPlayed = false;
            dashStart = enemy.rb.position;
            Vector2 aim = (Vector2)enemy.CurrentTarget.position - dashStart;
            direction = aim.sqrMagnitude > 0.0001f ? aim.normalized :
                movement.FacingRight ? Vector2.right : Vector2.left;
            // Capture the player's position when the dash telegraph starts; do not home during travel.
            intendedDistance = aim.magnitude;
            bodyToAgentOffset = transform.position - enemy.agent.nextPosition;
            plannedDistance = SafeMoveDistance(intendedDistance);
            preparationEndsAt = Time.time + Mathf.Max(0f, preparationDuration);
            movement.FaceAttackDirection(direction);
            enemy.SetForcedFacingDirection(direction);
            enemy.PlayAnimation("Idle");
            UpdatePathPreview();
        }

        public bool UpdatePattern(EnemyAIController enemy)
        {
            if (!OwnerAlive) return true;
            if (phase == Phase.Preparing)
            {
                plannedDistance = SafeMoveDistance(intendedDistance);
                UpdatePathPreview();
                if (Time.time >= preparationEndsAt) BeginDash(enemy);
            }
            else if (phase == Phase.DashFinished)
            {
                phase = Phase.Raising;
                UpdateCleaveAim(enemy);
                impactRadius = Mathf.Max(0.1f, hitRadius);
                combat.BeginPatternCircularCleave(this, enemy, direction, impactCenter, impactRadius,
                    damageMultiplier, "DashCleaveRaise");
            }
            else if (phase == Phase.Raising || phase == Phase.Holding || phase == Phase.Striking)
            {
                if (!impactPlayed)
                {
                    UpdateCleaveAim(enemy);
                    combat.UpdatePatternCircularCleaveAim(this, enemy, direction, impactCenter);
                }
                AnimatorStateInfo state = enemy.animator.GetCurrentAnimatorStateInfo(0);
                bool finished = state.normalizedTime >= 1f && !enemy.animator.IsInTransition(0);
                if (phase == Phase.Raising && state.fullPathHash == RaiseState && finished)
                {
                    // The non-looping raise clip holds its ninth sprite naturally; global slow effects remain intact.
                    phase = Phase.Holding;
                    holdEndsAt = Time.time + Mathf.Max(0f, preImpactPause);
                    if (preImpactPause <= 0f) BeginStrike(enemy);
                }
                else if (phase == Phase.Holding && Time.time >= holdEndsAt) BeginStrike(enemy);
                else if (phase == Phase.Striking && state.fullPathHash == StrikeState && finished)
                {
                    combat.HidePatternCleavePreview(this);
                    phase = Phase.Recovering;
                    recoveryEndsAt = Time.time + Mathf.Max(0f, recoveryDuration);
                    enemy.PlayAnimation("Idle");
                }
            }
            else if (phase == Phase.Recovering) return Time.time >= recoveryEndsAt;
            return phase == Phase.None;
        }

        private void BeginStrike(EnemyAIController enemy)
        {
            phase = Phase.Striking;
            UpdateCleaveAim(enemy);
            combat.BeginPatternCircularCleave(this, enemy, direction, impactCenter, impactRadius,
                damageMultiplier, "DashCleaveStrike");
        }

        private void BeginDash(EnemyAIController enemy)
        {
            SetPathPreviewVisible(false);
            dashStart = enemy.rb.position;
            plannedDistance = SafeMoveDistance(intendedDistance);
            if (plannedDistance <= 0.001f)
            {
                phase = Phase.DashFinished;
                return;
            }
            controlledAgent = enemy.agent;
            controlledBody = enemy.rb;
            controlledTransform = enemy.transform;
            bodyToAgentOffset = enemy.transform.position - controlledAgent.nextPosition;
            originalAgentUpdatePosition = controlledAgent.updatePosition;
            controlledAgent.updatePosition = false;
            changedAgentPosition = true;
            controlledAgent.isStopped = true;
            remainingDashTime = Mathf.Max(0.01f, dashDuration);
            dashSpeed = plannedDistance / remainingDashTime;
            finishOnNextFixedUpdate = false;
            phase = Phase.Dashing;
            enemy.PlayAnimation("DashRun");
        }

        private void FixedUpdate()
        {
            if (phase == Phase.None) return;
            if (!OwnerAlive)
            {
                End(activeEnemy, false);
                return;
            }
            if (phase != Phase.Dashing)
            {
                if (activeEnemy.rb != null) activeEnemy.rb.velocity = Vector2.zero;
                return;
            }
            if (controlledBody == null || !controlledBody.simulated || controlledAgent == null ||
                !controlledAgent.isActiveAndEnabled || !controlledAgent.isOnNavMesh)
            {
                RestoreNavigation();
                phase = Phase.DashFinished;
                return;
            }
            // MovePosition completes in physics. Read its result on the following fixed step.
            SyncNavigationToBody();
            float traveled = Mathf.Max(0f, Vector2.Dot(controlledBody.position - dashStart, direction));
            float remaining = Mathf.Max(0f, plannedDistance - traveled);
            if (finishOnNextFixedUpdate || remainingDashTime <= 0f || remaining <= 0.001f)
            {
                RestoreNavigation();
                phase = Phase.DashFinished;
                return;
            }
            float stepTime = Mathf.Min(Time.fixedDeltaTime, remainingDashTime);
            float requested = Mathf.Min(dashSpeed * stepTime, remaining);
            float safe = SafeMoveDistance(requested);
            if (safe <= 0.001f)
            {
                RestoreNavigation();
                phase = Phase.DashFinished;
                return;
            }
            controlledBody.MovePosition(controlledBody.position + direction * safe);
            remainingDashTime = Mathf.Max(0f, remainingDashTime - stepTime);
            finishOnNextFixedUpdate = safe < requested - 0.001f || safe >= remaining - 0.001f ||
                remainingDashTime <= 0f;
        }

        private float SafeMoveDistance(float requested)
        {
            if (activeEnemy == null || activeEnemy.agent == null ||
                !activeEnemy.agent.isActiveAndEnabled || !activeEnemy.agent.isOnNavMesh) return 0f;
            requested = Mathf.Max(0f, requested);
            if (requested <= 0f) return 0f;
            Vector2 position = activeEnemy.rb != null ? activeEnemy.rb.position : (Vector2)transform.position;
            Vector3 endBody = new Vector3(position.x, position.y, transform.position.z) + (Vector3)(direction * requested);
            if (activeEnemy.agent.Raycast(endBody - bodyToAgentOffset, out NavMeshHit edge))
            {
                float available = Vector2.Dot((Vector2)(edge.position + bodyToAgentOffset) - position, direction);
                requested = Mathf.Min(requested, Mathf.Max(0f, available - wallPadding));
            }
            if (bodyCollider == null || !bodyCollider.isActiveAndEnabled || requested <= 0f) return requested;
            ContactFilter2D filter = new ContactFilter2D();
            filter.SetLayerMask(Physics2D.AllLayers);
            filter.useTriggers = false;
            int count = bodyCollider.Cast(direction, filter, wallHits, requested + wallPadding);
            for (int i = 0; i < count; i++)
            {
                Collider2D collider = wallHits[i].collider;
                if (collider == null || collider.transform.IsChildOf(transform)) continue;
                // Player bodies and player-only ice pillars never stop or deal contact damage during this dash.
                if (!collider.CompareTag(Tags.Wall) &&
                    (wallLayers.value & (1 << collider.gameObject.layer)) == 0) continue;
                requested = Mathf.Min(requested, Mathf.Max(0f, wallHits[i].distance - wallPadding));
            }
            return requested;
        }

        private void SyncNavigationToBody()
        {
            if (!changedAgentPosition || controlledBody == null || controlledTransform == null) return;
            Vector2 position = controlledBody.position;
            Vector3 world = new Vector3(position.x, position.y, controlledTransform.position.z);
            controlledTransform.position = world;
            if (controlledAgent != null && controlledAgent.isActiveAndEnabled && controlledAgent.isOnNavMesh)
                controlledAgent.nextPosition = world - bodyToAgentOffset;
        }

        private void RestoreNavigation()
        {
            if (!changedAgentPosition) return;
            SyncNavigationToBody();
            if (controlledAgent != null) controlledAgent.updatePosition = originalAgentUpdatePosition;
            if (controlledBody != null) controlledBody.velocity = Vector2.zero;
            changedAgentPosition = false;
            controlledAgent = null;
            controlledBody = null;
            controlledTransform = null;
        }

        public void OnAnimationHit(EnemyAIController enemy)
        {
            if (phase != Phase.Striking || !OwnerAlive || combat == null || impactPlayed ||
                enemy.animator.GetCurrentAnimatorStateInfo(0).fullPathHash != StrikeState) return;
            // Sample once more on the actual contact frame, then freeze both damage and the visual.
            UpdateCleaveAim(enemy);
            combat.UpdatePatternCircularCleaveAim(this, enemy, direction, impactCenter);
            impactPlayed = true;
            combat.ResolvePatternCleaveHit(this);
            if (impactEffectPrefab == null) return;
            BossDemonSlimeImpactEffect effect = Instantiate(impactEffectPrefab, WorldPoint(impactCenter), Quaternion.identity);
            effect.Initialize(body, impactRadius);
        }

        private void UpdateCleaveAim(EnemyAIController enemy)
        {
            if (enemy.CurrentTarget != null)
            {
                Vector2 aim = (Vector2)enemy.CurrentTarget.position - (Vector2)transform.position;
                if (aim.sqrMagnitude > 0.0001f) direction = aim.normalized;
            }
            impactCenter = ImpactCenterAt(transform.position);
        }

        private Vector2 ImpactCenterAt(Vector2 position)
        {
            Vector2 side = new Vector2(-direction.y, direction.x);
            return position + (combat != null ? combat.AttackOriginOffset : Vector2.zero) +
                direction * impactForwardOffset + side * impactLateralOffset;
        }

        public void End(EnemyAIController enemy, bool completed)
        {
            RestoreNavigation();
            SetPathPreviewVisible(false);
            phase = Phase.None;
            activeEnemy = null;
            combat = null;
            finishOnNextFixedUpdate = false;
            impactPlayed = false;
        }

        public void ResetForReuse() => End(activeEnemy, false);

        private void UpdatePathPreview()
        {
            if (!showPathPreview || previewMaterial == null) { SetPathPreviewVisible(false); return; }
            if (pathPreview == null)
            {
                pathPreview = new GameObject("Demon Dash Path Preview");
                pathPreview.layer = 2;
                pathPreview.transform.SetParent(transform, false);
                pathFill = CreatePreviewLine("Dash Corridor Fill", 2);
                pathEdge = CreatePreviewLine("Dash Corridor Edge", 5);
            }
            SpriteRenderer sprite = body.GetComponent<SpriteRenderer>();
            int sortingLayer = sprite != null ? sprite.sortingLayerID : 0;
            int sortingOrder = sprite != null ? sprite.sortingOrder : 0;
            pathFill.sortingLayerID = pathEdge.sortingLayerID = sortingLayer;
            pathFill.sortingOrder = sortingOrder - 2;
            pathEdge.sortingOrder = sortingOrder - 1;
            float halfWidth = Mathf.Max(0.1f, activeEnemy.agent.radius);
            Vector2 start = activeEnemy.rb.position;
            Vector2 end = start + direction * plannedDistance;
            Vector2 side = new Vector2(-direction.y, direction.x) * halfWidth;
            pathFill.widthMultiplier = halfWidth * 2f;
            pathFill.startColor = pathFill.endColor = pathFillColor;
            pathEdge.widthMultiplier = 0.035f;
            pathEdge.startColor = pathEdge.endColor = pathEdgeColor;
            pathFill.SetPosition(0, WorldPoint(start));
            pathFill.SetPosition(1, WorldPoint(end));
            Vector2[] corners = { start + side, end + side, end - side, start - side, start + side };
            for (int i = 0; i < corners.Length; i++) pathEdge.SetPosition(i, WorldPoint(corners[i]));
            SetPathPreviewVisible(true);
        }

        private Vector3 WorldPoint(Vector2 point) => new Vector3(point.x, point.y, transform.position.z);

        private LineRenderer CreatePreviewLine(string name, int count)
        {
            GameObject child = new GameObject(name);
            child.layer = 2;
            child.transform.SetParent(pathPreview.transform, false);
            LineRenderer line = child.AddComponent<LineRenderer>();
            line.sharedMaterial = previewMaterial;
            line.useWorldSpace = true;
            line.positionCount = count;
            return line;
        }

        private void SetPathPreviewVisible(bool visible)
        {
            if (pathPreview != null) pathPreview.SetActive(visible);
        }

        private void OnDisable() => End(activeEnemy, false);

        private void OnDrawGizmosSelected()
        {
            if (phase != Phase.Preparing && phase != Phase.Dashing) return;
            Color oldColor = Gizmos.color;
            Matrix4x4 oldMatrix = Gizmos.matrix;
            Gizmos.matrix = Matrix4x4.identity;
            Gizmos.color = Color.red;
            Vector2 end = dashStart + direction * plannedDistance;
            Gizmos.DrawLine(WorldPoint(dashStart), WorldPoint(end));
            float halfWidth = activeEnemy != null && activeEnemy.agent != null ?
                Mathf.Max(0.1f, activeEnemy.agent.radius) : 0.55f;
            Vector2 side = new Vector2(-direction.y, direction.x) * halfWidth;
            Vector2[] corners = { dashStart + side, end + side, end - side, dashStart - side, dashStart + side };
            for (int i = 1; i < corners.Length; i++)
                Gizmos.DrawLine(WorldPoint(corners[i - 1]), WorldPoint(corners[i]));
            Gizmos.color = oldColor;
            Gizmos.matrix = oldMatrix;
        }
    }
}
