using UnityEngine;

namespace Nytherion.GamePlay.Characters.Enemy
{
    public class EnemyAttackState : EnemyBaseState
    {
        public EnemyAttackState(EnemyAIController enemyAIController) : base(enemyAIController) { }

        public override void EnterState(EnemyAIController enemy)
        {
        }

        public override void UpdateState(EnemyAIController enemy)
        {
            enemy.attackBehavior.TryAttack(enemy.player);

            if (!enemy.attackBehavior.IsInAttackRange(enemy.player))
            {
                enemy.TransitionToState(enemy.chaseState);
            }
        }

        public override void ExitState(EnemyAIController enemy)
        {
        }
    }
}
