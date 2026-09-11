using Nytherion.GamePlay.Characters.Player;
using Nytherion.GamePlay.Combat;
using UnityEngine;

namespace Nytherion.GamePlay.Characters.Enemy
{
    [DisallowMultipleComponent]
    public sealed class GoblinChargeCombatBehavior : MonoBehaviour, IEnemyCombatBehavior
    {
        private enum ChargePhase
        {
            None,
            Charging,
            Beginning,
            Dashing,
            Ending
        }

        [Header("Charge Conditions")]
        [SerializeField, Min(0.1f)] private float chargeRange = 5f;
        [SerializeField, Min(0f)] private float chargeWaitTime = 1f;
        [SerializeField, Min(0f)] private float chargeCooldown = 2f;

        [Header("Charge Movement")]
        [SerializeField, Min(0.1f)] private float chargeSpeed = 12f;
        [SerializeField, Min(0.1f)] private float chargeDistance = 7f;
        [SerializeField, Min(0.01f)] private float arrivalDistance = 0.15f;
        [SerializeField, Min(0.01f)] private float blockedCheckTime = 0.12f;
        [SerializeField, Min(0f)] private float blockedMoveDistance = 0.01f;
        [SerializeField, Min(0f)] private float wallStopDistance = 0.03f;

        [Header("Charge Range Preview")]
        [SerializeField] private GameObject chargeRangePreview;
        [SerializeField, Min(0.05f)] private float previewWidth = 0.65f;

        [Header("Player Knockback")]
        [SerializeField, Min(0f)] private float knockbackMultiplier = 2.5f;
        [SerializeField, Min(0f)] private float minimumKnockback = 8f;
        [SerializeField, Min(0f)] private float maximumKnockback = 24f;
        [SerializeField, Min(0f)] private float knockbackDuration = 0.4f;
        [SerializeField, Min(0f)] private float backwardWeight = 0.35f;
        [SerializeField, Min(0f)] private float sideWeight = 1f;

        [Header("Animation State Names")]
        [SerializeField] private string chargeLoopAnimation = "Attack_BeginLoop";
        [SerializeField] private string chargeBeginAnimation = "Attack_Begin";
        [SerializeField] private string chargeAnimation = "Attack";
        [SerializeField] private string chargeEndAnimation = "Attack_End";

        private ChargePhase phase;
        private readonly RaycastHit2D[] castHits = new RaycastHit2D[12];
        private EnemyAIController activeEnemy;
        private EnemyBase enemyBase;
        private StatusEffectManager statusEffectManager;
        private Collider2D bodyCollider;
        private GoblinChargePreview previewController;
        private Transform previewOriginalParent;
        private Vector2 chargeDirection;
        private Vector2 blockedCheckPosition;
        private float traveledChargeDistance;
        private float phaseStartedAt;
        private float nextChargeTime;
        private float nextBlockedCheckTime;
        private bool damagedPlayerThisCharge;
        private bool originalAgentUpdatePosition;
        private bool changedAgentUpdatePosition;
        private bool finishDashNextFixedUpdate;

        private void Awake()
        {
            enemyBase = GetComponent<EnemyBase>();
            statusEffectManager = GetComponent<StatusEffectManager>();
            bodyCollider = GetComponent<Collider2D>();

            if (chargeRangePreview != null)
            {
                previewController = chargeRangePreview.GetComponent<GoblinChargePreview>();
                previewOriginalParent = chargeRangePreview.transform.parent;
                HideChargeRangePreview();
            }
        }

        private void FixedUpdate()
        {
            if (phase == ChargePhase.Dashing && activeEnemy != null)
            {
                UpdateDash(activeEnemy);
            }
        }

        public bool ShouldEnterAttackState(EnemyAIController enemy)
        {
            return enemy != null &&
                   enemy.player != null &&
                   Time.time >= nextChargeTime &&
                   enemy.GetDistanceToPlayer() <= chargeRange;
        }

        public void EnterAttackState(EnemyAIController enemy)
        {
            activeEnemy = enemy;
            damagedPlayerThisCharge = false;
            enemy.StopMovement();
            FaceTowardsPlayer(enemy);
            ShowChargeRangePreview(
                enemy,
                ((Vector2)enemy.player.position - (Vector2)enemy.transform.position).normalized);
            SetPhase(ChargePhase.Charging);
            enemy.PlayAnimation(chargeLoopAnimation);
        }

