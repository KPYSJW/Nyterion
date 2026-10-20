using System;
using System.Collections.Generic;
using Nytherion.Core.Managers;
using Nytherion.Core.Utils;
using UnityEngine;

namespace Nytherion.Gameplay.Relics.Modules
{
    /// <summary>
    /// 운명의 육면체 세트 단계에 따라 직접 적중 피해를 무작위로 증감한다.
    /// </summary>
    [Serializable, RelicDisplayName("운명의 육면체 무작위 추가 피해")]
    public class PipCompletionEffect : RelicEffectBase
    {
        [Range(-1f, 1f)] public float minimumBonusRatio;
        [Range(-1f, 1f)] public float maximumBonusRatio = 0.6f;

        [NonSerialized] private PipCompletionRuntime appliedRuntime;

        public override void ApplyEffect(PlayerManager playerManager, int level)
        {
            RemoveEffect(playerManager, level);
            if (playerManager == null) return;

            appliedRuntime = playerManager.GetComponent<PipCompletionRuntime>();
            if (appliedRuntime == null)
            {
                appliedRuntime = playerManager.gameObject.AddComponent<PipCompletionRuntime>();
            }
            appliedRuntime.SetBonus(this, minimumBonusRatio, maximumBonusRatio);
        }

        public override void RemoveEffect(PlayerManager playerManager, int level)
        {
            if (appliedRuntime == null && playerManager != null)
            {
                appliedRuntime = playerManager.GetComponent<PipCompletionRuntime>();
            }
            appliedRuntime?.RemoveBonus(this);
            appliedRuntime = null;
        }
    }

    public sealed class PipCompletionRuntime : MonoBehaviour
    {
        private readonly Dictionary<PipCompletionEffect, Vector2> bonuses =
            new Dictionary<PipCompletionEffect, Vector2>();

        public float MinimumBonusRatio { get; private set; }
        public float MaximumBonusRatio { get; private set; }

        public void SetBonus(PipCompletionEffect source, float minimumBonusRatio, float maximumBonusRatio)
        {
            if (source == null) return;
            float minimum = Mathf.Clamp(minimumBonusRatio, -1f, 1f);
            bonuses[source] = new Vector2(minimum, Mathf.Clamp(maximumBonusRatio, minimum, 1f));
            Recalculate();
        }

        public void RemoveBonus(PipCompletionEffect source)
        {
            if (source == null || !bonuses.Remove(source)) return;
            Recalculate();
        }

        public float RollDamageMultiplier()
        {
            return bonuses.Count > 0
                ? 1f + UnityEngine.Random.Range(MinimumBonusRatio, MaximumBonusRatio)
                : 1f;
        }

        private void Recalculate()
        {
            MinimumBonusRatio = 0f;
            MaximumBonusRatio = 0f;
            bool hasBonus = false;
            foreach (Vector2 bonus in bonuses.Values)
            {
                // 여러 출처가 남아도 범위를 합산하지 않고 가장 높은 단계의 범위 한 쌍만 사용합니다.
                if (!hasBonus || bonus.y > MaximumBonusRatio ||
                    (Mathf.Approximately(bonus.y, MaximumBonusRatio) && bonus.x > MinimumBonusRatio))
                {
                    MinimumBonusRatio = bonus.x;
                    MaximumBonusRatio = bonus.y;
                    hasBonus = true;
                }
            }
        }
    }
}
