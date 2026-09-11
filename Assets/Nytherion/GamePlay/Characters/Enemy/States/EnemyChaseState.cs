namespace Nytherion.GamePlay.Characters.Enemy.States
{
    public class EnemyChaseState : EnemyBaseState
    {
        public EnemyChaseState(EnemyAIController enemyAIController) : base(enemyAIController) { }

        public override void EnterState(EnemyAIController enemy)
        {
            
            enemy.PlayAnimation("Run");
        }

        public override void UpdateState(EnemyAIController enemy)
        {
            if (enemy.ShouldEnterAttackState())
            {
                enemy.TransitionToState(enemy.attackState);
                return;
            }

            enemy.MoveTowardsPlayer();
        }

        public override void ExitState(EnemyAIController enemy)
        {
            enemy.StopMovement();
        }

    }
}
