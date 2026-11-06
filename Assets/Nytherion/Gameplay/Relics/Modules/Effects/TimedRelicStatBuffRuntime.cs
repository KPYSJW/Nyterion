using System.Collections.Generic;
using Nytherion.Core.Data;
using Nytherion.Core.Enums;
using Nytherion.Core.Managers;
using Nytherion.GamePlay.Characters.Player;
using UnityEngine;

namespace Nytherion.Gameplay.Relics.Modules
{
    /// <summary>유물별 임시 강화의 중첩, 만료 및 비활성화 시 해제를 관리합니다.</summary>
    public sealed class TimedRelicStatBuffRuntime : MonoBehaviour
    {
        private sealed class Buff
        {
            public StatModifier Modifier;
            public int Stacks;
            public float ExpiresAt;
        }

        private PlayerManager player;
        private PlayerHealth health;
        private readonly Dictionary<object, Buff> buffs = new Dictionary<object, Buff>();
        private readonly List<object> expired = new List<object>();

        public void Trigger(object source, PlayerManager owner, StatType stat, float value,
            bool percentage, float duration, int maxStacks)
        {
            if (!isActiveAndEnabled || owner == null || !owner.isActiveAndEnabled) return;
            player = owner;
            health = owner.GetComponent<PlayerHealth>();
            if (health != null && health.CurrentHealth <= 0f) return;

            if (buffs.TryGetValue(source, out Buff buff) && Time.time >= buff.ExpiresAt)
            {
                Remove(source);
                buff = null;
            }
            if (buff == null)
            {
                buff = new Buff();
                buffs.Add(source, buff);
            }
            buff.Stacks = Mathf.Min(buff.Stacks + 1, Mathf.Max(1, maxStacks));
            buff.ExpiresAt = Time.time + Mathf.Max(0.1f, duration);
            float amount = value * buff.Stacks;
            // 최대 중첩에서는 지속 시간만 갱신해 능력치가 누적되지 않게 합니다.
            if (buff.Modifier != null && Mathf.Approximately(buff.Modifier.value, amount)) return;
            if (buff.Modifier != null) player.RemoveTemporaryStatModifier(buff.Modifier);
            buff.Modifier = new StatModifier { stat = stat, value = amount, isPercentage = percentage };
            player.AddTemporaryStatModifier(buff.Modifier);
        }

        public void Remove(object source)
        {
            if (!buffs.TryGetValue(source, out Buff buff)) return;
            buffs.Remove(source);
            if (player != null && buff.Modifier != null) player.RemoveTemporaryStatModifier(buff.Modifier);
        }

        private void Update()
        {
            expired.Clear();
            foreach (var pair in buffs)
                if (Time.time >= pair.Value.ExpiresAt || player == null || !player.isActiveAndEnabled ||
                    (health != null && health.CurrentHealth <= 0f)) expired.Add(pair.Key);
            foreach (object source in expired) Remove(source);
        }

        private void OnDisable()
        {
            expired.Clear();
            expired.AddRange(buffs.Keys);
            foreach (object source in expired) Remove(source);
        }
    }
}
