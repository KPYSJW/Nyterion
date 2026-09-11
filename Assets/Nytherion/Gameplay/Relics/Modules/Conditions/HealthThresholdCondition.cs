using UnityEngine;
using Nytherion.Core.Managers;
using Nytherion.Core.Utils;
using System;

namespace Nytherion.Gameplay.Relics.Modules
{
    public enum ThresholdType { Below, Above }

    /// <summary>
    /// 플레이어의 체력 퍼센트에 따라 발동 여부를 결정
    /// </summary>
    [Serializable, RelicDisplayName("체력 수치 조건")]
    public class HealthThresholdCondition : RelicConditionBase
    {
        [Tooltip("기준 체력 퍼센트 (예: 50)")]
        [Range(0f, 100f)]
        public float thresholdPercent = 50f;

        [Tooltip("체력이 기준치보다 아래일 때 발동할지, 위일 때 발동할지 결정")]
        public ThresholdType thresholdType = ThresholdType.Below;

        public override bool IsConditionMet(PlayerManager playerManager, int level)
        {
            if (playerManager == null || playerManager.playerHealth == null) return false;

            float currentHealth = playerManager.playerHealth.CurrentHealth;
            float maxHealth = playerManager.playerHealth.MaxHealth;

            if (maxHealth <= 0) return false;

            float currentPercent = (currentHealth / maxHealth) * 100f;

            if (thresholdType == ThresholdType.Below)
            {
                return currentPercent <= thresholdPercent;
            }
            else
            {
                return currentPercent >= thresholdPercent;
            }
        }
    }
}