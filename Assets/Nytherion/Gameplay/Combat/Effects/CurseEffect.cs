using UnityEngine;
using System.Collections.Generic;
using Nytherion.Core.Data;
using Nytherion.GamePlay.Relics;

namespace Nytherion.GamePlay.Combat
{
    public class CurseEffect : StatusEffect
    {
        public override string EffectId => "Curse";
        public override Color EffectColor => new Color(0.4f, 0.2f, 0.6f); // 저주 어두운 보라색

        private float damageMultiplier = 1.1f; // 받는 피해 10% 증가

        public float DamageMultiplier => damageMultiplier;

        public CurseEffect(float multiplier, float duration)
        {
            this.damageMultiplier = multiplier;
            this.Duration = duration;
        }

        public override void OnApply()
        {
            AdjustCurseMultiplier();
            if (manager != null)
            {
                manager.PlayVFX(EffectId);
            }
        }

        private void AdjustCurseMultiplier()
        {
            Nytherion.Core.Managers.RelicManager relicManager = UnityEngine.Object.FindObjectOfType<Nytherion.Core.Managers.RelicManager>();
            if (relicManager != null)
            {
                foreach (KeyValuePair<string, Vector2Int> pair in relicManager.GetPlacedBlocks())
                {
                    RelicBlock block = relicManager.GetBlockAt(pair.Value.y, pair.Value.x);
                    if (block != null && !block.SourceData.isDisabled)
                    {
                        if (block.RelicId == "Seal of the Abyss")
                        {
                            float bonus = 0.1f + (block.SourceData.level - 1) * 0.03f;
                            damageMultiplier += bonus;
                            break;
                        }
                        else if (block.RelicId == "Shackles of Ruin")
                        {
                            float bonus = 0.15f + (block.SourceData.level - 1) * 0.04f;
                            damageMultiplier += bonus;
                            break;
                        }
                        else if (block.RelicId == "Cursed Crown")
                        {
                            float bonus = 0.25f + (block.SourceData.level - 1) * 0.05f;
                            damageMultiplier += bonus;
                            break;
                        }
                    }
                }
            }
        }

        public override void OnUpdate(float deltaTime)
        {
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
