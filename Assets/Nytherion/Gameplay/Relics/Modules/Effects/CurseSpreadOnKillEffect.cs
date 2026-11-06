using System;
using System.Collections.Generic;
using Nytherion.Core.Managers;
using Nytherion.Core.Utils;
using Nytherion.GamePlay.Characters.Enemy;
using Nytherion.GamePlay.Characters.Player;
using Nytherion.GamePlay.Combat;
using UnityEngine;

namespace Nytherion.Gameplay.Relics.Modules
{
    [Serializable, RelicDisplayName("저주받은 적 처치 시 저주 전파")]
    public sealed class CurseSpreadOnKillEffect : RelicEffectBase
    {
        public float radius = 3f;
        public float radiusPerLevel = 0.25f;
        [NonSerialized] private EventManager events;
        [NonSerialized] private PlayerManager player;
        [NonSerialized] private PlayerHealth health;
        [NonSerialized] private float spreadRadius;
        [NonSerialized] private HashSet<EnemyBase> visited;

        public override void ApplyEffect(PlayerManager playerManager, int level)
        {
            RemoveEffect(player, level);
            if (playerManager == null) return;
            player = playerManager;
            health = player.GetComponent<PlayerHealth>();
            events = player.EventManager;
            spreadRadius = Mathf.Max(0.1f, radius + radiusPerLevel * Mathf.Max(0, level - 1));
            visited = new HashSet<EnemyBase>();
            if (events != null) events.OnEnemyDied += HandleDeath;
        }

        private void HandleDeath(EnemyBase defeated)
        {
            if (defeated == null || !defeated.isDead || player == null || !player.isActiveAndEnabled ||
                (health != null && health.CurrentHealth <= 0f) ||
                !defeated.TryGetComponent(out StatusEffectManager source) ||
                !source.TryGetEffect(out CurseEffect curse) || curse.Timer <= 0f) return;

            visited.Clear();
            foreach (Collider2D hit in Physics2D.OverlapCircleAll(defeated.transform.position, spreadRadius,
                LayerMask.GetMask("Enemy", "FlyingBody")))
            {
                EnemyBase target = hit.GetComponentInParent<EnemyBase>();
                if (target == null || target == defeated || target.isDead || !target.isActiveAndEnabled ||
                    !visited.Add(target) || !target.TryGetComponent(out StatusEffectManager status) ||
                    status.HasEffect("Curse")) continue;
                status.ConfigureCombatContext(player);
                // 기본 배율을 복사해 전파할 때마다 유물 보정이 다시 누적되지 않게 합니다.
                status.ApplyEffect(new CurseEffect(curse.BaseDamageMultiplier, curse.Timer));
            }
        }

        public override void RemoveEffect(PlayerManager playerManager, int level)
        {
            if (events != null) events.OnEnemyDied -= HandleDeath;
            events = null;
            player = null;
            health = null;
            visited?.Clear();
        }
    }
}
