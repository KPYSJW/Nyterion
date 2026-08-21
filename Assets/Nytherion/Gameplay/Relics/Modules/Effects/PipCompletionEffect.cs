using System;
using System.Collections.Generic;
using Nytherion.Core.Data;
using Nytherion.Core.Managers;
using Nytherion.Core.Utils;
using Nytherion.Data.ScriptableObjects.Relics;
using Nytherion.GamePlay.Relics;
using UnityEngine;

namespace Nytherion.Gameplay.Relics.Modules
{
    /// <summary>
    /// 여섯 눈이 모두 보드에 장착됐을 때 각 눈의 기본 능력치 효과를 한 번 더 적용한다.
    /// </summary>
    [Serializable, RelicDisplayName("여섯 눈 완성 효과")]
    public class PipCompletionEffect : RelicEffectBase
    {
        private static readonly string[] PipRelicIds =
        {
            "First Pip",
            "Second Pip",
            "Third Pip",
            "Fourth Pip",
            "Fifth Pip",
            "Six Pip"
        };

        [NonSerialized] private readonly List<StatModifier> appliedModifiers = new List<StatModifier>();

        public override void ApplyEffect(PlayerManager playerManager, int level)
        {
            if (playerManager == null) return;

            RelicManager relicManager = UnityEngine.Object.FindObjectOfType<RelicManager>();
            if (relicManager == null) return;

            appliedModifiers.Clear();

            foreach (string relicId in PipRelicIds)
            {
                RelicBlock block = FindPlacedBlock(relicManager, relicId);
                if (block == null || block.SourceData == null) continue;

                StatRelicEffect baseEffect = FindBaseStatEffect(block.SourceData);
                if (baseEffect == null || baseEffect.statModifiers == null) continue;

                foreach (StatModifier modifier in baseEffect.statModifiers)
                {
                    float scaledValue = modifier.value + (modifier.valuePerLevel * Mathf.Max(0, block.Level - 1));
                    StatModifier completionModifier = new StatModifier
                    {
                        stat = modifier.stat,
                        value = scaledValue,
                        valuePerLevel = 0f,
                        isPercentage = modifier.isPercentage
                    };

                    appliedModifiers.Add(completionModifier);
                    playerManager.AddTemporaryStatModifier(completionModifier);
                }
            }
        }

        public override void RemoveEffect(PlayerManager playerManager, int level)
        {
            if (playerManager == null) return;

            foreach (StatModifier modifier in appliedModifiers)
            {
                playerManager.RemoveTemporaryStatModifier(modifier);
            }

            appliedModifiers.Clear();
        }

        private static RelicBlock FindPlacedBlock(RelicManager relicManager, string relicId)
        {
            foreach (var pair in relicManager.GetPlacedBlocks())
            {
                RelicBlock block = relicManager.GetBlockAt(pair.Value.y, pair.Value.x);
                if (block != null && block.RelicId == relicId)
                {
                    return block;
                }
            }

            return null;
        }

        private static StatRelicEffect FindBaseStatEffect(RelicData relicData)
        {
            foreach (RelicEffectModule module in relicData.effectModules)
            {
                if (module.condition != null && !(module.condition is AlwaysTrueCondition)) continue;

                foreach (RelicEffectBase effect in module.effects)
                {
                    if (effect is StatRelicEffect statEffect)
                    {
                        return statEffect;
                    }
                }
            }

            return null;
        }
    }
}
