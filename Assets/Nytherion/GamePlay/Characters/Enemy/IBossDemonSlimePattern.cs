namespace Nytherion.GamePlay.Characters.Enemy
{
    public interface IBossDemonSlimePattern
    {
        BossDemonSlimePatternType PatternType { get; }
        bool CanStart(EnemyAIController enemy);
        void Begin(EnemyAIController enemy, BossDemonSlimeCombatBehavior combat);
        bool UpdatePattern(EnemyAIController enemy);
        void OnAnimationHit(EnemyAIController enemy);
        void End(EnemyAIController enemy, bool completed);
        void ResetForReuse();
    }
}
