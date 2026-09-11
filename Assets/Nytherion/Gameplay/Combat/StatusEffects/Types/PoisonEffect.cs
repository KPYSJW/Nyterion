using UnityEngine;

namespace Nytherion.GamePlay.Combat
{
    public class PoisonEffect : StatusEffect
    {
        public override string EffectId => "Poison";
        public override Color EffectColor => new Color(0.2f, 0.8f, 0.2f); // 독성 초록색

        private const int MaximumStacks = 5;

        private float baseDamagePerStack;
        private float setDamageMultiplier = 1f;
        private int stackCount = 1;
        private float tickInterval = 1.0f;
        private float nextTickTime;

        public float BaseDamagePerStack => baseDamagePerStack;
        public float DamagePerStack => baseDamagePerStack * setDamageMultiplier;
        public float TickDamage => DamagePerStack * stackCount;
        public int StackCount => stackCount;

        public PoisonEffect(float damage, float duration, int initialStackCount = 1)
        {
            baseDamagePerStack = Mathf.Max(0f, damage);
            this.Duration = duration;
            stackCount = Mathf.Clamp(initialStackCount, 1, MaximumStacks);
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

        public void ApplySetBonus(float durationMultiplier, float damageMultiplier)
        {
            ModifyDuration(Duration * Mathf.Max(0f, durationMultiplier));
            setDamageMultiplier = Mathf.Max(0f, damageMultiplier);
        }

        public override void OnStack(StatusEffect newEffect)
        {
            if (!(newEffect is PoisonEffect poisonEffect)) return;

            int availableStacks = MaximumStacks - stackCount;
            stackCount += Mathf.Min(availableStacks, poisonEffect.stackCount);
            baseDamagePerStack = Mathf.Max(baseDamagePerStack, poisonEffect.baseDamagePerStack);
            setDamageMultiplier = Mathf.Max(setDamageMultiplier, poisonEffect.setDamageMultiplier);
            Duration = Mathf.Max(Duration, poisonEffect.Duration);
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
            // 독 만료 시 처리
        }
    }
}
