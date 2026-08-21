using UnityEngine;

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
            if (manager != null)
            {
                manager.PlayVFX(EffectId);
            }
        }

        public override void ApplyRelicModifiers(CombatModifierSnapshot modifiers)
        {
            int level = modifiers.GetActiveLevel("Seal of the Abyss");
            if (level > 0)
            {
                damageMultiplier += 0.1f + (level - 1) * 0.03f;
                return;
            }

            level = modifiers.GetActiveLevel("Shackles of Ruin");
            if (level > 0)
            {
                damageMultiplier += 0.15f + (level - 1) * 0.04f;
                return;
            }

            level = modifiers.GetActiveLevel("Cursed Crown");
            if (level > 0)
            {
                damageMultiplier += 0.25f + (level - 1) * 0.05f;
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
