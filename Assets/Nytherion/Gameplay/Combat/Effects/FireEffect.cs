using UnityEngine;
using System.Collections.Generic;
using Nytherion.Core.Data;
using Nytherion.GamePlay.Relics;

namespace Nytherion.GamePlay.Combat
{
    public class FireEffect : StatusEffect
    {
        public override string EffectId => "Fire";
        public override Color EffectColor => new Color(1.0f, 0.4f, 0.2f); // 주황빛 붉은색

        private float tickDamage;
        private float tickInterval = 0.5f;
        private float nextTickTime;

        public FireEffect(float damage, float duration)
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
                    if (block != null && block.RelicId == "Thermal Catalyst" && !block.SourceData.isDisabled)
                    {
                        float reduction = 0.3f + (block.SourceData.level - 1) * 0.05f;
                        reduction = Mathf.Clamp(reduction, 0f, 0.6f);
                        tickInterval = 0.5f * (1f - reduction);
                        break;
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
        }
    }
}
