using System.Collections.Generic;
using Nytherion.Core.Systems;
using Nytherion.GamePlay.Characters.Enemy;
using UnityEngine;

namespace Nytherion.GamePlay.Characters.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerController))]
    public sealed class PlayerEnemySoftPush : MonoBehaviour
    {
        [Header("Soft Push")]
        [SerializeField, Min(0.1f)] private float detectionRadius = 0.9f;
        [SerializeField, Min(0f)] private float pushDistance = 1.25f;
        [SerializeField, Range(-1f, 1f)] private float minimumInputAlignment = 0.1f;
        [SerializeField, Min(0f)] private float minimumCenterDistance = 0.2f;
        [SerializeField, Min(0.1f)] private float crowdDetectionRadius = 2.25f;
        [SerializeField, Min(0.1f)] private float enemySpacing = 0.9f;
        [SerializeField, Min(0f)] private float separationWeight = 0.8f;
        [SerializeField, Range(0f, 1f)] private float chainedPushMultiplier = 0.75f;

        private readonly Collider2D[] overlapResults = new Collider2D[64];
        private readonly HashSet<EnemyAIController> uniqueEnemies = new();
        private readonly HashSet<EnemyAIController> clusterEnemies = new();
        private readonly HashSet<EnemyAIController> forcedEnemies = new();
        private readonly List<EnemyAIController> nearbyEnemies = new(32);
        private readonly Queue<EnemyAIController> clusterSearch = new();
        private PlayerController playerController;

        private void Awake()
        {
            playerController = GetComponent<PlayerController>();
        }

        private void FixedUpdate()
        {
            ReleaseForcedDestinations();

            if (playerController == null ||
                playerController.IsKnockedBack ||
                playerController.IsDashing)
            {
                return;
            }

            Vector2 moveInput = playerController.MoveInput;
            if (moveInput.sqrMagnitude <= 0.01f)
                return;

            moveInput.Normalize();
            Vector2 playerPosition = transform.position;
            int overlapCount = Physics2D.OverlapCircleNonAlloc(
                playerPosition,
                crowdDetectionRadius,
                overlapResults);

            uniqueEnemies.Clear();
            clusterEnemies.Clear();
            nearbyEnemies.Clear();
            clusterSearch.Clear();

            for (int i = 0; i < overlapCount; i++)
            {
                Collider2D hit = overlapResults[i];
                if (hit == null) continue;

                EnemyAIController enemy =
                    hit.GetComponentInParent<EnemyAIController>();
                if (enemy == null || !enemy.isActiveAndEnabled || !uniqueEnemies.Add(enemy))
                    continue;

                nearbyEnemies.Add(enemy);

                if (IsDirectlyPushed(enemy, playerPosition, moveInput))
                {
                    clusterEnemies.Add(enemy);
                    clusterSearch.Enqueue(enemy);
                }
            }

            FindConnectedEnemies();

            foreach (EnemyAIController enemy in clusterEnemies)
            {
                PushClusterEnemy(
                    enemy,
                    playerPosition,
                    clusterEnemies.Contains(enemy) &&
                    IsDirectlyPushed(enemy, playerPosition, moveInput));
            }
        }

        private bool IsDirectlyPushed(
            EnemyAIController enemy,
            Vector2 playerPosition,
            Vector2 moveInput)
        {
            Vector2 enemyPosition = enemy.rb != null
                ? enemy.rb.position
                : (Vector2)enemy.transform.position;
            Vector2 awayFromPlayer = enemyPosition - playerPosition;
            float centerDistance = awayFromPlayer.magnitude;

            if (centerDistance <= 0.001f)
            {
                awayFromPlayer = moveInput;
                centerDistance = 0f;
            }
            else
            {
                awayFromPlayer /= centerDistance;
            }

            float inputAlignment = Vector2.Dot(moveInput, awayFromPlayer);
            return centerDistance <= detectionRadius &&
                   inputAlignment >= minimumInputAlignment;
        }

        private void FindConnectedEnemies()
        {
            float connectionDistance = enemySpacing * 1.15f;
            float connectionDistanceSquared = connectionDistance * connectionDistance;

            while (clusterSearch.Count > 0)
            {
                EnemyAIController current = clusterSearch.Dequeue();
                Vector2 currentPosition = GetEnemyPosition(current);

                foreach (EnemyAIController candidate in nearbyEnemies)
                {
                    if (candidate == null || clusterEnemies.Contains(candidate))
                        continue;

                    Vector2 candidatePosition = GetEnemyPosition(candidate);
                    if ((candidatePosition - currentPosition).sqrMagnitude >
                        connectionDistanceSquared)
                    {
                        continue;
                    }

                    clusterEnemies.Add(candidate);
                    clusterSearch.Enqueue(candidate);
                }
            }
        }

        private void PushClusterEnemy(
            EnemyAIController enemy,
            Vector2 playerPosition,
            bool isDirectlyPushed)
        {
            if (IsUsingSpecialMovement(enemy))
                return;

            Vector2 enemyPosition = GetEnemyPosition(enemy);
            Vector2 awayFromPlayer = enemyPosition - playerPosition;
            float centerDistance = awayFromPlayer.magnitude;

            if (centerDistance <= 0.001f)
            {
                awayFromPlayer = Vector2.right;
                centerDistance = 0f;
            }
            else
            {
                awayFromPlayer /= centerDistance;
            }

            Vector2 separation = CalculateEnemySeparation(enemy, enemyPosition);
            Vector2 pushDirection =
                (awayFromPlayer + separation * separationWeight).normalized;

            float proximityStrength = 1f - Mathf.InverseLerp(
                minimumCenterDistance,
                crowdDetectionRadius,
                centerDistance);
            float chainStrength = isDirectlyPushed ? 1f : chainedPushMultiplier;
            float moveDistance = pushDistance * proximityStrength * chainStrength;

            if (moveDistance <= 0f)
                return;

            Vector2 pushDestination = enemyPosition + pushDirection * moveDistance;
            enemy.SetForcedDestination(pushDestination);
            enemy.SetForcedFacingDirection(playerPosition - enemyPosition);
            forcedEnemies.Add(enemy);
        }

        private Vector2 CalculateEnemySeparation(
            EnemyAIController enemy,
            Vector2 enemyPosition)
        {
            Vector2 separation = Vector2.zero;

            foreach (EnemyAIController other in nearbyEnemies)
            {
                if (other == null || other == enemy)
                    continue;

                Vector2 difference = enemyPosition - GetEnemyPosition(other);
                float distance = difference.magnitude;
                if (distance <= 0.001f || distance >= enemySpacing)
                    continue;

                separation += difference.normalized *
                    (1f - distance / enemySpacing);
            }

            return separation.sqrMagnitude > 0.001f
                ? separation.normalized
                : Vector2.zero;
        }

        private static Vector2 GetEnemyPosition(EnemyAIController enemy)
        {
            return enemy.rb != null
                ? enemy.rb.position
                : (Vector2)enemy.transform.position;
        }

        private static bool IsUsingSpecialMovement(EnemyAIController enemy)
        {
            return enemy.agent != null &&
                   enemy.agent.isActiveAndEnabled &&
                   enemy.agent.isOnNavMesh &&
                   !enemy.agent.updatePosition;
        }

        private void ReleaseForcedDestinations()
        {
            foreach (EnemyAIController enemy in forcedEnemies)
            {
                if (enemy != null)
                {
                    enemy.ClearForcedDestination();
                    enemy.ClearForcedFacingDirection();
                }
            }

            forcedEnemies.Clear();
        }

        private void OnDisable()
        {
            ReleaseForcedDestinations();
        }

        private void OnValidate()
        {
            detectionRadius = Mathf.Max(0.1f, detectionRadius);
            pushDistance = Mathf.Max(0f, pushDistance);
            crowdDetectionRadius = Mathf.Max(detectionRadius, crowdDetectionRadius);
            enemySpacing = Mathf.Max(0.1f, enemySpacing);
            separationWeight = Mathf.Max(0f, separationWeight);
            minimumCenterDistance = Mathf.Clamp(
                minimumCenterDistance,
                0f,
                detectionRadius);
        }
    }
}
