using UnityEngine;
using System.Collections.Generic;
using Nytherion.Core.Data;
using Nytherion.GamePlay.Relics;

namespace Nytherion.GamePlay.Combat
{
    public class DemonicEffect : StatusEffect
    {
        public override string EffectId => "Demonic";
        public override Color EffectColor => new Color(0.6f, 0.1f, 0.6f); // 마성 자주색

        private float defenseReduction = 0.10f; // 방어력 10% 감소
        private float extraCritDamageMultiplier = 0.20f; // 치명타 피해 20%p 증가

        public float DefenseReduction => defenseReduction;
        public float ExtraCritDamageMultiplier => extraCritDamageMultiplier;

        public DemonicEffect(float duration)
        {
            this.Duration = duration;
        }

        public override void OnApply()
        {
            AdjustDemonicStats();
            if (manager != null)
            {
                manager.PlayVFX(EffectId);
            }
        }

        private void AdjustDemonicStats()
        {
            Nytherion.Core.Managers.RelicManager relicManager = UnityEngine.Object.FindObjectOfType<Nytherion.Core.Managers.RelicManager>();
            if (relicManager != null)
            {
                foreach (KeyValuePair<string, Vector2Int> pair in relicManager.GetPlacedBlocks())
                {
                    RelicBlock block = relicManager.GetBlockAt(pair.Value.y, pair.Value.x);
                    if (block != null && !block.SourceData.isDisabled)
                    {
                        if (block.RelicId == "Demonic Pact")
                        {
                            float bonusDef = 0.1f + (block.SourceData.level - 1) * 0.03f;
                            defenseReduction = Mathf.Min(0.8f, 0.10f + bonusDef);

                            float bonusCrit = 0.2f + (block.SourceData.level - 1) * 0.05f;
                            extraCritDamageMultiplier += bonusCrit;
                            break;
                        }
                        else if (block.RelicId == "Horn of the Abyss")
                        {
                            float bonusDef = 0.15f + (block.SourceData.level - 1) * 0.04f;
                            defenseReduction = Mathf.Min(0.8f, 0.10f + bonusDef);

                            float bonusCrit = 0.3f + (block.SourceData.level - 1) * 0.07f;
                            extraCritDamageMultiplier += bonusCrit;
                            break;
                        }
                        else if (block.RelicId == "Eye of the Archdemon")
                        {
                            float bonusDef = 0.25f + (block.SourceData.level - 1) * 0.05f;
                            defenseReduction = Mathf.Min(0.8f, 0.10f + bonusDef);

                            float bonusCrit = 0.4f + (block.SourceData.level - 1) * 0.1f;
                            extraCritDamageMultiplier += bonusCrit;
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
