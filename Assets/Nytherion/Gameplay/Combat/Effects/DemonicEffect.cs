using UnityEngine;

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
            if (manager != null)
            {
                manager.PlayVFX(EffectId);
            }
        }

        public override void ApplyRelicModifiers(CombatModifierSnapshot modifiers)
        {
            int level = modifiers.GetActiveLevel("Demonic Pact");
            if (level > 0)
            {
                ApplyBonuses(level, 0.1f, 0.03f, 0.2f, 0.05f);
                return;
            }

            level = modifiers.GetActiveLevel("Horn of the Abyss");
            if (level > 0)
            {
                ApplyBonuses(level, 0.15f, 0.04f, 0.3f, 0.07f);
                return;
            }

            level = modifiers.GetActiveLevel("Eye of the Archdemon");
            if (level > 0)
            {
                ApplyBonuses(level, 0.25f, 0.05f, 0.4f, 0.1f);
            }
        }

        private void ApplyBonuses(int level, float defenseBase, float defensePerLevel, float critBase, float critPerLevel)
        {
            float bonusDefense = defenseBase + (level - 1) * defensePerLevel;
            defenseReduction = Mathf.Min(0.8f, 0.1f + bonusDefense);
            extraCritDamageMultiplier += critBase + (level - 1) * critPerLevel;
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
