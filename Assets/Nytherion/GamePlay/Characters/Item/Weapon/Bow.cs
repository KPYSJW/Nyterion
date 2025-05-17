using Nytherion.GamePlay.Combat;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace Nytherion.GamePlay.Combat.Weapon
{
    public class Bow : RangedWeapon
    {
        public override void Attack(Vector2 direction)
        {
            // 공격 가능 상태 및 필수 컴포넌트 검사
            if (!CanAttack() || weaponData?.projectilePrefab == null || firePoint == null)
            {
                Debug.LogWarning("공격을 수행할 수 없습니다. 필요한 컴포넌트를 확인하세요.");
                return;
            }

            try
            {
                Projectile(direction);
                lastAttackTime = Time.time;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"발사체 생성 중 오류 발생: {ex.Message}");
            }
        }

        public override void AttackEnd()
        {

        }
    }
}

