using UnityEngine;
using Nytherion.Core.Managers;
using Nytherion.Data.ScriptableObjects.Weapons;

namespace Nytherion.GamePlay.Combat.Weapons
{
    public class MeteorWeapon : WeaponBase
    {
        [Header("Meteor Settings")]
        [Tooltip("하늘에서 떨어지는 높이")]
        public float dropHeight = 10f;

        public override void Attack(Vector2 direction, Vector3 targetPosition = default)
        {
            if (!CanAttack() || weaponData == null || weaponData.projectilePrefab == null) return;

            lastAttackTime = Time.time;
            PlayFireAnimation();

            // 마우스 클릭 지점(targetPosition) 위쪽에서 생성
            Vector3 spawnPos = targetPosition + (Vector3.up * dropHeight);

            // 하이브리드 방식: WeaponData에 등록된 프리팹을 사용하여 스폰
            GameObject meteor = ObjectPoolManager.Instance.SpawnFromPool(weaponData.projectilePrefab, spawnPos, Quaternion.identity);

            if (meteor == null) return;

            // 메테오 전용 로직: 낙하 지점 설정
            if (meteor.TryGetComponent<MeteorProj>(out var proj))
            {
                proj.Initialize(targetPosition);
            }

            // 데미지 주입
            if (meteor.TryGetComponent<CollisionObject>(out var col))
            {
                col.damage = weaponData.damage * EffectiveDamageMultiplier;
            }
        }

        public override void AttackEnd()
        {
        }
    }
}
