using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.U2D;

namespace Nytherion.GamePlay.Combat
{
    /// <summary>
    /// 원거리 무기를 구현한 클래스입니다.
    /// WeaponBase를 상속받아 발사체 기반의 원거리 공격 기능을 제공합니다.
    /// 발사체를 생성하여 지정된 방향으로 발사하는 기능을 제공합니다.
    /// </summary>
    public abstract class RangedWeapon : WeaponBase
    {
        [Header("Ranged Settings")]
        [Tooltip("발사체가 생성될 위치를 지정하는 Transform입니다." +
                 "캐릭터의 손이나 무기 끝부분에 위치시켜야 합니다.")]
        public Transform firePoint;
        

        /// <summary>발사체의 기본 속도</summary>
        private const float DefaultProjectileSpeed = 8f;

        /// <summary>
        /// 지정된 방향으로 발사체를 발사합니다.
        /// 발사체는 weaponData에 설정된 프리팹을 사용하여 생성됩니다.
        /// </summary>
        /// <param name="direction">발사 방향 (정규화되지 않은 벡터도 처리 가능)</param>
        /// <summary>
        /// 공격 종료 시 호출되는 메서드입니다.
        /// 원거리 무기의 경우 기본 색상으로 복원하는 간단한 처리를 수행합니다.
        /// </summary>
      
       public void Projectile(Vector2 direction)
        {
            GameObject projectile = Instantiate(weaponData.projectilePrefab, firePoint.position, Quaternion.identity);
            if (projectile.TryGetComponent<Rigidbody2D>(out var rb))
            {
                rb.velocity = direction.normalized * DefaultProjectileSpeed;

            }
            
            // 3. 추가적인 공격 종료 처리 (필요한 경우)
            // 예: 발사 애니메이션 중지, 사운드 정지 등
        }

    }
}