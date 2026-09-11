using System.Collections.Generic;
using Nytherion.Data.ScriptableObjects.Enemy;
using Nytherion.GamePlay.Combat;

namespace Nytherion.GamePlay.Characters.Enemy
{
    public static class EnemyCombatBehaviorFactory
    {
        public static IEnemyCombatBehavior Create(
            EnemyCombatType combatType,
            IReadOnlyList<IAttackBehavior> attackBehaviors,
            IAttackSelector attackSelector,
            float hybridSwitchDistance)
        {
            switch (combatType)
            {
                case EnemyCombatType.Hybrid:
                    return new HybridCombatBehavior(
                        attackBehaviors,
                        attackSelector,
                        hybridSwitchDistance);

                case EnemyCombatType.MovementAttack:
                    return new MovementAttackCombatBehavior();

                case EnemyCombatType.Melee:
                case EnemyCombatType.Ranged:
                default:
                    return new SingleAttackCombatBehavior(
                        attackBehaviors,
                        attackSelector);
            }
        }
    }
}
