using System.Collections;
using System.Collections.Generic;
using Nytherion.GamePlay.Combat.Behaviors;
using Nytherion.GamePlay.Characters.Enemy;
using UnityEngine;

public class EnemyAnimationEventRelay : MonoBehaviour
{
    [SerializeField]MeleeAttackBehavior meleeAttackBehavior;
    [SerializeField]RangedAttackBehavior rangedAttackBehavior;
    [SerializeField] private FrogJumpMovement frogJumpMovement;
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

    public void FrogJumpStart()
    {
        frogJumpMovement?.FrogJumpStart();
    }

    public void FrogLand()
    {
        frogJumpMovement?.FrogLand();
    }
    public void FrogStartIdle()
    {
        frogJumpMovement?.FrogStartIdle();
    }
}
