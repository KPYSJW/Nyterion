namespace Nytherion.GamePlay.Characters.Enemy
{
    public interface IEnemyCombatBehavior
    {
        bool ShouldEnterAttackState(EnemyAIController enemy);
        void EnterAttackState(EnemyAIController enemy);
        void UpdateAttackState(EnemyAIController enemy);
        void ExitAttackState(EnemyAIController enemy);
        void ResetForReuse();
    }
}
