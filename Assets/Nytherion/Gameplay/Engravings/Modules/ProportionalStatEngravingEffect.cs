using UnityEngine;
using Nytherion.Core.Managers;
using Nytherion.Core.Enums;
using Nytherion.Core.Data;
using System;

namespace Nytherion.Gameplay.Engravings.Modules
{
    /// <summary>
    /// 특정 스탯(예: 이동속도)에 비례하여 다른 스탯(예: 공격력)을 증감시키는 효과
    /// </summary>
    [Serializable]
    public class ProportionalStatEngravingEffect : EngravingEffectBase
    {
        [Tooltip("기준이 될 스탯 (예: MoveSpeed)")]
        public StatType sourceStat = StatType.MoveSpeed;

        [Tooltip("변경할 스탯 (예: MeleeDamage)")]
        public StatType targetStat = StatType.MeleeDamage;

        [Tooltip("기준 스탯의 1당 변경할 스탯의 비율 (예: 이속 1당 공격력 0.5 증가 -> 0.5)")]
        public float ratio = 0.5f;

        [Tooltip("레벨업 시 비율 증가량")]
        public float ratioPerLevel = 0.1f;

        private PlayerManager cachedPlayerManager;
        private int currentLevel;
        private StatModifier currentModifier;
        private bool isUpdating = false;

        public override void ApplyEffect(PlayerManager playerManager, int level)
        {
            if (playerManager == null) return;

            cachedPlayerManager = playerManager;
            currentLevel = level;

            UpdateStatModifier();

            // 스탯이 변할 때마다 비례 수치도 업데이트하기 위해 이벤트 구독
            cachedPlayerManager.OnPlayerStatsChanged -= HandleStatsChanged;
            cachedPlayerManager.OnPlayerStatsChanged += HandleStatsChanged;
        }

        public override void RemoveEffect(PlayerManager playerManager, int level)
        {
            if (cachedPlayerManager != null)
            {
                cachedPlayerManager.OnPlayerStatsChanged -= HandleStatsChanged;

                if (currentModifier != null)
                {
                    cachedPlayerManager.RemoveTemporaryStatModifier(currentModifier);
                    currentModifier = null;
                }
            }
        }

        private void HandleStatsChanged()
        {
            if (isUpdating) return;
            UpdateStatModifier();
        }

        private void UpdateStatModifier()
        {
            if (cachedPlayerManager == null || cachedPlayerManager.currentPlayerData == null) return;

            isUpdating = true; // 무한 루프 방지

            float sourceValue = GetStatValue(cachedPlayerManager.currentPlayerData, sourceStat);
            float finalRatio = ratio + (ratioPerLevel * Mathf.Max(0, currentLevel - 1));
            float targetValueIncrease = sourceValue * finalRatio;

            // 기존 모디파이어 제거
            if (currentModifier != null)
            {
                cachedPlayerManager.RemoveTemporaryStatModifier(currentModifier);
            }

            // 새 모디파이어 생성 및 적용
            currentModifier = new StatModifier
            {
                stat = targetStat,
                value = targetValueIncrease,
                valuePerLevel = 0f,
                isPercentage = false
            };

            cachedPlayerManager.AddTemporaryStatModifier(currentModifier);

            isUpdating = false;
        }

        private float GetStatValue(Nytherion.Data.ScriptableObjects.Player.PlayerData data, StatType stat)
        {
            switch (stat)
            {
                case StatType.MaxHealth: return data.maxHealth;
                case StatType.Defense: return data.defense;
                case StatType.MoveSpeed: return data.moveSpeed;
                case StatType.MeleeDamage: return data.meleeDamage;
                case StatType.RangedDamage: return data.rangedDamage;
                case StatType.MeleeSpeed: return data.meleeSpeed;
                case StatType.RangedSpeed: return data.rangedSpeed;
                case StatType.DashSpeed: return data.dashSpeed;
                case StatType.DashDuration: return data.dashDuration;
                case StatType.DashCooldown: return data.dashCooldown;
                case StatType.ExtraProjectiles: return data.extraProjectiles;
                default: return 0f;
            }
        }
    }
}