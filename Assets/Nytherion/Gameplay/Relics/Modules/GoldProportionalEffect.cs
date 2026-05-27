using UnityEngine;
using Nytherion.Core.Managers;
using Nytherion.Core.Enums;
using Nytherion.Core.Data;
using Nytherion.Core.Interfaces;
using Nytherion.Core.Utils;
using System;
using System.Collections.Generic;

namespace Nytherion.Gameplay.Relics.Modules
{
    /// <summary>
    /// 보유 골드량에 비례하여 근접/원거리 공격력을 강화하는 효과
    /// </summary>
    [Serializable, RelicDisplayName("골드 비례 공격력 강화 효과")]
    public class GoldProportionalEffect : RelicEffectBase
    {
        [Tooltip("비례 기준이 되는 단위 골드량")]
        public int goldUnit = 100;

        [Tooltip("단위 골드당 증가시킬 공격력 수치")]
        public float damagePerUnit = 2f;

        [Tooltip("최대 공격력 증가 수치")]
        public float maxDamageIncrease = 10f;

        private PlayerManager cachedPlayerManager;
        private CurrencyDataManager cachedCurrencyManager;
        private List<StatModifier> appliedModifiers = new List<StatModifier>();
        private bool isUpdating = false;

        public override void ApplyEffect(PlayerManager playerManager, int level)
        {
            if (playerManager == null) return;

            cachedPlayerManager = playerManager;
            cachedCurrencyManager = UnityEngine.Object.FindObjectOfType<CurrencyDataManager>();

            UpdateEffect();

            if (cachedCurrencyManager != null)
            {
                cachedCurrencyManager.OnDataChanged -= HandleCurrencyChanged;
                cachedCurrencyManager.OnDataChanged += HandleCurrencyChanged;
            }
        }

        public override void RemoveEffect(PlayerManager playerManager, int level)
        {
            if (cachedCurrencyManager != null)
            {
                cachedCurrencyManager.OnDataChanged -= HandleCurrencyChanged;
            }

            ClearModifiers();
        }

        private void HandleCurrencyChanged(CurrencyChangeData changeData)
        {
            if (changeData.currencyType == CurrencyType.Gold)
            {
                UpdateEffect();
            }
        }

        private void UpdateEffect()
        {
            if (cachedPlayerManager == null || cachedCurrencyManager == null) return;
            if (isUpdating) return;

            isUpdating = true;

            int currentGold = cachedCurrencyManager.GetGold();
            int units = currentGold / goldUnit;
            float damageIncrease = units * damagePerUnit;

            if (damageIncrease > maxDamageIncrease)
            {
                damageIncrease = maxDamageIncrease;
            }

            ClearModifiers();

            StatModifier meleeMod = new StatModifier
            {
                stat = StatType.MeleeDamage,
                value = damageIncrease,
                valuePerLevel = 0f,
                isPercentage = false
            };

            StatModifier rangedMod = new StatModifier
            {
                stat = StatType.RangedDamage,
                value = damageIncrease,
                valuePerLevel = 0f,
                isPercentage = false
            };

            appliedModifiers.Add(meleeMod);
            appliedModifiers.Add(rangedMod);

            cachedPlayerManager.AddTemporaryStatModifier(meleeMod);
            cachedPlayerManager.AddTemporaryStatModifier(rangedMod);

            isUpdating = false;
        }

        private void ClearModifiers()
        {
            if (cachedPlayerManager == null || appliedModifiers == null) return;

            foreach (StatModifier modifier in appliedModifiers)
            {
                cachedPlayerManager.RemoveTemporaryStatModifier(modifier);
            }
            appliedModifiers.Clear();
        }
    }
}
