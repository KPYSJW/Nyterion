using Nytherion.Core.Managers;
using UnityEngine;

namespace Nytherion.GamePlay.Skills
{
    public class SoulEaterSkill : SkillBase
    {
        [SerializeField] private float speed = 20f;
        [SerializeField] private string poolTag = "SoulEater";
        
        [Header("Growth Settings")]
        [SerializeField] private float damageIncreaseOnKill = 1f;
        private float permanentBonusDamage = 0f;

        protected override void Activate()
        {
            if (skillData != null)
            {
                Vector3 spawnPosition = firePoint != null ? firePoint.position : caster.position;

                Vector3 mouseWorldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
                mouseWorldPos.z = 0f;
                Vector3 direction = (mouseWorldPos - spawnPosition).normalized;
                float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                Quaternion spawnRotation = Quaternion.Euler(0f, 0f, angle);

                GameObject projectileInstance = ObjectPoolManager.Instance.SpawnFromPool(poolTag, spawnPosition, spawnRotation);

                if (projectileInstance != null && projectileInstance.TryGetComponent(out SoulEaterProj projectile))
                {
                    projectileInstance.SetActive(true);
                    
                    float totalDamage = skillData.damage + permanentBonusDamage;
                    
                    projectile.Initialize(totalDamage, speed, skillData.range, poolTag, OnEnemyKilled);
                }
                else
                {
                    Debug.LogError($"[SoulEaterSkill] 풀에서 오브젝트를 가져오지 못했거나 SoulEaterProj이 없습니다!");
                }
            }
        }

        private void OnEnemyKilled()
        {
            permanentBonusDamage += damageIncreaseOnKill;
            Debug.Log($"[SoulEaterSkill] 적 처치! 영구 데미지 증가. 현재 추가 데미지: {permanentBonusDamage}");
        }
    }
}