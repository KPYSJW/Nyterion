using UnityEngine;
using Nytherion.Core.Managers;
using System.Collections.Generic;
using Nytherion.Data.ScriptableObjects.Weapons;

namespace Nytherion.GamePlay.Combat.Weapons
{
    /// <summary>
    /// 플레이어가 지금까지 획득했던 원거리 무기들의 투사체 중 하나를 무작위로 발사하는 특수 무기
    /// </summary>
    public class RandomArchiveWeapon : RangedWeapon
    {
        private ProgressionManager progressionManager;
        
        [Header("Random Archive Settings")]
        [Tooltip("기록된 투사체가 없을 때 발사할 기본 투사체 프리팹")]
        public GameObject fallbackProjectilePrefab;

        [Header("Test Settings")]
        [Tooltip("이 옵션을 켜면 실제 획득 기록을 무시하고 아래의 테스트 목록에서 무작위로 발사")]
        public bool useTestProjectiles = false;
        
        [Tooltip("테스트용 투사체 프리팹 목록")]
        public List<GameObject> testProjectiles = new List<GameObject>();

        protected override void Awake()
        {
            base.Awake();
            progressionManager = FindObjectOfType<ProgressionManager>();
        }

        public override void Initialize(WeaponData data)
        {
            base.Initialize(data);

            if (data != null && data.projectilePrefab != null)
            {
                fallbackProjectilePrefab = data.projectilePrefab;
            }
        }

        public override void Attack(Vector2 direction, Vector3 targetPosition = default)
        {
            if (!CanAttack() || firePoint == null || weaponData == null) return;

            // 공격 직전에 랜덤 투사체 결정
            SetRandomProjectile();

            // 발사 로직 수행
            FireProjectiles(direction, 1);

            lastAttackTime = Time.time;
        }

        public override void AttackEnd()
        {
        }

        private void SetRandomProjectile()
        {
            // 테스트 모드
            if (useTestProjectiles && testProjectiles != null && testProjectiles.Count > 0)
            {
                int randomIndex = Random.Range(0, testProjectiles.Count);
                weaponData.projectilePrefab = testProjectiles[randomIndex];
                return;
            }

            // ProgressionManager에서 획득 기록 가져오기
            if (progressionManager != null)
            {
                
                List<GameObject> unlockedPrefabs = progressionManager.GetUnlockedProjectilePrefabs();

                if (unlockedPrefabs != null && unlockedPrefabs.Count > 0)
                {
                    int randomIndex = Random.Range(0, unlockedPrefabs.Count);
                    weaponData.projectilePrefab = unlockedPrefabs[randomIndex];
                    return;
                }
            }

            // 기록이 없으면 기본 설정된 프리팹 사용
            weaponData.projectilePrefab = fallbackProjectilePrefab;
        }
    }
}