
using UnityEngine;

namespace Nytherion.GamePlay.Combat
{
    public interface IAttackBehavior
    {
        bool TryAttack(Transform target);
        
        bool IsInAttackRange(Transform target);
        
        float AttackCoolDown { get; }
    }
}
