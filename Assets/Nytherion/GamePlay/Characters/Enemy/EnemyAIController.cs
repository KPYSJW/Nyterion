using UnityEngine;
using UnityEngine.AI;
using Nytherion.Core.Systems;
using Nytherion.GamePlay.Combat;
using Nytherion.GamePlay.Characters.Enemy.States;
using System.Collections.Generic;
using Nytherion.Data.ScriptableObjects.Enemy;
using Nytherion.GamePlay.Combat.Behaviors;
using System.Linq;

namespace Nytherion.GamePlay.Characters.Enemy
{
    public class EnemyAIController : MonoBehaviour
    {
        public float detectRange;
        public float moveSpeed;
        private bool hasForcedDestination;
        private Vector3 forcedDestination;
        public Transform player;
        public NavMeshAgent agent; 
       // public NavMeshObstacle Obstacle;
        public Rigidbody2D rb; 
        private MeleeAttackBehavior meleeAttack;

        private RangedAttackBehavior rangedAttack;

        public bool HasMeleeAttack=>meleeAttack!=null;
        public bool HasRangedAttack=>rangedAttack!=null;

        private bool movementAllowed = true;

        private EnemyBaseState currentState;
        public EnemyIdleState idleState;
        public EnemyChaseState chaseState;
        public EnemyAttackState attackState;
        public Animator animator;
        //public SpriteRenderer spriteRenderer;
        public Transform Root;
        public Vector2 RootDefaultScale;
        public EnemyCombatType CurrentCombatType;

        public EnemyData enemyData;
        public float HybridSwitchDistance;
        public float TooCloseDistance => 4f;
        private void Awake()
        {
            var playerInstance = GameObject.FindWithTag(Tags.Player);
            if (playerInstance == null)
            {
                enabled = false;
                return;
            }
            RootDefaultScale=Root.localScale;
            player = playerInstance.transform;
            rb = GetComponent<Rigidbody2D>();
            agent = GetComponent<NavMeshAgent>();
            //Obstacle=GetComponent<NavMeshObstacle>();
            if (rb == null||agent==null)
            {
                enabled = false;
                return;
            }
             agent.updateRotation = false;
             agent.updateUpAxis = false;

            InitializeAttackSystems();
            if (!HasMeleeAttack && !HasRangedAttack)
            {
                enabled = false;
                return;
            }

           

            idleState = new EnemyIdleState(this);
            chaseState = new EnemyChaseState(this);
            attackState = new EnemyAttackState(this);

            
            
            currentState = idleState;
            currentState.EnterState(this);
        }

        private void Update()
        {
            currentState.UpdateState(this);
            UpdateDirection();
        }

        public void TransitionToState(EnemyBaseState newState)
        {
            if (currentState != null)
            {
                currentState.ExitState(this);
            }
            currentState = newState;
            currentState.EnterState(this);
        }
        public void SetForcedDestination(Vector3 destination)
        {
            forcedDestination = destination;
            hasForcedDestination = true;
        }

        public void ClearForcedDestination()
        {
            hasForcedDestination = false;
        }
        private void UpdateDirection()
        {
            if (Root == null || agent == null) return;

            Vector3 velocity = agent.desiredVelocity;
            if (velocity.sqrMagnitude <= 0.01f) return;

            Vector3 scale = RootDefaultScale;

            if (velocity.x > 0f)
            {
                scale.x = -Mathf.Abs(scale.x);
            }
            else
            {
                scale.x = Mathf.Abs(scale.x);
            }

            Root.localScale = scale;
        }

        public void MoveTowardsPlayer()
        {
            if (!movementAllowed) return;
            if (agent == null || !agent.isOnNavMesh) return;

            agent.isStopped = false;

            if (hasForcedDestination)
            {
                agent.SetDestination(forcedDestination);
            }
            else
            {
                if (player == null) return;

                agent.SetDestination(player.position);
            }

            UpdateDirection();
        }

        public void MoveToTarget(Vector2 targetPosition)
        {
            if (!movementAllowed) return;
            if (!agent.isOnNavMesh) return;

            agent.isStopped = false;
            agent.SetDestination(targetPosition);

            UpdateDirection();
        }

