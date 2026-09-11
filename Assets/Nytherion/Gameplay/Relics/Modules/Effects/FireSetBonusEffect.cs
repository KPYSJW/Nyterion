using System;
using System.Collections.Generic;
using Nytherion.Core.Managers;
using Nytherion.Core.Utils;
using Nytherion.GamePlay.Combat;
using UnityEngine;

namespace Nytherion.Gameplay.Relics.Modules
{
    /// <summary>
    /// 화상 피해, 지속 시간, 피해 주기와 최대 중첩 수를 강화한다.
    /// </summary>
    [Serializable, RelicDisplayName("화염술사 세트 강화")]
    public class FireSetBonusEffect : RelicEffectBase
    {
        [Min(0f)] public float durationMultiplier = 1f;
        [Min(0f)] public float tickDamageMultiplier = 1f;
        [Range(0.1f, 1f)] public float tickIntervalMultiplier = 1f;
        [Min(1)] public int maxStacks = 1;

        [NonSerialized] private FireSetBonusRuntime appliedRuntime;

        public override void ApplyEffect(PlayerManager playerManager, int level)
        {
            RemoveEffect(playerManager, level);
            if (playerManager == null) return;

            appliedRuntime = playerManager.GetComponent<FireSetBonusRuntime>();
            if (appliedRuntime == null)
            {
                appliedRuntime = playerManager.gameObject.AddComponent<FireSetBonusRuntime>();
            }

            appliedRuntime.SetBonus(this, new FireSetBonusValues
            {
                DurationMultiplier = durationMultiplier,
                TickDamageMultiplier = tickDamageMultiplier,
                TickIntervalMultiplier = tickIntervalMultiplier,
                MaxStacks = maxStacks
            });
        }

        public override void RemoveEffect(PlayerManager playerManager, int level)
        {
            if (appliedRuntime == null && playerManager != null)
            {
                appliedRuntime = playerManager.GetComponent<FireSetBonusRuntime>();
            }

            appliedRuntime?.RemoveBonus(this);
            appliedRuntime = null;
        }
    }

    public struct FireSetBonusValues
    {
        public float DurationMultiplier;
        public float TickDamageMultiplier;
        public float TickIntervalMultiplier;
        public int MaxStacks;
    }

    public sealed class FireSetBonusRuntime : MonoBehaviour
    {
        private readonly Dictionary<FireSetBonusEffect, FireSetBonusValues> bonuses =
            new Dictionary<FireSetBonusEffect, FireSetBonusValues>();

        public float DurationMultiplier { get; private set; } = 1f;
        public float TickDamageMultiplier { get; private set; } = 1f;
        public float TickIntervalMultiplier { get; private set; } = 1f;
        public int MaxStacks { get; private set; } = 1;

        public void SetBonus(FireSetBonusEffect source, FireSetBonusValues values)
        {
            if (source == null) return;

            bonuses[source] = values;
            Recalculate();
        }

        public void RemoveBonus(FireSetBonusEffect source)
        {
            if (source == null || !bonuses.Remove(source)) return;
            Recalculate();
        }

        public void ApplyTo(FireEffect fireEffect)
        {
            if (fireEffect == null) return;

            fireEffect.ApplySetBonus(
                DurationMultiplier,
                TickDamageMultiplier,
                TickIntervalMultiplier,
                MaxStacks);
        }

        private void Recalculate()
        {
            DurationMultiplier = 1f;
            TickDamageMultiplier = 1f;
            TickIntervalMultiplier = 1f;
            MaxStacks = 1;

            foreach (FireSetBonusValues bonus in bonuses.Values)
            {
                DurationMultiplier = Mathf.Max(DurationMultiplier, bonus.DurationMultiplier);
                TickDamageMultiplier = Mathf.Max(TickDamageMultiplier, bonus.TickDamageMultiplier);
                TickIntervalMultiplier = Mathf.Min(
                    TickIntervalMultiplier,
                    Mathf.Clamp(bonus.TickIntervalMultiplier, 0.1f, 1f));
                MaxStacks = Mathf.Max(MaxStacks, bonus.MaxStacks);
            }
        }
    }
}
