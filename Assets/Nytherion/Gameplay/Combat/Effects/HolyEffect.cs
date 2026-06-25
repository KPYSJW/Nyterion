using UnityEngine;
using System.Collections.Generic;
using Nytherion.Core.Managers;
using Nytherion.Core.Data;
using Nytherion.GamePlay.Relics;

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
            AdjustHolyStats();
            if (manager != null)
            {
                manager.PlayVFX(EffectId);
            }
        }

        private void AdjustHolyStats()
        {
            Nytherion.Core.Managers.RelicManager relicManager = UnityEngine.Object.FindObjectOfType<Nytherion.Core.Managers.RelicManager>();
            if (relicManager != null)
            {
                foreach (KeyValuePair<string, Vector2Int> pair in relicManager.GetPlacedBlocks())
                {
                    RelicBlock block = relicManager.GetBlockAt(pair.Value.y, pair.Value.x);
                    if (block != null && !block.SourceData.isDisabled)
                    {
                        if (block.RelicId == "Sacred Grail")
                        {
                            float bonusReduction = 0.1f + (block.SourceData.level - 1) * 0.02f;
                            outgoingDamageMultiplier = Mathf.Max(0.5f, 0.9f - bonusReduction);

                            float bonusHeal = 1.0f + (block.SourceData.level - 1) * 0.5f;
                            healAmount += bonusHeal;
                            break;
                        }
                        else if (block.RelicId == "Archangel's Halo")
                        {
                            float bonusReduction = 0.25f + (block.SourceData.level - 1) * 0.05f;
                            outgoingDamageMultiplier = Mathf.Max(0.4f, 0.9f - bonusReduction);

                            float bonusHeal = 2.0f + (block.SourceData.level - 1) * 1.0f;
                            healAmount += bonusHeal;
                            break;
                        }
                    }
                }
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
