using System.Collections.Generic;
using UnityEngine;
using Nytherion.GamePlay.Combat;

namespace Nytherion.GamePlay.Characters.Enemy
{
    public interface IAttackSelector
    {
        IAttackBehavior SelectAttackBehavior(
            Transform self,
            Transform target,
            IReadOnlyList<IAttackBehavior> attackBehaviors);
    }
}
