namespace Nytherion.GamePlay.Characters.Enemy
{
    public sealed class MovementAttackCombatBehavior : IEnemyCombatBehavior
    {
        public bool ShouldEnterAttackState(EnemyAIController enemy)
        {
            return false;
        }

        public void EnterAttackState(EnemyAIController enemy)
        {
        }

        public void UpdateAttackState(EnemyAIController enemy)
        {
            enemy.TransitionToState(enemy.chaseState);
        }

        public void ExitAttackState(EnemyAIController enemy)
        {
        }

        public void ResetForReuse()
        {
        }
    }
}
