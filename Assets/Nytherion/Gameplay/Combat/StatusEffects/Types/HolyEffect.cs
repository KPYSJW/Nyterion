using UnityEngine;
using Nytherion.Core.Managers;

namespace Nytherion.GamePlay.Combat
{
    public class HolyEffect : StatusEffect
    {
        public override string EffectId => "Holy";
        public override Color EffectColor => new Color(1.0f, 0.9f, 0.5f); // 신성 황금색

        private float outgoingDamageMultiplier = 0.9f; // 적이 가하는 피해 10% 감소
        private float healChance = 0.15f; // 15% 확률
        private float healAmount = 1.0f; // 체력 1 회복

        public float OutgoingDamageMultiplier => outgoingDamageMultiplier;

        public HolyEffect(float duration)
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
            int level = modifiers.GetActiveLevel("Sacred Grail");
            if (level > 0)
            {
                float bonusReduction = 0.1f + (level - 1) * 0.02f;
                outgoingDamageMultiplier = Mathf.Max(0.5f, 0.9f - bonusReduction);
                healAmount += 1f + (level - 1) * 0.5f;
                return;
            }

            level = modifiers.GetActiveLevel("Archangel's Halo");
            if (level > 0)
            {
                float bonusReduction = 0.25f + (level - 1) * 0.05f;
                outgoingDamageMultiplier = Mathf.Max(0.4f, 0.9f - bonusReduction);
                healAmount += 2f + (level - 1);
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

        public void TriggerHealChance()
        {
            if (Random.value <= healChance)
            {
                PlayerManager playerManager = manager != null ? manager.PlayerManager : null;
                if (playerManager != null && playerManager.playerHealth != null)
                {
                    playerManager.playerHealth.Heal(healAmount);
                    Debug.Log($"[Holy] 신성 가호 효과 발동! 플레이어 체력 {healAmount} 회복.");
                }
            }
        }
    }
}
