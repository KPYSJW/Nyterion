using UnityEngine;

namespace Nytherion.GamePlay.Combat
{
    public class DemonicEffect : StatusEffect
    {
        public override string EffectId => "Demonic";
        public override Color EffectColor => new Color(0.6f, 0.1f, 0.6f); // 마성 자주색

        private float defenseReduction = 0.25f; // 방어력 25% 감소
        private float extraCritDamageMultiplier = 0.5f; // 치명타 피해 50%p 증가

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
