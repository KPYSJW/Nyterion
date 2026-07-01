using UnityEngine;
using System.Collections.Generic;
using Nytherion.Core.Data;
using Nytherion.GamePlay.Relics;

namespace Nytherion.GamePlay.Combat
{
    public class PoisonEffect : StatusEffect
    {
        public override string EffectId => "Poison";
        public override Color EffectColor => new Color(0.2f, 0.8f, 0.2f); // 독성 초록색

        private float tickDamage;
        private float tickInterval = 1.0f;
        private float nextTickTime;

        public PoisonEffect(float damage, float duration)
        {
            this.tickDamage = damage;
            this.Duration = duration;
        }

        public override void OnApply()
        {
            AdjustTickInterval();
            nextTickTime = Time.time + tickInterval;
            if (manager != null)
            {
                manager.PlayVFX(EffectId);
            }
        }

        private void AdjustTickInterval()
        {
            Nytherion.Core.Managers.RelicManager relicManager = UnityEngine.Object.FindObjectOfType<Nytherion.Core.Managers.RelicManager>();
            if (relicManager != null)
            {
                foreach (KeyValuePair<string, Vector2Int> pair in relicManager.GetPlacedBlocks())
                {
                    RelicBlock block = relicManager.GetBlockAt(pair.Value.y, pair.Value.x);
                    if (block != null && !block.SourceData.isDisabled)
                    {
                        if (block.RelicId == "Toxic Catalyst")
                        {
                            float reduction = 0.3f + (block.SourceData.level - 1) * 0.05f;
                            reduction = Mathf.Clamp(reduction, 0f, 0.6f);
                            tickInterval = 1.0f * (1f - reduction);
                            break;
                        }
                        else if (block.RelicId == "Hydra's Fang")
                        {
                            float reduction = 0.1f + (block.SourceData.level - 1) * 0.02f;
                            reduction = Mathf.Clamp(reduction, 0f, 0.3f);
                            tickInterval = 1.0f * (1f - reduction);
                            break;
                        }
                    }
                }
            }
        }

        public override void OnUpdate(float deltaTime)
        {
            if (Time.time >= nextTickTime)
            {
                if (target != null && !target.isDead)
                {
                    target.TakeDamage(tickDamage);
                }
                nextTickTime = Time.time + tickInterval;
            }
        }

        public override void OnRemove()
        {
            if (manager != null)
            {
                manager.StopVFX(EffectId);
            }
            // 독 만료 시 처리
        }
    }
}
