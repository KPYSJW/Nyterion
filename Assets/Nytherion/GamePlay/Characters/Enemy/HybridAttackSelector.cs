using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Nytherion.GamePlay.Combat;
using Nytherion.GamePlay.Combat.Behaviors;
using UnityEngine;

namespace Nytherion.GamePlay.Characters.Enemy
{
    public class HybridAttackSelector : MonoBehaviour, IAttackSelector
    {
        [SerializeField] private float meleePriorityDistance=2.5f;

        public void SetSwitchDistance(float distance)
        {
            meleePriorityDistance=Mathf.Max(0.1f,distance);
        }

        public IAttackBehavior SelectAttackBehavior(
            Transform self,
            Transform target,
            IReadOnlyList<IAttackBehavior> attackBehaviors)
        {
            if(attackBehaviors==null || attackBehaviors.Count==0) return null;

            IAttackBehavior melee=attackBehaviors.OfType<MeleeAttackBehavior>().FirstOrDefault();
            IAttackBehavior ranged=attackBehaviors.OfType<RangedAttackBehavior>().FirstOrDefault();

            if(target==null)
            {
                return melee ?? ranged ?? attackBehaviors[0];
            }

            float distance=Vector2.Distance(self.position, target.position);

             if (distance <= meleePriorityDistance)
            {
                if (melee != null && melee.IsInAttackRange(target)) return melee;
                if (ranged != null && ranged.IsInAttackRange(target)) return ranged;
            }
            else
            {
                if (ranged != null && ranged.IsInAttackRange(target)) return ranged;
                if (melee != null && melee.IsInAttackRange(target)) return melee;
            }

            if (melee != null || ranged != null)
            {
                return new[] { melee, ranged }
                    .Where(x => x != null)
                    .OrderByDescending(x => x.AttackCoolDown)
                    .FirstOrDefault();
            }

            return attackBehaviors[0];
        }
    }
}