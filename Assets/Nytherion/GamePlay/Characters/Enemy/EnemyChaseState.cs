using UnityEngine;

namespace Nytherion.GamePlay.Characters.Enemy
{
    public class EnemyChaseState : EnemyBaseState
    {
        public EnemyChaseState(EnemyAIController enemyAIController) : base(enemyAIController) { }

        public override void EnterState(EnemyAIController enemy)
        {
        }

        public override void UpdateState(EnemyAIController enemy)
        {
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
        }
    }
}
