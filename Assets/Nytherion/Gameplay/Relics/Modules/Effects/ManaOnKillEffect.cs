using System;
using Nytherion.Core.Managers;
using Nytherion.Core.Utils;
using Nytherion.GamePlay.Characters.Enemy;
using Nytherion.GamePlay.Characters.Player;
using UnityEngine;

namespace Nytherion.Gameplay.Relics.Modules
{
    [Serializable, RelicDisplayName("적 처치 시 마나 회복")]
    public sealed class ManaOnKillEffect : RelicEffectBase
    {
        public float manaPerKill = 3f;
        public float manaPerLevel = 1f;
        [NonSerialized] private EventManager events;
        [NonSerialized] private PlayerSkillManager skills;
        [NonSerialized] private PlayerHealth health;
        [NonSerialized] private float recovery;

        public override void ApplyEffect(PlayerManager playerManager, int level)
        {
            RemoveEffect(playerManager, level);
            if (playerManager == null) return;
            events = playerManager.EventManager;
            skills = playerManager.GetComponent<PlayerSkillManager>();
            health = playerManager.GetComponent<PlayerHealth>();
            recovery = Mathf.Max(0f, manaPerKill + manaPerLevel * Mathf.Max(0, level - 1));
            if (events != null && skills != null) events.OnEnemyDied += HandleDeath;
        }

        private void HandleDeath(EnemyBase enemy)
        {
            if (enemy == null || !enemy.isDead || skills == null || !skills.isActiveAndEnabled ||
                (health != null && health.CurrentHealth <= 0f)) return;
            skills.RestoreMana(recovery);
        }

        public override void RemoveEffect(PlayerManager playerManager, int level)
        {
            if (events != null) events.OnEnemyDied -= HandleDeath;
            events = null;
            skills = null;
            health = null;
        }
    }
}
