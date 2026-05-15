using System;
using System.Linq;
using UnityEngine;
using Nytherion.Core.Enums;
using Nytherion.Core.Managers;
using Nytherion.Core.Utils;
using Nytherion.Data.ScriptableObjects.Items;

namespace Nytherion.Gameplay.Relics.Modules
{
    /// <summary>
    /// 플레이어가 특정 특성(Trait)을 가진 장비를 착용하고 있는지 검사
    /// </summary>
    [Serializable, RelicDisplayName("장비 특성 조건")]
    public class EquipmentTraitCondition : RelicConditionBase
    {
        [Tooltip("요구하는 장비 특성(태그)")]
        public EquipmentTrait requiredTrait;

        public override bool IsConditionMet(PlayerManager playerManager, int level)
        {
            if (playerManager == null) return false;

            var equipmentManager = UnityEngine.Object.FindObjectOfType<EquipmentDataManager>();
            if (equipmentManager == null) return false;

            // 장착된 모든 장비를 순회하며 해당 특성이 있는지 검사
            foreach (var equipment in equipmentManager.EquippedItems.Values)
            {
                if (equipment != null && equipment.traits != null && equipment.traits.Contains(requiredTrait))
                {
                    return true;
                }
            }

            return false;
        }
    }
}