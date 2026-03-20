using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Nytherion.GamePlay.Combat;
using Nytherion.GamePlay.Combat.Behaviors;

namespace Nytherion.GamePlay.Characters.Enemy
{
    public class MeleeOnlySelector : MonoBehaviour, IAttackSelector
    {
        public IAttackBehavior SelectAttackBehavior(
            Transform self,
            Transform target,
            IReadOnlyList<IAttackBehavior> attackBehaviors)
        {
            if (attackBehaviors == null || attackBehaviors.Count == 0) return null;

            IAttackBehavior melee = attackBehaviors.OfType<MeleeAttackBehavior>().FirstOrDefault();
            return melee ?? attackBehaviors[0];
        }
    }
}
