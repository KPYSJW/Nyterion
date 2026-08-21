using UnityEngine;

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
            nextTickTime = Time.time + tickInterval;
            if (manager != null)
            {
                manager.PlayVFX(EffectId);
            }
        }

        public override void ApplyRelicModifiers(CombatModifierSnapshot modifiers)
        {
            float durationMultiplier = 1f;
            int venomHourglassLevel = modifiers.GetActiveLevel("Venom Hourglass");
            if (venomHourglassLevel > 0)
            {
                durationMultiplier += 0.5f + (venomHourglassLevel - 1) * 0.1f;
            }

            int hydraLevel = modifiers.GetActiveLevel("Hydra's Fang");
            if (hydraLevel > 0)
            {
                durationMultiplier += 0.3f + (hydraLevel - 1) * 0.05f;
            }

            if (durationMultiplier > 1f)
            {
                ModifyDuration(Duration * durationMultiplier);
            }

            int catalystLevel = modifiers.GetActiveLevel("Toxic Catalyst");
            if (catalystLevel > 0)
            {
                float reduction = 0.3f + (catalystLevel - 1) * 0.05f;
                tickInterval = 1f * (1f - Mathf.Clamp(reduction, 0f, 0.6f));
            }
            else if (hydraLevel > 0)
            {
                float reduction = 0.1f + (hydraLevel - 1) * 0.02f;
                tickInterval = 1f * (1f - Mathf.Clamp(reduction, 0f, 0.3f));
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
