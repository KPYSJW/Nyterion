using UnityEngine;

namespace Nytherion.GamePlay.Combat
{
    public class CurseEffect : StatusEffect
    {
        public override string EffectId => "Curse";
        public override Color EffectColor => new Color(0.4f, 0.2f, 0.6f); // 저주 어두운 보라색

        private float damageMultiplier = 1.3f; // 받는 피해 30% 증가

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
