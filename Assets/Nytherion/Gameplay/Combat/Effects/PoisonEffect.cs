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
