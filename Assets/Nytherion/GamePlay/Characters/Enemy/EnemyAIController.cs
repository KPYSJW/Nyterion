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
        public float detectRange = 8f;
        public float moveSpeed = 2f;

        public Transform player;
        public NavMeshAgent agent; 
       // public NavMeshObstacle Obstacle;
        public Rigidbody2D rb; 
        private List<IAttackBehavior> attackBehaviors=new List<IAttackBehavior>(); 
        private IAttackSelector attackSelector;

        private EnemyBaseState currentState;
        public EnemyIdleState idleState;
        public EnemyChaseState chaseState;
        public EnemyAttackState attackState;
        public Animator animator;
        public SpriteRenderer spriteRenderer;
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
            if (attackBehaviors.Count == 0)
            {
                enabled = false;
                return;
            }

            if(attackBehaviors.Count>1 && attackSelector==null)
            {
                Debug.LogError($"[{name}] attackBehavior가 여러 개인데 IAttackSelector가 없습니다.");
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
            UpdateSpriteDirection();
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

        private void UpdateSpriteDirection()
        {
            if (spriteRenderer == null || agent == null) return;

            Vector3 velocity = agent.desiredVelocity;
            if (velocity.sqrMagnitude > 0.01f)
            {
                spriteRenderer.flipX = velocity.x > 0f;
            }
        }

        public void MoveTowardsPlayer()
        {
            if (player == null || !agent.isOnNavMesh) return;

            agent.isStopped = false;
            agent.SetDestination(player.position);

            UpdateSpriteDirection();
            
        }

        public void MoveToTarget(Vector2 targetPosition)
        {
            if (!agent.isOnNavMesh) return;

            agent.isStopped = false;
            agent.SetDestination(targetPosition);

            UpdateSpriteDirection();
        }

        public float GetDistanceToPlayer()
        {
            if (player == null) return Mathf.Infinity;
            return Vector2.Distance(transform.position, player.position);
        }

        public void MoveInDirection(Vector2 direction, float distance = 1.5f)
        {
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

            UpdateSpriteDirection();
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
            if (agent == null) return;

            agent.isStopped = true;
            agent.ResetPath();

            if (rb != null)
            {
                rb.velocity = Vector2.zero;
            }
        }

        public bool CanAttackPlayer()
        {
            if(player==null)return false;
           /* if(enemyData.combatType==EnemyCombatType.Ranged)
            {
                if(GetDistanceToPlayer()<TooCloseDistance)
                {
                    return false;
                }
            }*/
            IAttackBehavior selected=SelectAttackBehavior();
            return selected != null && selected.IsInAttackRange(player);
        }

        public bool TryAttackPlayer()
        {
            if(player==null)return false;
            IAttackBehavior selected = SelectAttackBehavior();
            return selected != null && selected.TryAttack(player);
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
            if(attackSelector is HybridAttackSelector hybridAttackSelector)
            {
                hybridAttackSelector.SetSwitchDistance(data.hybridSwitchDistance);
            }
        }

        private void InitializeAttackSystems()
        {
            attackBehaviors=GetComponents<MonoBehaviour>()
            .OfType<IAttackBehavior>()
            .ToList();

            attackSelector=GetComponents<MonoBehaviour>()
            .OfType<IAttackSelector>()
            .FirstOrDefault();
        }

        private IAttackBehavior SelectAttackBehavior()
        {
            if (attackBehaviors == null || attackBehaviors.Count == 0) return null;
            if (attackBehaviors.Count == 1) return attackBehaviors[0];
            if (attackSelector == null) return null;

            return attackSelector.SelectAttackBehavior(transform, player, attackBehaviors);
        }

        public void PlayAnimation(string stateName)
        {
            animator.Play(stateName);
        }
        

        public void MoveAwayFromPlayer(float retreatDistance = 4f)
        {
            if (player == null || !agent.isOnNavMesh) return;

            Vector2 direction = ((Vector2)transform.position - (Vector2)player.position).normalized;
            Vector2 target = (Vector2)transform.position + direction * retreatDistance;

            agent.isStopped = false;
            agent.SetDestination(target);

            UpdateSpriteDirection();
        }
    }
}