using UnityEngine;

namespace Nytherion.GamePlay.Combat
{
    public class LightningEffect : StatusEffect
    {
        public override string EffectId => "Lightning";

        public LightningEffect(float duration)
        {
            Duration = duration;
        }

        public override void OnApply() { }
        public override void OnUpdate(float deltaTime) { }
        public override void OnRemove() => manager?.StopVFX(EffectId);

        public void ApplyBonusDamage(float damage)
        {
            if (target == null || target.isDead) return;
            var data = manager != null ? manager.Database : null;
            float ratio = data != null ? data.lightningBonusDamageRatio : 0.3f;
            manager?.PlayVFX(EffectId);
            // 추가 피해는 연쇄 피해로 처리해 감전과 적중 효과를 재귀 발동하지 않습니다.
            target.TakeDamage(Mathf.Max(1f, damage * ratio), true);
        }
    }
}