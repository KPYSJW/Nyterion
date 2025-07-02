using UnityEngine;

namespace Nytherion.GamePlay.Characters.Enemy
{
    public class EnemyIdleState : EnemyBaseState
    {
        public EnemyIdleState(EnemyAIController enemyAIController) : base(enemyAIController) { }

        public override void EnterState(EnemyAIController enemy)
        {
        }

        public override void UpdateState(EnemyAIController enemy)
        {
            if (Vector3.Distance(enemy.transform.position, enemy.player.position) < enemy.detectRange)
            {
                enemy.TransitionToState(enemy.chaseState);
            }
        }

        public override void ExitState(EnemyAIController enemy)
        {
        }
    }
}
