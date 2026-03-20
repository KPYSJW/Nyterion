using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Nytherion.GamePlay.Combat;
using Nytherion.GamePlay.Combat.Behaviors;

namespace Nytherion.GamePlay.Characters.Enemy
{
    public class RangedOnlySelector : MonoBehaviour, IAttackSelector
    {
        public IAttackBehavior SelectAttackBehavior(
            Transform self,
            Transform target,
            IReadOnlyList<IAttackBehavior> attackBehaviors)
        {
            if (attackBehaviors == null || attackBehaviors.Count == 0) return null;

            IAttackBehavior ranged = attackBehaviors.OfType<RangedAttackBehavior>().FirstOrDefault();
            return ranged ?? attackBehaviors[0];
        }
    }
}
