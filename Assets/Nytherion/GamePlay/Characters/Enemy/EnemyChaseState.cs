using UnityEngine;

namespace Nytherion.GamePlay.Characters.Enemy
{
    public class EnemyChaseState : EnemyBaseState
    {
        public EnemyChaseState(EnemyAIController enemyAIController) : base(enemyAIController) { }

        public override void EnterState(EnemyAIController enemy)
        {
            // Initialization logic for Chase state, if any
            Debug.Log("Entering Chase State");
        }

        public override void UpdateState(EnemyAIController enemy)
        {
            // Transition to AttackState if player is in attack range
            if (enemy.attackBehavior.IsInAttackRange(enemy.player))
            {
                enemy.TransitionToState(enemy.attackState);
            }
            else
            {
                enemy.MoveTowardsPlayer();
            }
        }

        public override void ExitState(EnemyAIController enemy)
        {
            // Cleanup logic for Chase state, if any
            Debug.Log("Exiting Chase State");
        }
    }
}
