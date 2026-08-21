using UnityEngine;

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
            nextTickTime = Time.time + tickInterval;
            if (manager != null)
            {
                manager.PlayVFX(EffectId);
            }
        }

        public override void ApplyRelicModifiers(CombatModifierSnapshot modifiers)
        {
            int durationLevel = modifiers.GetActiveLevel("Sulphur Hourglass");
            if (durationLevel > 0)
            {
                float durationMultiplier = 1.5f + (durationLevel - 1) * 0.1f;
                ModifyDuration(Duration * durationMultiplier);
            }

            int catalystLevel = modifiers.GetActiveLevel("Thermal Catalyst");
            if (catalystLevel > 0)
            {
                float reduction = 0.3f + (catalystLevel - 1) * 0.05f;
                tickInterval = 0.5f * (1f - Mathf.Clamp(reduction, 0f, 0.6f));
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