        public void UpdateAttackState(EnemyAIController enemy)
        {
            enemy.StopMovement();

            switch (phase)
            {
                case ChargePhase.Charging:
                    FaceTowardsPlayer(enemy);
                    ShowChargeRangePreview(
                        enemy,
                        ((Vector2)enemy.player.position - (Vector2)enemy.transform.position).normalized);
                    enemy.TryMoveToForcedDestination();
                    if (Time.time - phaseStartedAt >= chargeWaitTime)
                    {
                        LockChargeDestination(enemy);
                        SetPhase(ChargePhase.Beginning);
                        enemy.PlayAnimation(chargeBeginAnimation);
                    }
                    break;

                case ChargePhase.Beginning:
                    ShowChargeRangePreview(enemy, chargeDirection);
                    enemy.TryMoveToForcedDestination();
                    if (enemy.IsAnimationFinished(chargeBeginAnimation))
                    {
                        BeginDash(enemy);
                    }
                    break;

                case ChargePhase.Dashing:
                    break;

                case ChargePhase.Ending:
                    if (enemy.IsAnimationFinished(chargeEndAnimation))
                    {
                        nextChargeTime = Time.time + chargeCooldown;
                        enemy.TransitionToState(enemy.chaseState);
                    }
                    break;
            }
        }

        public void ExitAttackState(EnemyAIController enemy)
        {
            enemy.StopMovement();
            HideChargeRangePreview();
            activeEnemy = null;
            phase = ChargePhase.None;
            RestoreAgentPosition(enemy);
        }

        public void ResetForReuse()
        {
            RestoreAgentPosition(activeEnemy);
            HideChargeRangePreview();
            activeEnemy = null;
            phase = ChargePhase.None;
            chargeDirection = Vector2.zero;
            traveledChargeDistance = 0f;
            damagedPlayerThisCharge = false;
            nextChargeTime = 0f;
            finishDashNextFixedUpdate = false;
        }

        private void LockChargeDestination(EnemyAIController enemy)
        {
            Vector2 origin = enemy.transform.position;
            Vector2 playerPosition = enemy.player.position;
            chargeDirection = (playerPosition - origin).normalized;

            if (chargeDirection.sqrMagnitude <= 0.001f)
            {
                chargeDirection = enemy.Root != null && enemy.Root.localScale.x < 0f
                    ? Vector2.right
                    : Vector2.left;
            }

            FaceDirection(enemy, chargeDirection);
            ShowChargeRangePreview(enemy, chargeDirection);
        }

        private void BeginDash(EnemyAIController enemy)
        {
            SetPhase(ChargePhase.Dashing);
            traveledChargeDistance = 0f;
            finishDashNextFixedUpdate = false;
            blockedCheckPosition = GetCurrentPosition(enemy);
            nextBlockedCheckTime = Time.time + blockedCheckTime;

            if (enemy.agent != null && enemy.agent.isActiveAndEnabled)
            {
                originalAgentUpdatePosition = enemy.agent.updatePosition;
                enemy.agent.updatePosition = false;
                changedAgentUpdatePosition = true;
            }

            HideChargeRangePreview();
            enemy.PlayAnimation(chargeAnimation);
        }

        private void UpdateDash(EnemyAIController enemy)
        {
            if (finishDashNextFixedUpdate)
            {
                FinishDash(enemy);
                return;
            }

            Vector2 currentPosition = GetCurrentPosition(enemy);
            float remainingDistance = chargeDistance - traveledChargeDistance;

            if (remainingDistance <= arrivalDistance)
            {
                FinishDash(enemy);
                return;
            }

            float moveDistance = Mathf.Min(
                chargeSpeed * Time.fixedDeltaTime,
                remainingDistance);
            moveDistance = GetWallSafeMoveDistance(moveDistance, out bool willHitWall);

            if (moveDistance <= 0f)
            {
                FinishDash(enemy);
                return;
            }

            Vector2 movement = chargeDirection * moveDistance;

            if (enemy.rb != null)
            {
                enemy.rb.MovePosition(currentPosition + movement);
            }
            else
            {
                enemy.transform.position = currentPosition + movement;
            }

            traveledChargeDistance += moveDistance;
            finishDashNextFixedUpdate = willHitWall;

            if (Time.time >= nextBlockedCheckTime)
            {
                Vector2 newPosition = enemy.transform.position;
                if (Vector2.Distance(blockedCheckPosition, newPosition) <= blockedMoveDistance)
                {
                    FinishDash(enemy);
                    return;
                }

                blockedCheckPosition = newPosition;
                nextBlockedCheckTime = Time.time + blockedCheckTime;
            }
        }

