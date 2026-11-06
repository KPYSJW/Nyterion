using System;

namespace Nytherion.GamePlay.Characters.Enemy
{
    public sealed class BossWitchAttackTiming
    {
        public float NextAttackAt { get; private set; }
        public float NextPatternAt { get; private set; }

        public bool IsAttackReady(float now) => now >= NextAttackAt;
        public bool IsPatternReady(float now) => now >= NextPatternAt;

        public void Reset(float now, float attackInterval, float patternCooldown)
        {
            NextAttackAt = now + Math.Max(0.05f, attackInterval);
            NextPatternAt = now + Math.Max(0f, patternCooldown);
        }

        public void CompleteAttack(float now, float attackInterval, float patternCooldown, bool usedPattern)
        {
            NextAttackAt = now + Math.Max(0.05f, attackInterval);
            // Basic attacks must not postpone an already pending boss pattern.
            if (usedPattern) NextPatternAt = now + Math.Max(0f, patternCooldown);
        }
    }
}
