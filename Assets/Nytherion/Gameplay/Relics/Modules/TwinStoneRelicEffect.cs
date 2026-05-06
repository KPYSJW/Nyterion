using UnityEngine;
using Nytherion.Core.Managers;
using System;
using Nytherion.GamePlay.Combat;

namespace Nytherion.Gameplay.Relics.Modules
{
    /// <summary>
    /// 플레이어가 원거리 공격(투사체 발사)을 할 때, 
    /// 현재 투사체 수만큼 데미지가 감소된 추가 '트윈스톤' 투사체를 발사하는 특수 효과
    /// </summary>
    [Serializable]
    public class TwinStoneRelicEffect : RelicEffectBase
    {
        [Tooltip("1레벨 기준 추가 투사체의 데미지 배율 (예: 0.5 = 원래 데미지의 50%)")]
        public float damageRatio = 0.5f;
        [Tooltip("레벨이 1 오를 때마다 추가되는 데미지 배율 (예: 0.1)")]
        public float damageRatioPerLevel = 0.1f;
        
        [Tooltip("클론 투사체가 퍼질 기본 각도")]
        public float spreadAngle = 20f;

        private EventManager cachedEventManager;
        private PlayerManager cachedPlayerManager;
        private int currentLevel = 1;

        public override void ApplyEffect(PlayerManager playerManager, int level)
        {
            if (playerManager == null) return;
            cachedPlayerManager = playerManager;
            currentLevel = level;
            
            cachedEventManager = GameObject.FindObjectOfType<EventManager>();
            if (cachedEventManager != null)
            {
                // 기존에 등록된 콜백이 있다면 제거하여 중복 등록 방지
                cachedEventManager.OnPlayerRangedAttack -= HandleRangedAttack;
                cachedEventManager.OnPlayerRangedAttack += HandleRangedAttack;
                Debug.Log($"[TwinStoneRelicEffect] 트윈스톤 효과 적용 (레벨: {currentLevel})");
            }
        }

        public override void RemoveEffect(PlayerManager playerManager, int level)
        {
            if (cachedEventManager != null)
            {
                cachedEventManager.OnPlayerRangedAttack -= HandleRangedAttack;
                Debug.Log("[TwinStoneRelicEffect] 트윈스톤 효과 해제");
            }
        }

        private void HandleRangedAttack(Vector2 direction, int projectileCount, float baseDamage, Transform firePoint, string poolTag)
        {
            if (firePoint == null || string.IsNullOrEmpty(poolTag)) return;

            // 레벨에 따른 스케일링 계산
            float finalDamageRatio = damageRatio + (damageRatioPerLevel * Mathf.Max(0, currentLevel - 1));

            float baseAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            
            // 원본 투사체와 약간 다르게 발사되도록 무작위성 추가
            float startAngle = baseAngle - (spreadAngle / 2f);
            float angleStep = projectileCount > 1 ? spreadAngle / (projectileCount - 1) : 0f;

            for (int i = 0; i < projectileCount; i++)
            {
                float currentAngle = startAngle + (angleStep * i) + UnityEngine.Random.Range(-10f, 10f); 
                Vector2 spreadDirection = new Vector2(Mathf.Cos(currentAngle * Mathf.Deg2Rad), Mathf.Sin(currentAngle * Mathf.Deg2Rad));
                
                Vector3 spawnPos = new Vector3(firePoint.position.x, firePoint.position.y, 0f);
                GameObject cloneProj = ObjectPoolManager.Instance.SpawnFromPool(poolTag, spawnPos, Quaternion.identity);
                
                if (cloneProj.TryGetComponent<Rigidbody2D>(out var rb))
                {
                    rb.velocity = spreadDirection.normalized * 8f; // DefaultProjectileSpeed 8f 기준
                    cloneProj.transform.rotation = Quaternion.AngleAxis(currentAngle, Vector3.forward);
                }
                
                if (cloneProj.TryGetComponent<CollisionObject>(out var collisionObj))
                {
                    collisionObj.damage = baseDamage * finalDamageRatio;
                }
                
                // 시각적으로 트윈스톤임을 알 수 있게 투명도(Alpha)를 50%
                if (cloneProj.TryGetComponent<SpriteRenderer>(out var projSprite))
                {
                    Color color = projSprite.color;
                    color.a = 0.5f; 
                    projSprite.color = color;
                }
            }
        }
    }
}