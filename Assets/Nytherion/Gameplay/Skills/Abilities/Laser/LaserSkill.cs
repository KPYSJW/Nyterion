using Nytherion.Core.Managers;
using Nytherion.Data.ScriptableObjects.Skill;
using UnityEngine;

namespace Nytherion.GamePlay.Skills
{
    public class LaserSkill : SkillBase
    {
        [SerializeField] private string poolTag = "Laser";

        protected override void Activate()
        {
            if (skillData is LaserSkillData laserData)
            {
                Vector3 spawnPosition = firePoint != null ? firePoint.position : caster.position;

                GameObject laserInstance = ObjectPoolManager.Instance.SpawnFromPool(poolTag, spawnPosition, Quaternion.identity);

                if (laserInstance != null && laserInstance.TryGetComponent(out LaserBeam laserEffect))
                {
                    laserInstance.SetActive(true);
                    
                    laserEffect.Initialize(caster, firePoint, laserData.damage, laserData.fireDuration, laserData.tickRate, poolTag);
                }
                else
                {
                    Debug.LogError($"[LaserSkill] 풀에서 오브젝트를 가져오지 못했거나 LaserBeam가 없습니다!");
                }
            }
            else
            {
                Debug.LogError("[LaserSkill] skillData가 LaserSkillData 타입이 아닙니다!");
            }
        }
    }
}