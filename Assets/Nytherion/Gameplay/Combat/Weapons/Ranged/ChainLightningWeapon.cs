using UnityEngine;
using Nytherion.GamePlay.Combat.Weapons;

namespace Nytherion.GamePlay.Combat
{
    /// <summary>
    /// 단발성으로 체인 라이트닝 투사체를 발사하는 차징 무기.
    /// 차징 시간에 비례하여 데미지 배율이 증가합니다.
    /// </summary>
    public class ChainLightningWeapon : ChargeableRangedWeapon
    {
        protected override void FireChargedAttack(Vector2 direction, float chargePercent)
        {
            // 차징 정도에 따른 데미지 증폭 적용: 차징 안하면 1.0배(100%), 풀 차징 시 2.0배(200%)
            this.damageMultiplier = IsChargingEnabled() ? Mathf.Lerp(1.0f, 2.0f, chargePercent) : 1.0f;
            
            // 단순 단발 투사체 발사
            FireProjectiles(direction, 1);
        }
    }
}
