using System;
using System.Collections.Generic;
using Nytherion.Core.Managers;
using Nytherion.Core.Utils;
using UnityEngine;

namespace Nytherion.Gameplay.Relics.Modules
{
    /// <summary>
    /// 도탄 피해와 속도를 강화하고 추가 탄환을 생성한다.
    /// </summary>
    [Serializable, RelicDisplayName("난반사 세트 강화")]
    public sealed class TrickshotSetBonusEffect : RelicEffectBase
    {
        [Range(0f, 1f)] public float firstBounceDamageMultiplier = 0.5f;
        [Range(0f, 1f)] public float laterBounceDamageMultiplier = 0.8f;
        [Min(1f)] public float bounceSpeedMultiplier = 1f;
        [Min(1f)] public float bounceDamageIncreaseMultiplier = 1f;

        [Header("추가 탄환")]
        [Min(0)] public int extraProjectileCount;
        [Min(0f)] public float extraProjectileDamageRatio;

        [NonSerialized] private TrickshotSetBonusRuntime appliedRuntime;

        public override void ApplyEffect(PlayerManager playerManager, int level)
        {
            RemoveEffect(playerManager, level);
            if (playerManager == null) return;

            appliedRuntime = playerManager.GetComponent<TrickshotSetBonusRuntime>();
            if (appliedRuntime == null)
            {
                appliedRuntime = playerManager.gameObject.AddComponent<TrickshotSetBonusRuntime>();
            }

            appliedRuntime.SetBonus(this, new TrickshotSetBonusValues
            {
                FirstBounceDamageMultiplier = firstBounceDamageMultiplier,
                LaterBounceDamageMultiplier = laterBounceDamageMultiplier,
                BounceSpeedMultiplier = bounceSpeedMultiplier,
                BounceDamageIncreaseMultiplier = bounceDamageIncreaseMultiplier,
                ExtraProjectileCount = extraProjectileCount,
                ExtraProjectileDamageRatio = extraProjectileDamageRatio
            });
        }

        public override void RemoveEffect(PlayerManager playerManager, int level)
        {
            if (appliedRuntime == null && playerManager != null)
            {
                appliedRuntime = playerManager.GetComponent<TrickshotSetBonusRuntime>();
            }

            appliedRuntime?.RemoveBonus(this);
            appliedRuntime = null;
        }
    }

    public struct TrickshotSetBonusValues
    {
        public float FirstBounceDamageMultiplier;
        public float LaterBounceDamageMultiplier;
        public float BounceSpeedMultiplier;
        public float BounceDamageIncreaseMultiplier;
        public int ExtraProjectileCount;
        public float ExtraProjectileDamageRatio;
    }

    public sealed class TrickshotSetBonusRuntime : MonoBehaviour
    {
        private readonly Dictionary<TrickshotSetBonusEffect, TrickshotSetBonusValues> bonuses =
            new Dictionary<TrickshotSetBonusEffect, TrickshotSetBonusValues>();

        public float FirstBounceDamageMultiplier { get; private set; } = 0.5f;
        public float LaterBounceDamageMultiplier { get; private set; } = 0.8f;
        public float BounceSpeedMultiplier { get; private set; } = 1f;
        public float BounceDamageIncreaseMultiplier { get; private set; } = 1f;
        public int ExtraProjectileCount { get; private set; }
        public float ExtraProjectileDamageRatio { get; private set; }

        public void SetBonus(TrickshotSetBonusEffect source, TrickshotSetBonusValues values)
        {
            if (source == null) return;
            bonuses[source] = values;
            Recalculate();
        }

        public void RemoveBonus(TrickshotSetBonusEffect source)
        {
            if (source == null || !bonuses.Remove(source)) return;
            Recalculate();
        }

        private void Recalculate()
        {
            FirstBounceDamageMultiplier = 0.5f;
            LaterBounceDamageMultiplier = 0.8f;
            BounceSpeedMultiplier = 1f;
            BounceDamageIncreaseMultiplier = 1f;
            ExtraProjectileCount = 0;
            ExtraProjectileDamageRatio = 0f;

            foreach (TrickshotSetBonusValues bonus in bonuses.Values)
            {
                FirstBounceDamageMultiplier = Mathf.Max(FirstBounceDamageMultiplier, bonus.FirstBounceDamageMultiplier);
                LaterBounceDamageMultiplier = Mathf.Max(LaterBounceDamageMultiplier, bonus.LaterBounceDamageMultiplier);
                BounceSpeedMultiplier = Mathf.Max(BounceSpeedMultiplier, bonus.BounceSpeedMultiplier);
                BounceDamageIncreaseMultiplier = Mathf.Max(BounceDamageIncreaseMultiplier, bonus.BounceDamageIncreaseMultiplier);
                ExtraProjectileCount = Mathf.Max(ExtraProjectileCount, bonus.ExtraProjectileCount);
                ExtraProjectileDamageRatio = Mathf.Max(ExtraProjectileDamageRatio, bonus.ExtraProjectileDamageRatio);
            }
        }
    }
}
