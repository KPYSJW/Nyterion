namespace Nytherion.GamePlay.Characters.Enemy.States
{
    public class EnemyAttackState : EnemyBaseState
    {
        public EnemyAttackState(EnemyAIController enemyAIController)
            : base(enemyAIController)
        {
        }

        public override void EnterState(EnemyAIController enemy)
        {
            enemy.EnterAttackState();
        }

        public override void UpdateState(EnemyAIController enemy)
        {
            enemy.UpdateAttackState();
        }

        public override void ExitState(EnemyAIController enemy)
        {
            enemy.ExitAttackState();
        }
    }
}