        public float GetDistanceToPlayer()
        {
            if (player == null) return Mathf.Infinity;
            return Vector2.Distance(transform.position, player.position);
        }

        public void MoveInDirection(Vector2 direction, float distance = 1.5f)
        {
            if (!movementAllowed) return;
            if (!agent.isOnNavMesh) return;

            if (direction.sqrMagnitude < 0.001f)
            {
                StopMovement();
                return;
            }

            direction.Normalize();
            Vector2 target = (Vector2)transform.position + direction * distance;

            agent.isStopped = false;
            agent.SetDestination(target);

            UpdateDirection();
        }

        
       /* public Vector2 GetMeleeChaseDirection(float sideBiasWeight = 0.15f)
        {
            if (player == null) return Vector2.zero;

            Vector2 toPlayer = ((Vector2)player.position - (Vector2)transform.position).normalized;
            Vector2 tangent = new Vector2(-toPlayer.y, toPlayer.x);

            float sideSign = (GetInstanceID() % 2 == 0) ? 1f : -1f;

            Vector2 finalDirection = (toPlayer + tangent * sideBiasWeight * sideSign).normalized;
            return finalDirection;
        }

        public Vector2 GetMeleeSlotTarget(float forwardOffset = 1.2f, float sideOffset = 0.8f)
        {
            if (player == null) return transform.position;

            Vector2 toPlayer = ((Vector2)player.position - (Vector2)transform.position).normalized;
            Vector2 side = new Vector2(-toPlayer.y, toPlayer.x);

            float sideSign = (GetInstanceID() % 2 == 0) ? 1f : -1f;

            return (Vector2)player.position - toPlayer * forwardOffset + side * sideOffset * sideSign;
        }

        public bool IsFrontBlocked(float checkRadius = 0.6f, float forwardDistance = 0.8f)
        {
            if (player == null) return false;

            Vector2 toPlayer = ((Vector2)player.position - (Vector2)transform.position).normalized;
            Vector2 checkCenter = (Vector2)transform.position + toPlayer * forwardDistance;

            Collider2D[] hits = Physics2D.OverlapCircleAll(checkCenter, checkRadius);

            foreach (var hit in hits)
            {
                if (hit.gameObject == gameObject) continue;
                if (!hit.CompareTag(Tags.Enemy)) continue;

                float myDistance = GetDistanceToPlayer();
                float otherDistance = Vector2.Distance(hit.transform.position, player.position);

                if (otherDistance < myDistance)
                {
                    return true;
                }
            }

            return false;
        }

        public Vector2 GetBlockedFlowDirection(float sideWeight = 0.8f)
        {
            if (player == null) return Vector2.zero;

            Vector2 toPlayer = ((Vector2)player.position - (Vector2)transform.position).normalized;
            Vector2 side = new Vector2(-toPlayer.y, toPlayer.x);
            float sideSign = (GetInstanceID() % 2 == 0) ? 1f : -1f;

            return (toPlayer * 0.2f + side * sideWeight * sideSign).normalized;
        }
        public Vector2 GetSeparationDirection(float separationRadius)
        {
            Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, separationRadius);

            Vector2 separation = Vector2.zero;

            foreach (var hit in hits)
            {
                if (hit.gameObject == gameObject) continue;
                if (!hit.CompareTag("Enemy")) continue;

                Vector2 away = (Vector2)(transform.position - hit.transform.position);
                float distance = away.magnitude;

                if (distance > 0.001f)
                {
                    separation += away.normalized / distance;
                }
            }

            return separation.normalized;
        }

        public Vector2 GetCloseFlowDirection(float flowWeight = 0.35f)
        {
            if (player == null) return Vector2.zero;

            Vector2 toPlayer = ((Vector2)player.position - (Vector2)transform.position).normalized;
            Vector2 tangent = new Vector2(-toPlayer.y, toPlayer.x);

            float sideSign = (GetInstanceID() % 2 == 0) ? 1f : -1f;

            Vector2 flowDirection = (toPlayer * (1f - flowWeight) + tangent * flowWeight * sideSign).normalized;
            return flowDirection;
        }

        public Vector2 GetSurroundPosition(float radius, float sideOffset)
        {
            if (player == null) return transform.position;

            Vector2 toEnemy = ((Vector2)transform.position - (Vector2)player.position).normalized;

            if (toEnemy == Vector2.zero)
                toEnemy = Vector2.right;

            Vector2 tangent = new Vector2(-toEnemy.y, toEnemy.x);

            float sideSign = (GetInstanceID() % 2 == 0) ? 1f : -1f;//랜덤방향

            Vector2 target =
                (Vector2)player.position +
                toEnemy * radius +
                tangent * sideOffset * sideSign;

            return target;
        }*/

