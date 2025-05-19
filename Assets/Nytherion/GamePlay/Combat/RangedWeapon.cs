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
    public class RangedWeapon : WeaponBase
    {
        [Header("Ranged Settings")]
        [Tooltip("발사체가 생성될 위치를 지정하는 Transform입니다." +
                 "캐릭터의 손이나 무기 끝부분에 위치시켜야 합니다.")]
        public Transform firePoint;
        
        [Tooltip("무기의 시각적 표현을 담당하는 스프라이트 렌더러입니다." +
                "공격 시 색상 변경 등에 사용됩니다.")]
        public SpriteRenderer sprite;

        /// <summary>발사체의 기본 속도</summary>
        private const float DefaultProjectileSpeed = 8f;
        
        /// <summary>무기의 기본 색상 (흰색, 완전 불투명)</summary>
        private static readonly Color DefaultColor = new Color(1, 1, 1, 1);

        /// <summary>
        /// 지정된 방향으로 발사체를 발사합니다.
        /// 발사체는 weaponData에 설정된 프리팹을 사용하여 생성됩니다.
        /// </summary>
        /// <param name="direction">발사 방향 (정규화되지 않은 벡터도 처리 가능)</param>
        public override void Attack(Vector2 direction)
        {
            // 1. 공격 가능 상태 및 필수 컴포넌트 검사
            if (!CanAttack() || weaponData?.projectilePrefab == null || firePoint == null)
            {
                Debug.LogWarning("공격을 수행할 수 없습니다. 필요한 컴포넌트를 확인하세요.");
                return;
            }

            try
            {
                // 2. 발사체 생성 (발사 지점에 인스턴스화)
                //    발사체는 무기 데이터에 지정된 프리팹을 사용합니다.
                GameObject projectile = Instantiate(
                    weaponData.projectilePrefab,  // 발사체 프리팹
                    firePoint.position,            // 발사 위치
                    Quaternion.identity);          // 기본 회전값
                
                // 3. 발사체에 물리 속성 적용 (Rigidbody2D가 있는 경우)
                if (projectile.TryGetComponent<Rigidbody2D>(out Rigidbody2D rb))
                {
                    // 입력 방향으로 발사체 속도 설정 (정규화하여 일정한 속도 유지)
                    rb.velocity = direction.normalized * DefaultProjectileSpeed;
                    
                    // 발사체 방향 설정 (시각적 일관성을 위해)
                    if (direction != Vector2.zero)
                    {
                        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                        projectile.transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);
                    }
                }

                // 4. 마지막 공격 시간을 현재 시간으로 갱신 (쿨다운 시작)
                lastAttackTime = Time.time;
                
                // 5. 발사 효과 재생 (사운드, 이펙트 등)
                // 예: PlayShootSound();
                //     ShowMuzzleFlash();
            }
            catch (System.Exception ex)
            {
                // 6. 오류 처리 (필요한 경우)
                Debug.LogError($"발사체 생성 중 오류 발생: {ex.Message}");
                Debug.LogException(ex);
            }
        }

        /// <summary>
        /// 공격 종료 시 호출되는 메서드입니다.
        /// 원거리 무기의 경우 기본 색상으로 복원하는 간단한 처리를 수행합니다.
        /// </summary>
        public override void AttackEnd()
        {
            // 1. 스프라이트가 할당되어 있는지 확인
            if (sprite != null)
            {
                // 2. 무기 색상을 기본 색상으로 복원
                sprite.color = DefaultColor;
            }
            
            // 3. 추가적인 공격 종료 처리 (필요한 경우)
            // 예: 발사 애니메이션 중지, 사운드 정지 등
        }
    }
}