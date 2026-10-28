using System;
using Nytherion.Core.Enums;
using Nytherion.Core.Managers;
using Nytherion.Core.Utils;
using Nytherion.GamePlay.Characters.Player;
using UnityEngine;

namespace Nytherion.Gameplay.Relics.Modules
{
    [Serializable, RelicDisplayName("연속 피격 시 임시 방어력 증가")]
    public sealed class ConsecutiveHitDefenseEffect : RelicEffectBase
    {
        public float defensePerHit = 3f;
        public float defensePerLevel = 1f;
        [Min(1)] public int maxStacks = 3;
        [Min(0.1f)] public float duration = 4f;
        [NonSerialized] private PlayerManager player;
        [NonSerialized] private PlayerHealth health;
        [NonSerialized] private TimedRelicStatBuffRuntime runtime;
        [NonSerialized] private float bonus;

        public override void ApplyEffect(PlayerManager playerManager, int level)
        {
            RemoveEffect(player, level);
            if (playerManager == null) return;
            player = playerManager;
            health = player.GetComponent<PlayerHealth>();
            runtime = player.GetComponent<TimedRelicStatBuffRuntime>();
            if (runtime == null) runtime = player.gameObject.AddComponent<TimedRelicStatBuffRuntime>();
            bonus = Mathf.Max(0f, defensePerHit + defensePerLevel * Mathf.Max(0, level - 1));
            if (health != null) health.DamageTaken += HandleDamage;
        }

        private void HandleDamage(float damage)
        {
            if (damage > 0f && runtime != null)
                runtime.Trigger(this, player, StatType.Defense, bonus, false, duration, maxStacks);
        }

        public override void RemoveEffect(PlayerManager playerManager, int level)
        {
            if (health != null) health.DamageTaken -= HandleDamage;
            if (runtime != null) runtime.Remove(this);
            player = null;
            health = null;
            runtime = null;
        }
    }
}
