using UnityEngine;

namespace Nytherion.GamePlay.Characters.Enemy.States
{
    public class EnemyIdleState : EnemyBaseState
    {
        public EnemyIdleState(EnemyAIController enemyAIController) : base(enemyAIController) { }

        public override void EnterState(EnemyAIController enemy)
        {
            enemy.PlayAnimation("Idle");
        }

        public override void UpdateState(EnemyAIController enemy)
        {
            if (enemy.CurrentTarget != null && (enemy.CurrentTarget != enemy.player ||
                Vector3.Distance(enemy.transform.position, enemy.CurrentTarget.position) < enemy.detectRange))
            {
                enemy.TransitionToState(enemy.chaseState);
            }
        }

        public override void ExitState(EnemyAIController enemy)
        {
        }
    }
}
