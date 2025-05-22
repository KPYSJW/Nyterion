using UnityEngine;

namespace Nytherion.GamePlay.Characters.Enemy
{
    public class EnemyIdleState : EnemyBaseState
    {
        public EnemyIdleState(EnemyAIController enemyAIController) : base(enemyAIController) { }

        public override void EnterState(EnemyAIController enemy)
        {
            // Initialization logic for Idle state, if any
            Debug.Log("Entering Idle State");
        }

        public override void UpdateState(EnemyAIController enemy)
        {
            // Transition to ChaseState if player is within detectRange
            if (Vector3.Distance(enemy.transform.position, enemy.player.position) < enemy.detectRange)
            {
                enemy.TransitionToState(enemy.chaseState);
            }
        }

        public override void ExitState(EnemyAIController enemy)
        {
            // Cleanup logic for Idle state, if any
            Debug.Log("Exiting Idle State");
        }
    }
}
