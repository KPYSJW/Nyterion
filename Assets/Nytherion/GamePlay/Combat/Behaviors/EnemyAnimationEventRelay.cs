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
    [SerializeField] private BagBarrageCombatBehavior bagBarrageCombatBehavior;
     public void ActivateCollider()
    {
        // 개구리는 FrogLand에서 이동 점프와 공격 점프를 구분한 뒤
        // 공격 점프일 때만 착지 공격 콜라이더를 활성화한다.
        if (frogJumpMovement != null)
            return;

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

    public void FireBagBarrage()
    {
        bagBarrageCombatBehavior?.FireBarrage();
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
