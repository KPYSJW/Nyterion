using UnityEngine;
using UnityEngine.AI;
using Nytherion.Core.Systems;
using Nytherion.GamePlay.Combat;
using Nytherion.GamePlay.Characters.Enemy.States;
using System.Collections.Generic;
using Nytherion.Data.ScriptableObjects.Enemy;
using System.Linq;

namespace Nytherion.GamePlay.Characters.Enemy
{
    public class EnemyAIController : MonoBehaviour
    {
        public bool xflip=false;
        public float detectRange;
        public float moveSpeed;
        private bool hasForcedDestination;
        private Vector3 forcedDestination;
        private bool hasForcedFacingDirection;
        private Vector2 forcedFacingDirection;
        public Transform player;
        public NavMeshAgent agent; 
       // public NavMeshObstacle Obstacle;
        public Rigidbody2D rb; 
        private IEnemyMovementBehavior movementBehavior;
        private readonly List<IAttackBehavior> attackBehaviors = new();
        private IAttackSelector attackSelector;
        private IEnemyCombatBehavior customCombatBehavior;
        private IEnemyCombatBehavior combatBehavior;

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

            InitializeMovementSystem();
            InitializeCombatSystem();
            if (movementBehavior == null || combatBehavior == null)
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

            if (hasForcedDestination)
            {
                TryMoveToForcedDestination();
            }

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
        public bool TryMoveToForcedDestination()
        {
            if (!movementAllowed || !hasForcedDestination || movementBehavior == null)
                return false;

            movementBehavior.MoveTowards(forcedDestination);
            return true;
        }
        public void SetForcedFacingDirection(Vector2 direction)
        {
            if (direction.sqrMagnitude <= 0.001f) return;

            forcedFacingDirection = direction.normalized;
            hasForcedFacingDirection = true;
        }

        public void ClearForcedFacingDirection()
        {
            hasForcedFacingDirection = false;
            forcedFacingDirection = Vector2.zero;
        }
        private void UpdateDirection()
        {
            if (Root == null || agent == null) return;

            Vector3 velocity = hasForcedFacingDirection
                ? forcedFacingDirection
                : movementBehavior?.CurrentVelocity ?? agent.desiredVelocity;
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
             if(xflip==true)
            {
                scale.x*=-1;
            }
            Root.localScale = scale;
        }

        public void MoveTowardsPlayer()
        {
            if (!movementAllowed) return;
            if (movementBehavior == null) return;
            if (!hasForcedDestination && player == null) return;

            Vector3 destination = hasForcedDestination
                ? forcedDestination
                : player.position;

            movementBehavior.MoveTowards(destination);

            UpdateDirection();
        }

        public void MoveToTarget(Vector2 targetPosition)
        {
            if (!movementAllowed) return;
            if (movementBehavior == null) return;

            movementBehavior.MoveTowards(targetPosition);

            UpdateDirection();
        }

        public float GetDistanceToPlayer()
        {
            if (player == null) return Mathf.Infinity;
            return Vector2.Distance(transform.position, player.position);
        }

        public void MoveInDirection(Vector2 direction, float distance = 1.5f)
        {
            if (!movementAllowed || movementBehavior == null) return;

            if (direction.sqrMagnitude < 0.001f)
            {
                StopMovement();
                return;
            }

            direction.Normalize();
            Vector2 target = (Vector2)transform.position + direction * distance;

            movementBehavior.MoveTowards(target);

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
            movementBehavior?.StopMovement();
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
            movementBehavior?.Configure(data);
            ConfigureCombatBehavior();
        }