        private void FinishDash(EnemyAIController enemy)
        {
            if (phase != ChargePhase.Dashing)
                return;

            enemy.StopMovement();
            finishDashNextFixedUpdate = false;
            RestoreAgentPosition(enemy);
            SetPhase(ChargePhase.Ending);
            enemy.PlayAnimation(chargeEndAnimation);
        }

        private float GetWallSafeMoveDistance(float requestedDistance, out bool willHitWall)
        {
            willHitWall = false;

            if (bodyCollider == null)
            {
                bodyCollider = GetComponent<Collider2D>();
            }

            if (bodyCollider == null || requestedDistance <= 0f)
                return requestedDistance;

            int hitCount = bodyCollider.Cast(
                chargeDirection,
                castHits,
                requestedDistance + wallStopDistance);

            float nearestWallDistance = float.PositiveInfinity;
            for (int i = 0; i < hitCount; i++)
            {
                Collider2D hitCollider = castHits[i].collider;
                if (hitCollider == null ||
                    !hitCollider.CompareTag(Nytherion.Core.Systems.Tags.Wall))
                {
                    continue;
                }

                nearestWallDistance = Mathf.Min(
                    nearestWallDistance,
                    castHits[i].distance);
            }

            if (float.IsPositiveInfinity(nearestWallDistance))
                return requestedDistance;

            willHitWall = true;
            return Mathf.Clamp(
                nearestWallDistance - wallStopDistance,
                0f,
                requestedDistance);
        }

        private static Vector2 GetCurrentPosition(EnemyAIController enemy)
        {
            return enemy.rb != null
                ? enemy.rb.position
                : (Vector2)enemy.transform.position;
        }

        private void RestoreAgentPosition(EnemyAIController enemy)
        {
            if (!changedAgentUpdatePosition || enemy == null || enemy.agent == null)
                return;

            Vector2 physicsPosition = GetCurrentPosition(enemy);
            Vector3 currentPosition = new Vector3(
                physicsPosition.x,
                physicsPosition.y,
                enemy.transform.position.z);
            if (enemy.agent.isActiveAndEnabled && enemy.agent.isOnNavMesh)
            {
                enemy.agent.nextPosition = currentPosition;
                enemy.agent.Warp(currentPosition);
            }

            enemy.agent.updatePosition = originalAgentUpdatePosition;
            changedAgentUpdatePosition = false;
        }

        private void SetPhase(ChargePhase nextPhase)
        {
            phase = nextPhase;
            phaseStartedAt = Time.time;
        }

        private void ShowChargeRangePreview(
            EnemyAIController enemy,
            Vector2 direction)
        {
            if (chargeRangePreview == null || enemy == null ||
                direction.sqrMagnitude <= 0.001f)
            {
                return;
            }

            direction.Normalize();

            Transform previewTransform = chargeRangePreview.transform;
            previewTransform.SetParent(null, true);

            Vector2 origin = GetCurrentPosition(enemy);
            previewTransform.position = origin;
            previewTransform.rotation = Quaternion.Euler(
                0f,
                0f,
                Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);

            previewTransform.localScale = Vector3.one;
            chargeRangePreview.SetActive(true);

            if (previewController == null)
            {
                previewController = chargeRangePreview.GetComponent<GoblinChargePreview>();
            }

            previewController?.Configure(chargeDistance, previewWidth);
        }

        private void HideChargeRangePreview()
        {
            if (chargeRangePreview == null)
                return;

            chargeRangePreview.SetActive(false);

            if (previewOriginalParent != null)
            {
                chargeRangePreview.transform.SetParent(previewOriginalParent, false);
            }
        }

        private void OnDisable()
        {
            HideChargeRangePreview();
        }

        private static void FaceTowardsPlayer(EnemyAIController enemy)
        {
            if (enemy == null || enemy.player == null)
                return;

            Vector2 direction = enemy.player.position - enemy.transform.position;
            FaceDirection(enemy, direction);
        }

