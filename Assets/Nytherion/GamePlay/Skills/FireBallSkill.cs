using Nytherion.Core.Managers;
using UnityEngine;
using VContainer;

namespace Nytherion.GamePlay.Skills
{
    public class FireBallSkill : SkillBase
    {
        [SerializeField] private float speed = 20f;
        [SerializeField] private string poolTag = "FireBall";

        protected override void Activate()
        {
            if (skillData != null)
            {
                Vector3 spawnPosition = firePoint != null ? firePoint.position : caster.position;

                GameObject fireballInstance = ObjectPoolManager.Instance.SpawnFromPool(poolTag, spawnPosition, caster.rotation);

                if (fireballInstance != null && fireballInstance.TryGetComponent(out FireballProjectile projectile))
                {
                    fireballInstance.SetActive(true);
                    projectile.Initialize(skillData.damage, speed, skillData.range, poolTag);
                }
                else
                {
                    Debug.LogError($"[FireBallSkill] 풀에서 오브젝트를 가져오지 못했거나 FireballProjectile이 없습니다!");
                }
            }
        }
    }
}