using System.Collections;
using System.Collections.Generic;
using Nytherion.GamePlay.Combat.Behaviors;
using UnityEngine;

public class EnemyAnimationEventRelay : MonoBehaviour
{
    [SerializeField]MeleeAttackBehavior meleeAttackBehavior;
    [SerializeField]RangedAttackBehavior rangedAttackBehavior;
     public void ActivateCollider()
    {
        if(meleeAttackBehavior!=null)
        meleeAttackBehavior?.ActivateCollider();
    }

    public void DeactivateCollider()
    {
         if(meleeAttackBehavior!=null)
        meleeAttackBehavior.DeactivateCollider();
    }

    public void SpawnProjectileVisual()
    {
         if(rangedAttackBehavior!=null)
        rangedAttackBehavior.SpawnProjectileVisual();
    }
}
