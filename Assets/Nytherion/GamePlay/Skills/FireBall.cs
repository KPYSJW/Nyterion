using Nytherion.Core.Managers;
using Nytherion.GamePlay.Characters.Skill;
using UnityEngine;


public class FireBall : SkillBase
{
    PlayerAction playerActions;
    [SerializeField] private float damage;
    [SerializeField] private float range;
    [SerializeField] private float speed;
    [SerializeField] private string poolTag = "FireBall";
    public Transform firePoint;

    protected override void Activate()
    {
        if (skillData != null && skillData.skillPrefab != null)
        {
            Vector3 spawnPosition = firePoint != null ? firePoint.position : transform.position;

            GameObject fireballInstance = ObjectPoolManager.Instance.SpawnFromPool(poolTag, spawnPosition, transform.rotation);

            FireballProjectile projectile = fireballInstance.GetComponent<FireballProjectile>();
            if (projectile != null)
            {
                projectile.Initialize(damage, speed, range, poolTag);
            }
        }
        else
        {
            Debug.LogWarning("FireBall 스킬 데이터나 프리팹이 할당되지 않았습니다.");
        }
    }
}
