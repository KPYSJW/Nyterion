using Nytherion.Core.Interfaces;
using UnityEngine;

namespace Nytherion.GamePlay.Characters.Companions
{
    /// <summary>
    /// 부모 소환수의 추적 및 투사체 공격 동작을 사용하는 슬라임 소환수입니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SlimeCompanion : SummonedCompanion
    {
        protected override bool TryAttack(Transform target)
        {
            if (target == null || !target.TryGetComponent(out IDamageable damageable))
            {
                return false;
            }

            attackFreezeTimer = attackFreezeDuration;
            SetMoving(false);
            damageable.TakeDamage(GetProjectileDamage(ownerCombat != null ? ownerCombat.currentWeapon : null));
            TriggerAttackAnimation();
            return true;
        }
    }
}
