using System;
using Nytherion.Core.Enums;
using Nytherion.Core.Managers;
using Nytherion.Core.Utils;
using Nytherion.GamePlay.Characters.Player;
using Nytherion.GamePlay.Skills;
using UnityEngine;

namespace Nytherion.Gameplay.Relics.Modules
{
    [Serializable, RelicDisplayName("스킬 사용 후 공격 속도 증가")]
    public sealed class SkillUseAttackSpeedEffect : RelicEffectBase
    {
        public float attackSpeedBonus = 0.2f;
        public float bonusPerLevel = 0.04f;
        [Min(0.1f)] public float duration = 3f;
        [NonSerialized] private PlayerManager player;
        [NonSerialized] private PlayerSkillManager skills;
        [NonSerialized] private TimedRelicStatBuffRuntime runtime;
        [NonSerialized] private float bonus;

        public override void ApplyEffect(PlayerManager playerManager, int level)
        {
            RemoveEffect(player, level);
            if (playerManager == null) return;
            player = playerManager;
            skills = player.GetComponent<PlayerSkillManager>();
            runtime = player.GetComponent<TimedRelicStatBuffRuntime>();
            if (runtime == null) runtime = player.gameObject.AddComponent<TimedRelicStatBuffRuntime>();
            bonus = Mathf.Max(0f, attackSpeedBonus + bonusPerLevel * Mathf.Max(0, level - 1));
            if (skills != null) skills.SkillUsed += HandleSkillUsed;
        }

        private void HandleSkillUsed(SkillBase skill)
        {
            if (runtime != null) runtime.Trigger(this, player, StatType.RangedSpeed, bonus, true, duration, 1);
        }

        public override void RemoveEffect(PlayerManager playerManager, int level)
        {
            if (skills != null) skills.SkillUsed -= HandleSkillUsed;
            if (runtime != null) runtime.Remove(this);
            player = null;
            skills = null;
            runtime = null;
        }
    }
}
