using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Nytherion.GamePlay.Characters.Player;

namespace Nytherion.GamePlay.Characters.Enemy
{
    using Nytherion.GamePlay.Combat;
    public class EnemyAIController : MonoBehaviour
    {
        public float detectRange = 8f;
        public float moveSpeed = 2f;

        public Transform player; 
        public Rigidbody2D rb; 
        public IAttackBehavior attackBehavior; 

        private EnemyBaseState currentState;
        public EnemyIdleState idleState;
        public EnemyChaseState chaseState;
        public EnemyAttackState attackState;

        private void Awake()
        {
            var playerInstance = GameObject.FindWithTag("Player");
            if (playerInstance == null)
            {
                enabled = false;
                return;
            }
            
            player = playerInstance.transform;
            rb = GetComponent<Rigidbody2D>();
            attackBehavior = GetComponent<IAttackBehavior>();

            if (rb == null)
            {
                enabled = false;
                return;
            }

            if (attackBehavior == null)
            {
                enabled = false;
                return;
            }

            idleState = new EnemyIdleState(this);
            chaseState = new EnemyChaseState(this);
            attackState = new EnemyAttackState(this);

            if (rb == null)
            {
                enabled = false;
                return;
            }

            if (attackBehavior == null)
            {
                enabled = false;
                return;
            }
            
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
    }
}