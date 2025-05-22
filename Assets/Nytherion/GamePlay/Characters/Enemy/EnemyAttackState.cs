using UnityEngine;

namespace Nytherion.GamePlay.Characters.Enemy
{
    public class EnemyAttackState : EnemyBaseState
    {
        public EnemyAttackState(EnemyAIController enemyAIController) : base(enemyAIController) { }

        public override void EnterState(EnemyAIController enemy)
        {
            // Initialization logic for Attack state, if any
            Debug.Log("Entering Attack State");
        }

        public override void UpdateState(EnemyAIController enemy)
        {
            enemy.attackBehavior.TryAttack(enemy.player);

            // Transition to ChaseState if player is not in attack range
            if (!enemy.attackBehavior.IsInAttackRange(enemy.player))
            {
                enemy.TransitionToState(enemy.chaseState);
            }
        }

        public override void ExitState(EnemyAIController enemy)
        {
            // Cleanup logic for Attack state, if any
            Debug.Log("Exiting Attack State");
        }
    }
}
