using UnityEngine;

namespace Nytherion.GamePlay.Combat
{
    public class FireEffect : StatusEffect
    {
        public override string EffectId => "Fire";
        public override Color EffectColor => new Color(1.0f, 0.4f, 0.2f); // 주황빛 붉은색

        private float baseTickDamage;
        private float setDamageMultiplier = 1f;
        private int stackCount = 1;
        private int maximumStacks = 1;
        private float tickInterval = 0.5f;
        private float nextTickTime;

        public float TickDamage => baseTickDamage * setDamageMultiplier * stackCount;
        public int StackCount => stackCount;

        public FireEffect(float damage, float duration)
        {
            baseTickDamage = Mathf.Max(0f, damage);
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

        public void ApplySetBonus(
            float durationMultiplier,
            float damageMultiplier,
            float intervalMultiplier,
            int maxStacks)
        {
            ModifyDuration(Duration * Mathf.Max(0f, durationMultiplier));
            setDamageMultiplier = Mathf.Max(0f, damageMultiplier);
            tickInterval *= Mathf.Clamp(intervalMultiplier, 0.1f, 1f);
            maximumStacks = Mathf.Max(1, maxStacks);
        }

        public override void OnStack(StatusEffect newEffect)
        {
            if (!(newEffect is FireEffect fireEffect)) return;

            maximumStacks = Mathf.Max(maximumStacks, fireEffect.maximumStacks);
            stackCount = Mathf.Min(maximumStacks, stackCount + fireEffect.stackCount);
            baseTickDamage = Mathf.Max(baseTickDamage, fireEffect.baseTickDamage);
            setDamageMultiplier = Mathf.Max(setDamageMultiplier, fireEffect.setDamageMultiplier);
            tickInterval = Mathf.Min(tickInterval, fireEffect.tickInterval);
            Duration = Mathf.Max(Duration, fireEffect.Duration);
            Timer = Duration;
        }

        public override void OnUpdate(float deltaTime)
        {
            if (Time.time >= nextTickTime)
            {
                if (target != null && !target.isDead)
                {
                    target.TakeDamage(TickDamage);
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
