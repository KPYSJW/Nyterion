using System;
using System.Collections.Generic;
using Nytherion.Core.Managers;
using Nytherion.Core.Utils;
using UnityEngine;

namespace Nytherion.Gameplay.Relics.Modules
{
    /// <summary>
    /// 활성화된 소환물 유물 투사체의 피해와 추가 발사 수를 공통으로 보정한다.
    /// </summary>
    [Serializable, RelicDisplayName("소환물 유물 세트 강화")]
    public class FollowerAttackSetBonusEffect : RelicEffectBase
    {
        [Min(0f)]
        [Tooltip("소환물 유물 투사체 피해 배율 (1.15 = 15% 증가)")]
        public float damageMultiplier = 1f;

        [Min(0)]
        [Tooltip("소환물 유물이 한 번 공격할 때 추가로 발사할 투사체 수")]
        public int additionalProjectileCount;

        [Min(0f)]
        [Tooltip("추가 투사체를 발사할 때 적용할 탄퍼짐 간 각도")]
        public float additionalProjectileSpreadAngle = 12f;

        [NonSerialized] private FollowerAttackSetBonusRuntime appliedRuntime;

        public override void ApplyEffect(PlayerManager playerManager, int level)
        {
            RemoveEffect(playerManager, level);
            if (playerManager == null) return;

            appliedRuntime = playerManager.GetComponent<FollowerAttackSetBonusRuntime>();
            if (appliedRuntime == null)
            {
                appliedRuntime = playerManager.gameObject.AddComponent<FollowerAttackSetBonusRuntime>();
            }

            appliedRuntime.SetBonus(this, damageMultiplier, additionalProjectileCount, additionalProjectileSpreadAngle);
        }

        public override void RemoveEffect(PlayerManager playerManager, int level)
        {
            if (appliedRuntime == null && playerManager != null)
            {
                appliedRuntime = playerManager.GetComponent<FollowerAttackSetBonusRuntime>();
            }

            if (appliedRuntime != null)
            {
                appliedRuntime.RemoveBonus(this);
            }

            appliedRuntime = null;
        }
    }

    /// <summary>
    /// 동시에 활성화될 수 있는 여러 소환물 유물 세트 보정을 합산한다.
    /// </summary>
    public sealed class FollowerAttackSetBonusRuntime : MonoBehaviour
    {
        private readonly Dictionary<FollowerAttackSetBonusEffect, BonusValues> bonuses =
            new Dictionary<FollowerAttackSetBonusEffect, BonusValues>();

        public float DamageMultiplier { get; private set; } = 1f;
        public int AdditionalProjectileCount { get; private set; }
        public float AdditionalProjectileSpreadAngle { get; private set; }

        private struct BonusValues
        {
            public float DamageMultiplier;
            public int AdditionalProjectileCount;
            public float AdditionalProjectileSpreadAngle;
        }

        public void SetBonus(
            FollowerAttackSetBonusEffect source,
            float damageMultiplier,
            int additionalProjectileCount,
            float additionalProjectileSpreadAngle)
        {
            if (source == null) return;

            bonuses[source] = new BonusValues
            {
                DamageMultiplier = Mathf.Max(0f, damageMultiplier),
                AdditionalProjectileCount = Mathf.Max(0, additionalProjectileCount),
                AdditionalProjectileSpreadAngle = Mathf.Max(0f, additionalProjectileSpreadAngle)
            };
            Recalculate();
        }

        public void RemoveBonus(FollowerAttackSetBonusEffect source)
        {
            if (source == null || !bonuses.Remove(source)) return;
            Recalculate();
        }

        private void Recalculate()
        {
            float damage = 1f;
            int additionalProjectiles = 0;
            float spreadAngle = 0f;
            foreach (BonusValues bonus in bonuses.Values)
            {
                damage *= bonus.DamageMultiplier;
                additionalProjectiles += bonus.AdditionalProjectileCount;
                spreadAngle = Mathf.Max(spreadAngle, bonus.AdditionalProjectileSpreadAngle);
            }

            DamageMultiplier = damage;
            AdditionalProjectileCount = additionalProjectiles;
            AdditionalProjectileSpreadAngle = spreadAngle;
        }
    }
}
