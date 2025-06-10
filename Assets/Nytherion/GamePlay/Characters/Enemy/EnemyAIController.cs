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

        public Transform player; // Made public for states
        public Rigidbody2D rb; // Made public for states
        public IAttackBehavior attackBehavior; // Made public for states

        private EnemyBaseState _currentState;
        public EnemyIdleState idleState;
        public EnemyChaseState chaseState;
        public EnemyAttackState attackState;

        private void Awake()
        {
            var playerInstance = Nytherion.GamePlay.Characters.Player.Player.Instance;
            if (playerInstance == null)
            {
                Debug.LogError("Player instance not found. Make sure a Player exists in the scene.");
                enabled = false;
                return;
            }
            
            player = playerInstance.transform;
            rb = GetComponent<Rigidbody2D>();
            attackBehavior = GetComponent<IAttackBehavior>();

            if (rb == null)
            {
                Debug.LogError($"{name}: Rigidbody2D component is required.", this);
                enabled = false;
                return;
            }

            if (attackBehavior == null)
            {
                Debug.LogError($"{name}: IAttackBehavior component is required.", this);
                enabled = false;
                return;
            }

            // Initialize states after all validations
            idleState = new EnemyIdleState(this);
            chaseState = new EnemyChaseState(this);
            attackState = new EnemyAttackState(this);

            if (rb == null)
            {
                Debug.LogError("Rigidbody2D not found. Enemy AI will not function.");
                enabled = false;
                return;
            }

            if (attackBehavior == null)
            {
                Debug.LogError("IAttackBehavior component not found. Enemy AI will not function.");
                enabled = false;
                return;
            }
            
            _currentState = idleState;
            _currentState.EnterState(this);
        }

        private void Update()
        {
            _currentState.UpdateState(this);
        }

        public void TransitionToState(EnemyBaseState newState)
        {
            if (_currentState != null)
            {
                _currentState.ExitState(this);
            }
            _currentState = newState;
            _currentState.EnterState(this);
        }

        public void MoveTowardsPlayer()
        {
            Vector2 direction = (player.position - transform.position).normalized;
            rb.velocity = direction * moveSpeed;
        }
    }
}