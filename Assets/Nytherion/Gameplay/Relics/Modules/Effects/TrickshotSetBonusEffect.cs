using System;
using System.Collections.Generic;
using Nytherion.Core.Managers;
using Nytherion.Core.Utils;
using UnityEngine;

namespace Nytherion.Gameplay.Relics.Modules
{
    /// <summary>
    /// 관통·도탄·분열 같은 투사체 상호작용을 누적 강화하고 마지막 충돌에 잔향탄을 생성한다.
    /// </summary>
    [Serializable, RelicDisplayName("도탄 기술자 세트 강화")]
    public sealed class TrickshotSetBonusEffect : RelicEffectBase
    {
        [Min(0f)] public float projectileDamageMultiplier = 1f;
        [Min(0f)] public float interactionDamageBonus;
        [Min(0)] public int maxInteractionStacks;

        [Header("잔향탄")]
        [Min(0)] public int echoProjectileCount;
        [Min(0f)] public float echoDamageRatio;
        [Min(0.1f)] public float echoSearchRadius = 6f;

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
                ProjectileDamageMultiplier = projectileDamageMultiplier,
                InteractionDamageBonus = interactionDamageBonus,
                MaxInteractionStacks = maxInteractionStacks,
                EchoProjectileCount = echoProjectileCount,
                EchoDamageRatio = echoDamageRatio,
                EchoSearchRadius = echoSearchRadius
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
        public float ProjectileDamageMultiplier;
        public float InteractionDamageBonus;
        public int MaxInteractionStacks;
        public int EchoProjectileCount;
        public float EchoDamageRatio;
        public float EchoSearchRadius;
    }

    public sealed class TrickshotSetBonusRuntime : MonoBehaviour
    {
        private readonly Dictionary<TrickshotSetBonusEffect, TrickshotSetBonusValues> bonuses =
            new Dictionary<TrickshotSetBonusEffect, TrickshotSetBonusValues>();

        public float ProjectileDamageMultiplier { get; private set; } = 1f;
        public float InteractionDamageBonus { get; private set; }
        public int MaxInteractionStacks { get; private set; }
        public int EchoProjectileCount { get; private set; }
        public float EchoDamageRatio { get; private set; }
        public float EchoSearchRadius { get; private set; } = 6f;

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
            ProjectileDamageMultiplier = 1f;
            InteractionDamageBonus = 0f;
            MaxInteractionStacks = 0;
            EchoProjectileCount = 0;
            EchoDamageRatio = 0f;
            EchoSearchRadius = 6f;

            foreach (TrickshotSetBonusValues bonus in bonuses.Values)
            {
                ProjectileDamageMultiplier = Mathf.Max(
                    ProjectileDamageMultiplier,
                    bonus.ProjectileDamageMultiplier);
                InteractionDamageBonus = Mathf.Max(
                    InteractionDamageBonus,
                    bonus.InteractionDamageBonus);
                MaxInteractionStacks = Mathf.Max(
                    MaxInteractionStacks,
                    bonus.MaxInteractionStacks);
                EchoProjectileCount = Mathf.Max(EchoProjectileCount, bonus.EchoProjectileCount);
                EchoDamageRatio = Mathf.Max(EchoDamageRatio, bonus.EchoDamageRatio);
                EchoSearchRadius = Mathf.Max(EchoSearchRadius, bonus.EchoSearchRadius);
            }
        }
    }
}