        private static void FaceDirection(EnemyAIController enemy, Vector2 direction)
        {
            if (enemy == null || enemy.Root == null || Mathf.Abs(direction.x) <= 0.001f)
                return;

            Vector3 scale = enemy.RootDefaultScale;
            scale.x = direction.x > 0f
                ? -Mathf.Abs(scale.x)
                : Mathf.Abs(scale.x);

            if (enemy.xflip)
            {
                scale.x *= -1f;
            }

            enemy.Root.localScale = scale;
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (phase != ChargePhase.Dashing)
                return;

            PlayerHealth playerHealth = collision.collider.GetComponentInParent<PlayerHealth>();
            PlayerController playerController =
                collision.collider.GetComponentInParent<PlayerController>();

            if (playerHealth != null || playerController != null)
            {
                DamagePlayerOnce(playerHealth);
                KnockPlayerBack(playerController);
                if (activeEnemy != null)
                {
                    finishDashNextFixedUpdate = true;
                }
                return;
            }

            if (collision.collider.CompareTag(Nytherion.Core.Systems.Tags.Wall) &&
                activeEnemy != null)
            {
                finishDashNextFixedUpdate = true;
            }
        }

        private void KnockPlayerBack(PlayerController playerController)
        {
            if (playerController == null || playerController.IsDashing)
                return;

            Vector2 moveDirection = chargeDirection;
            if (moveDirection.sqrMagnitude <= 0.001f)
                return;

            Vector2 toPlayer = ((Vector2)playerController.transform.position -
                (Vector2)transform.position).normalized;

            float cross = moveDirection.x * toPlayer.y -
                moveDirection.y * toPlayer.x;
            float sideSign = Mathf.Abs(cross) > 0.001f
                ? Mathf.Sign(cross)
                : (GetInstanceID() % 2 == 0 ? 1f : -1f);

            Vector2 backward = -moveDirection;
            Vector2 side = new Vector2(-moveDirection.y, moveDirection.x) * sideSign;
            Vector2 knockbackDirection =
                (backward * backwardWeight + side * sideWeight).normalized;

            float knockbackSpeed = Mathf.Clamp(
                chargeSpeed * knockbackMultiplier,
                minimumKnockback,
                maximumKnockback);

            playerController.ApplyKnockback(
                knockbackDirection,
                knockbackSpeed,
                knockbackDuration);
        }

        private void DamagePlayerOnce(PlayerHealth playerHealth)
        {
            if (damagedPlayerThisCharge || playerHealth == null)
                return;

            damagedPlayerThisCharge = true;

            if (enemyBase == null)
            {
                enemyBase = GetComponent<EnemyBase>();
            }

            if (statusEffectManager == null)
            {
                statusEffectManager = GetComponent<StatusEffectManager>();
            }

            if (enemyBase == null || enemyBase.enemyData == null)
                return;

            float damage = enemyBase.enemyData.damageAmount;
            if (statusEffectManager != null)
            {
                damage *= statusEffectManager.GetOutgoingDamageMultiplier();
            }

            playerHealth.TakeDamage(damage);
        }

        private void OnValidate()
        {
            chargeRange = Mathf.Max(0.1f, chargeRange);
            chargeWaitTime = Mathf.Max(0f, chargeWaitTime);
            chargeCooldown = Mathf.Max(0f, chargeCooldown);
            chargeSpeed = Mathf.Max(0.1f, chargeSpeed);
            chargeDistance = Mathf.Max(0.1f, chargeDistance);
            arrivalDistance = Mathf.Max(0.01f, arrivalDistance);
            blockedCheckTime = Mathf.Max(0.01f, blockedCheckTime);
            blockedMoveDistance = Mathf.Max(0f, blockedMoveDistance);
            wallStopDistance = Mathf.Max(0f, wallStopDistance);
            previewWidth = Mathf.Max(0.05f, previewWidth);
            knockbackMultiplier = Mathf.Max(0f, knockbackMultiplier);
            minimumKnockback = Mathf.Max(0f, minimumKnockback);
            maximumKnockback = Mathf.Max(minimumKnockback, maximumKnockback);
            knockbackDuration = Mathf.Max(0f, knockbackDuration);
            backwardWeight = Mathf.Max(0f, backwardWeight);
            sideWeight = Mathf.Max(0f, sideWeight);
        }
    }
}
