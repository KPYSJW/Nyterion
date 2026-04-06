namespace Nytherion.GamePlay.Characters.Enemy.States
{
    public class EnemyAttackState : EnemyBaseState
    {
        public EnemyAttackState(EnemyAIController enemyAIController) : base(enemyAIController) { }

        public override void EnterState(EnemyAIController enemy)
        {
            enemy.PlayAnimation("Attack");
            enemy.StopMovement();
        }

        public override void UpdateState(EnemyAIController enemy)
        {
            enemy.TryAttackPlayer();

            if (!enemy.CanAttackPlayer())
            {
                enemy.TransitionToState(enemy.chaseState);
            }
        }

        public override void ExitState(EnemyAIController enemy)
        {
        }
    }
}
