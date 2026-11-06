namespace Nytherion.GamePlay.Characters.Enemy
{
    /// <summary>Implemented by pattern components assigned to the witch's combat behavior.</summary>
    public interface IBossWitchPattern
    {
        BossWitchPatternType PatternType { get; }
        bool CanStart(EnemyAIController enemy);
        void Begin(EnemyAIController enemy);
        // Return true only after the pattern, including its recovery, has finished.
        bool UpdatePattern(EnemyAIController enemy);
        void End(EnemyAIController enemy, bool completed);
        void ResetForReuse();
    }
}
