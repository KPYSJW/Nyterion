using UnityEngine;

namespace Nytherion.GamePlay.Combat
{
    /// <summary>
    /// 하이브리드 무기 시스템을 위한 범용 원거리 무기 클래스
    /// 특정 무기 로직 없이 WeaponData에 정의된 설정을 바탕으로 동작
    /// </summary>
    public class GenericRangedWeapon : RangedWeapon
    {
        public override void Attack(Vector2 direction, Vector3 targetPosition = default)
        {
            if (!CanAttack()) return;

            // RangedWeapon의 FireProjectiles를 호출하여 투사체를 발사
            // 기본 발사 개수는 1개이며, 추가 투사체 각인 등이 있다면 내부 로직에 의해 자동으로 증가
            FireProjectiles(direction, 1);
            
            lastAttackTime = Time.time;
            
        }

        public override void AttackEnd()
        {
        }
    }
}
