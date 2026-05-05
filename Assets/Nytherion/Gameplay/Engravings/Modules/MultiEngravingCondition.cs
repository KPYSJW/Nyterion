using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Nytherion.Core.Managers;

namespace Nytherion.Gameplay.Engravings.Modules
{
    /// <summary>
    /// 다른 특정 각인들이 보드에 활성화(Equip)되어 있는지 검사
    /// </summary>
    [Serializable]
    public class MultiEngravingCondition : EngravingConditionBase
    {
        [Tooltip("반드시 활성화되어야 하는 각인들의 ID(engravingName) 목록입니다.")]
        public List<string> requiredEngravingIds = new List<string>();

        public override bool IsConditionMet(PlayerManager playerManager, int level)
        {
            if (requiredEngravingIds == null || requiredEngravingIds.Count == 0) return true;

            var engravingManager = UnityEngine.Object.FindObjectOfType<EngravingManager>();
            if (engravingManager == null) return false;

            // 보드에 장착된 모든 각인의 ID 목록을 가져옴
            var placedBlocks = engravingManager.GetPlacedBlocks();
            var placedIds = placedBlocks.Select(kvp => kvp.Key).ToHashSet();

            // 요구하는 모든 ID가 placedIds에 포함되어 있는지 검사
            foreach (var requiredId in requiredEngravingIds)
            {
                if (!placedIds.Contains(requiredId))
                {
                    return false; // 하나라도 없다면 조건 불충족
                }
            }

            return true; // 모두 모임 
        }
    }
}