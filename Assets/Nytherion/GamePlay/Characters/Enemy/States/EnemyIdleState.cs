using UnityEngine;

namespace Nytherion.GamePlay.Characters.Enemy.States
{
    public class EnemyIdleState : EnemyBaseState
    {
        public EnemyIdleState(EnemyAIController enemyAIController) : base(enemyAIController) { }

        public override void EnterState(EnemyAIController enemy)
        {
            UnityEngine.Debug.Log("기본상태");
            enemy.PlayAnimation("Idle");
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
