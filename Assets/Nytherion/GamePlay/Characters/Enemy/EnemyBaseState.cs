namespace Nytherion.GamePlay.Characters.Enemy
{
    public abstract class EnemyBaseState
    {
        protected EnemyAIController _enemyAIController;

        protected EnemyBaseState(EnemyAIController enemyAIController)
        {
            _enemyAIController = enemyAIController;
        }

        public abstract void EnterState(EnemyAIController enemy);
        public abstract void UpdateState(EnemyAIController enemy);
        public abstract void ExitState(EnemyAIController enemy);
    }
}
