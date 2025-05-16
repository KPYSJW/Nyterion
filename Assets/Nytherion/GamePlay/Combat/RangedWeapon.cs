using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.U2D;

namespace Nytherion.GamePlay.Combat
{
    /// <summary>
    /// 원거리 무기를 구현한 클래스입니다.
    /// WeaponBase를 상속받아 발사체 기반의 원거리 공격 기능을 제공합니다.
    /// </summary>
    public class RangedWeapon : WeaponBase
    {
        [Header("Ranged Settings")]
        [Tooltip("발사체가 생성될 위치를 지정하는 Transform")]
        public Transform firePoint;
        
        [Tooltip("무기의 시각적 표현을 담당하는 스프라이트 렌더러")]
        public SpriteRenderer sprite;

        // 상수 정의
        private const float DefaultProjectileSpeed = 8f;
        private static readonly Color DefaultColor = new Color(1, 1, 1, 1); // 흰색, 완전 불투명

        /// <summary>
        /// 지정된 방향으로 발사체를 발사합니다.
        /// 발사체는 weaponData에 설정된 프리팹을 사용하여 생성됩니다.
        /// </summary>
        /// <param name="direction">발사 방향 (정규화되지 않은 벡터도 처리 가능)</param>
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
                // 발사체 생성 및 속도 설정
                GameObject projectile = Instantiate(weaponData.projectilePrefab, firePoint.position, Quaternion.identity);
                if (projectile.TryGetComponent<Rigidbody2D>(out var rb))
                {
                    rb.velocity = direction.normalized * DefaultProjectileSpeed;
                }

                // 마지막 공격 시간 갱신
                lastAttackTime = Time.time;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"발사체 생성 중 오류 발생: {ex.Message}");
            }
        }

        /// <summary>
        /// 공격 종료 시 호출되는 메서드입니다.
        /// 원거리 무기의 경우 기본 색상으로 복원하는 간단한 처리를 수행합니다.
        /// </summary>
        public override void AttackEnd()
        {
            if (sprite != null)
            {
                sprite.color = DefaultColor;
            }
        }
    }
}