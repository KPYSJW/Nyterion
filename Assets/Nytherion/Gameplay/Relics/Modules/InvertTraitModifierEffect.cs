using System;
using System.Collections.Generic;
using UnityEngine;
using Nytherion.Core.Enums;
using Nytherion.Core.Managers;

namespace Nytherion.Gameplay.Relics.Modules
{
    /// <summary>
    /// 특정 특성(Trait)을 가진 장비의 모든 마이너스(-) 스탯을 플러스(+)로 강제 반전시킵니다.
    /// 예: [Cursed] 태그 장비의 공격력 -10 옵션을 공격력 +10으로 변환
    /// </summary>
    [Serializable]
    public class InvertTraitModifierEffect : RelicEffectBase
    {
        [Tooltip("마이너스 스탯을 반전시킬 대상 장비의 특성(태그)")]
        public EquipmentTrait targetTrait;

        public override void ApplyEffect(PlayerManager playerManager, int level)
        {
            if (playerManager == null) return;
            
            // PlayerManager에게 특정 태그의 반전을 지시
            playerManager.AddTraitInversion(targetTrait);
            Debug.Log($"[InvertTraitEffect] {targetTrait} 장비의 마이너스 스탯이 반전됩니다.");
        }

        public override void RemoveEffect(PlayerManager playerManager, int level)
        {
            if (playerManager == null) return;

            playerManager.RemoveTraitInversion(targetTrait);
            Debug.Log($"[InvertTraitEffect] {targetTrait} 장비의 스탯 반전이 해제되었습니다.");
        }
    }
}