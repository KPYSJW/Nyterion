using System;
using Nytherion.Core.Managers;
using Nytherion.Core.Utils;
using Nytherion.GamePlay.Characters.Player;
using UnityEngine;

namespace Nytherion.Gameplay.Relics.Modules
{
    [Serializable, RelicDisplayName("적중 시 마나 회복")]
    public sealed class ManaOnHitEffect : RelicEffectBase
    {
        [Min(0f)] public float manaPerHit = 2f;
        [Min(0f)] public float manaPerLevel = 0.5f;
        [NonSerialized] private EventManager events;
        [NonSerialized] private PlayerSkillManager skills;
        [NonSerialized] private PlayerHealth health;
        [NonSerialized] private float recovery;

        public override void ApplyEffect(PlayerManager playerManager, int level)
        {
            RemoveEffect(playerManager, level);
            if (playerManager == null) return;
            skills = playerManager.GetComponent<PlayerSkillManager>();
            health = playerManager.GetComponent<PlayerHealth>();
            events = playerManager.EventManager;
            recovery = Mathf.Max(0f, manaPerHit + manaPerLevel * Mathf.Max(0, level - 1));
            if (events != null && skills != null) events.OnEnemyDamagedByPlayerDetailed += HandleHit;
        }

        private void HandleHit(PlayerDamageEventData hit)
        {
            if (skills == null || !skills.isActiveAndEnabled || (health != null && health.CurrentHealth <= 0f) ||
                hit.Target == null || hit.DamageAmount <= 0f || hit.IsDamageOverTime || hit.IsChainDamage) return;
            skills.RestoreMana(recovery);
        }

        public override void RemoveEffect(PlayerManager playerManager, int level)
        {
            if (events != null) events.OnEnemyDamagedByPlayerDetailed -= HandleHit;
            events = null;
            skills = null;
            health = null;
            recovery = 0f;
        }
    }
}
