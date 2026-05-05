using UnityEngine;
using Nytherion.Core.Enums;
using Nytherion.Core.Data;
using Nytherion.Core.Managers;
using System;

namespace Nytherion.Gameplay.Engravings.Modules
{
    /// <summary>
    /// 잡은 몬스터 수(킬 카운트)에 비례해서 특정 스탯(예: 생명력 흡수)이 증가하는 성장형 각인.
    /// 최대 증가치가 정해져 있으며, 각인 레벨이 오를수록 최대 한도(Cap)가 늘어납니다.
    /// </summary>
    [Serializable]
    public class KillCountGrowthStatEngravingEffect : EngravingEffectBase
    {
        [Tooltip("증가시킬 대상 스탯 (예: Lifesteal)")]
        public StatType targetStat = StatType.Lifesteal;

        [Tooltip("몬스터 1마리를 잡을 때마다 증가하는 수치 (예: 0.005)")]
        public float valuePerKill = 0.005f;

        [Tooltip("1레벨 기준 스탯 증가 최대치 한도")]
        public float maxBonusBase = 0.1f;

        [Tooltip("레벨이 1 오를 때마다 추가되는 최대치 한도")]
        public float maxBonusPerLevel = 0.05f;

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

            // 몬스터가 죽어서 킬 카운트가 올랐을 때 스탯 재계산을 위해 이벤트 구독
            cachedPlayerManager.OnPlayerStatsChanged -= HandleStatsChanged;
            cachedPlayerManager.OnPlayerStatsChanged += HandleStatsChanged;
            
            Debug.Log($"[KillCountGrowth] 성장형 스탯({targetStat}) 효과 시작. (레벨: {level}, 현재 킬: {cachedPlayerManager.CurrentRunKillCount})");
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
            Debug.Log($"[KillCountGrowth] 성장형 스탯({targetStat}) 효과 해제.");
        }

        private void HandleStatsChanged()
        {
            if (isUpdating) return;
            UpdateStatModifier();
        }

        private void UpdateStatModifier()
        {
            if (cachedPlayerManager == null) return;
            isUpdating = true;

            int killCount = cachedPlayerManager.CurrentRunKillCount;
            
            // 킬 수에 따른 순수 보너스 계산
            float rawBonus = killCount * valuePerKill;

            // 레벨에 따른 최대치(Cap) 스케일링 계산
            float currentMaxBonus = maxBonusBase + (maxBonusPerLevel * Mathf.Max(0, currentLevel - 1));

            // 최대치 한도(캡) 적용
            float finalBonus = Mathf.Min(rawBonus, currentMaxBonus);

            // 기존 모디파이어 제거
            if (currentModifier != null)
            {
                cachedPlayerManager.RemoveTemporaryStatModifier(currentModifier);
            }

            // 새로운 수치로 생성하여 적용
            currentModifier = new StatModifier
            {
                stat = targetStat,
                value = finalBonus,
                valuePerLevel = 0f,
                isPercentage = false
            };

            cachedPlayerManager.AddTemporaryStatModifier(currentModifier);

            isUpdating = false;
        }
    }
}