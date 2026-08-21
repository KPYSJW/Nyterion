using UnityEngine;
namespace Nytherion.GamePlay.Combat.Weapons
{
    /// <summary>
    /// 하이브리드 무기 시스템을 위한 범용 차징 무기 클래스
    /// WeaponData의 maxChargeTime을 사용하며, 차징 완료 시 투사체를 발사
    /// </summary>
    public class GenericChargeableWeapon : ChargeableRangedWeapon
    {
        public override void Initialize(Nytherion.Data.ScriptableObjects.Weapons.WeaponData data)
        {
            base.Initialize(data);
            if (data != null)
            {
                this.maxChargeTime = data.maxChargeTime;
            }
        }

        protected override void FireChargedAttack(Vector2 direction, float chargePercent)
        {
            this.damageMultiplier = IsChargingEnabled() ? Mathf.Lerp(0.5f, 1.0f, chargePercent) : 1.0f;
            
            FireProjectiles(direction, 1);
        }

        protected override void OnCharging(float chargePercent)
        {
        }
    }
}
