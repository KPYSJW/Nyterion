using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace Game.Characters.Enemy
{
    using Game.Combat;
    public class EnemyAIController : MonoBehaviour
    {
        public enum EnemyState { Idle, Chase, Attack }
        public EnemyState currentState = EnemyState.Idle;

        public float detectRange = 8f;
        public float moveSpeed = 2f;

        private Transform player;
        private Rigidbody2D rb;
        private IAttackBehavior attackBehavior;

        private void Awake()
        {
            player = GameObject.Find("Player").transform;
            rb = GetComponent<Rigidbody2D>();
            attackBehavior = GetComponent<IAttackBehavior>();
        }

        private void Update()
        {
            switch (currentState)
            {
                case EnemyState.Idle:
                    if (Vector2.Distance(transform.position, player.position) <= detectRange)
                    {
                        currentState = EnemyState.Chase;
                    }
                    break;
                case EnemyState.Chase:
                    if (attackBehavior != null && attackBehavior.IsInAttackRange(player))
                    {
                        rb.velocity = Vector2.zero;
                        currentState = EnemyState.Attack;
                    }
                    else
                    {
                        MoveTowardsPlayer();
                    }
                    break;
                case EnemyState.Attack:
                    if (attackBehavior != null)
                    {
                        attackBehavior.TryAttack(player);
                        if (!attackBehavior.IsInAttackRange(player))
                        {
                            currentState = EnemyState.Chase;
                        }
                    }
                    break;
            }
        }

        private void MoveTowardsPlayer()
        {
            Vector2 direction = (player.position - transform.position).normalized;
            rb.velocity = direction * moveSpeed;
        }
    }
}