        public void StopMovement()
        {
             if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
            {
                agent.isStopped = true;
                agent.ResetPath();
            }
            if (rb != null)
            {
                rb.velocity = Vector2.zero;
            }
        }

        public void SetMovementAllowed(bool allowed)
        {
            movementAllowed = allowed;

            if (!movementAllowed)
            {
                StopMovement();
            }
        }

        public void ApplyEnemyData(EnemyData data)
        {
            if(data==null)return;
            enemyData=data;
            CurrentCombatType = data.combatType;
            HybridSwitchDistance = data.hybridSwitchDistance;
            moveSpeed=data.moveSpeed;
            detectRange=data.detectRange;
             if (agent != null)
            {
                agent.speed = data.moveSpeed;
            }
        }

        private void InitializeAttackSystems()
        {
            meleeAttack=GetComponent<MeleeAttackBehavior>();
            rangedAttack=GetComponent<RangedAttackBehavior>();
        }

        public bool CanUseMeleeAttack()
        {
            return player!=null && meleeAttack!=null && meleeAttack.IsInAttackRange(player);
        }
        public bool CanUseRangedAttack()
        {
            return player!=null && rangedAttack!=null && rangedAttack.IsInAttackRange(player);
        }

        public bool IsMeleeAttackReady()
        {
            return meleeAttack != null && meleeAttack.AttackCoolDown >= 1f;
        }

        public bool IsRangedAttackReady()
        {
            return rangedAttack != null && rangedAttack.AttackCoolDown >= 1f;
        }

        public bool TryMeleeAttack()
        {
            if (player == null || meleeAttack == null) return false;
            return meleeAttack.TryAttack(player);
        }

        public bool TryRangedAttack()
        {
            if (player == null || rangedAttack == null) return false;
            return rangedAttack.TryAttack(player);
        }

        public bool CanAttackPlayer()
        {
            switch (CurrentCombatType)
            {
                case EnemyCombatType.Melee:
                    return CanUseMeleeAttack();

                case EnemyCombatType.Ranged:
                    return CanUseRangedAttack();

                case EnemyCombatType.Hybrid:
                    return CanUseMeleeAttack() || CanUseRangedAttack();

                default:
                    return false;
            }
        }

        public bool TryAttackPlayer()
        {
            switch (CurrentCombatType)
            {
                case EnemyCombatType.Melee:
                    return TryMeleeAttack();

                case EnemyCombatType.Ranged:
                    return TryRangedAttack();

                case EnemyCombatType.Hybrid:
                    if (CanUseMeleeAttack()) return TryMeleeAttack();
                    if (CanUseRangedAttack()) return TryRangedAttack();
                    return false;

                default:
                    return false;
            }
        }


        

        public void PlayAnimation(string stateName)
        {
            animator.Play(stateName);
        }
        

        public void MoveAwayFromPlayer(float retreatDistance = 4f)
        {
            if (!movementAllowed) return;
            if (player == null || !agent.isOnNavMesh) return;

            Vector2 direction = ((Vector2)transform.position - (Vector2)player.position).normalized;
            Vector2 target = (Vector2)transform.position + direction * retreatDistance;

            agent.isStopped = false;
            agent.SetDestination(target);

            UpdateDirection();
        }
    }
}