        public void ResetForReuse(EnemyData data)
        {
            if (player == null)
            {
                GameObject playerInstance = GameObject.FindWithTag(Tags.Player);
                if (playerInstance != null)
                {
                    player = playerInstance.transform;
                }
            }

            if (rb == null) rb = GetComponent<Rigidbody2D>();
            if (agent == null) agent = GetComponent<NavMeshAgent>();
            if (movementBehavior == null) InitializeMovementSystem();
            if (combatBehavior == null) InitializeCombatSystem();

            if (player == null || rb == null || agent == null ||
                movementBehavior == null || combatBehavior == null)
            {
                enabled = false;
                return;
            }

            enabled = true;
            movementAllowed = true;
            ClearForcedDestination();
            ClearForcedFacingDirection();
            if (movementBehavior.RequiresNavMeshAgent && !agent.enabled)
            {
                agent.enabled = true;
            }
            else if (!movementBehavior.RequiresNavMeshAgent && agent.enabled)
            {
                agent.enabled = false;
            }
            agent.updateRotation = false;
            agent.updateUpAxis = false;

            ApplyEnemyData(data);
            StopMovement();
            rb.angularVelocity = 0f;

            if (Root != null)
            {
                Root.localScale = RootDefaultScale;
            }

            foreach (IAttackBehavior attackBehavior in attackBehaviors)
            {
                attackBehavior.ResetForReuse();
            }
            combatBehavior.ResetForReuse();

            if (animator != null&&animator.isActiveAndEnabled)
            {
                animator.Rebind();
                animator.Update(0f);
            }

            if (idleState == null) idleState = new EnemyIdleState(this);
            if (chaseState == null) chaseState = new EnemyChaseState(this);
            if (attackState == null) attackState = new EnemyAttackState(this);

            TransitionToState(idleState);
            movementBehavior.ResetForReuse();
        }

        public void PrepareForPoolReturn()
        {
            movementAllowed = false;
            ClearForcedDestination();
            ClearForcedFacingDirection();
            StopMovement();
            foreach (IAttackBehavior attackBehavior in attackBehaviors)
            {
                attackBehavior.ResetForReuse();
            }
            combatBehavior?.ResetForReuse();
        }

        private void InitializeCombatSystem()
        {
            attackBehaviors.Clear();
            attackBehaviors.AddRange(
                GetComponents<MonoBehaviour>().OfType<IAttackBehavior>());

            attackSelector = GetComponents<MonoBehaviour>()
                .OfType<IAttackSelector>()
                .FirstOrDefault();

            customCombatBehavior = GetComponents<MonoBehaviour>()
                .OfType<IEnemyCombatBehavior>()
                .FirstOrDefault();

            ConfigureCombatBehavior();
        }

        private void ConfigureCombatBehavior()
        {
            combatBehavior = customCombatBehavior ??
                EnemyCombatBehaviorFactory.Create(
                    CurrentCombatType,
                    attackBehaviors,
                    attackSelector,
                    HybridSwitchDistance);
        }

        private void InitializeMovementSystem()
        {
            movementBehavior = GetComponents<MonoBehaviour>()
                .OfType<IEnemyMovementBehavior>()
                .FirstOrDefault();

            if (movementBehavior == null && agent != null)
            {
                movementBehavior = new NavMeshChaseMovement(agent, rb);
            }
        }

        public bool ShouldEnterAttackState()
        {
            return combatBehavior != null &&
                   combatBehavior.ShouldEnterAttackState(this);
        }

        public void EnterAttackState()
        {
            combatBehavior?.EnterAttackState(this);
        }

        public void UpdateAttackState()
        {
            combatBehavior?.UpdateAttackState(this);
        }

        public void ExitAttackState()
        {
            combatBehavior?.ExitAttackState(this);
        }


        

        public void PlayAnimation(string stateName)
        {
            if (animator == null || !animator.isActiveAndEnabled ||
                animator.layerCount == 0 || string.IsNullOrEmpty(stateName))
                return;

            int stateHash = Animator.StringToHash(stateName);
            if (animator.HasState(0, stateHash))
            {
                animator.Play(stateHash);
                return;
            }

            int fullPathHash = Animator.StringToHash($"Base Layer.{stateName}");
            if (animator.HasState(0, fullPathHash))
            {
                animator.Play(fullPathHash);
            }
        }

        public bool IsAnimationFinished(string stateName)
        {
            if (animator == null || !animator.isActiveAndEnabled)
                return true;

            AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
            return stateInfo.IsName(stateName) &&
                   stateInfo.normalizedTime >= 1f &&
                   !animator.IsInTransition(0);
        }
        

        public void MoveAwayFromPlayer(float retreatDistance = 4f)
        {
            if (!movementAllowed) return;
            if (player == null || movementBehavior == null) return;

            Vector2 direction = ((Vector2)transform.position - (Vector2)player.position).normalized;
            Vector2 target = (Vector2)transform.position + direction * retreatDistance;

            movementBehavior.MoveTowards(target);

            UpdateDirection();
        }
    }
}
