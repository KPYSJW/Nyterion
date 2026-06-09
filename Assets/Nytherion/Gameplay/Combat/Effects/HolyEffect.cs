using UnityEngine;
using Nytherion.Core.Managers;

namespace Nytherion.GamePlay.Combat
{
    public class HolyEffect : StatusEffect
    {
        public override string EffectId => "Holy";
        public override Color EffectColor => new Color(1.0f, 0.9f, 0.5f); // 신성 황금색

        private float outgoingDamageMultiplier = 0.8f; // 적이 가하는 피해 20% 감소
        private float healChance = 0.15f; // 15% 확률
        private float healAmount = 2.0f; // 체력 2 회복

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
                PlayerManager playerManager = Object.FindObjectOfType<PlayerManager>();
                if (playerManager != null && playerManager.playerHealth != null)
                {
                    playerManager.playerHealth.Heal(healAmount);
                    Debug.Log($"[Holy] 신성 가호 효과 발동! 플레이어 체력 {healAmount} 회복.");
                }
            }
        }
    }
}
