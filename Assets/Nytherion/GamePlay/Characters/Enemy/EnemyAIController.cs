using UnityEngine;
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
        public Rigidbody2D rb; 
        private List<IAttackBehavior> attackBehaviors=new List<IAttackBehavior>(); 
        private IAttackSelector attackSelector;

        private EnemyBaseState currentState;
        public EnemyIdleState idleState;
        public EnemyChaseState chaseState;
        public EnemyAttackState attackState;

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
            

            if (rb == null)
            {
                enabled = false;
                return;
            }

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

        public void MoveTowardsPlayer()
        {
            Vector2 direction = (player.position - transform.position).normalized;
            rb.velocity = direction * moveSpeed;
        }

        public void StopMovement()
        {
            rb.velocity=Vector2.zero;
        }

        public bool CanAttackPlayer()
        {
            if(player==null)return false;
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

            moveSpeed=data.moveSpeed;
            detectRange=data.detectRange;

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
    }
}