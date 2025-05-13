using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace Game.Combat
{
    public interface IAttackBehavior
    {
        void TryAttack(Transform target);
        bool IsInAttackRange(Transform target);
        float AttackCoolDown { get; }
    }
}
