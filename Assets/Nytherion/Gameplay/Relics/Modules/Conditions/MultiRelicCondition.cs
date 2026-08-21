using System;
using System.Collections.Generic;
using UnityEngine;
using Nytherion.Core.Managers;
using Nytherion.Core.Utils;

namespace Nytherion.Gameplay.Relics.Modules
{
    /// <summary>
    /// 다른 특정 각인들이 보드에 활성화되어 있는지 검사
    /// </summary>
    [Serializable, RelicDisplayName("복합 유물 조건")]
    public class MultiRelicCondition : RelicConditionBase
    {
        [Tooltip("반드시 활성화되어야 하는 각인들의 ID 목록입니다.")]
        public List<string> requiredRelicIds = new List<string>();

        public override bool IsConditionMet(PlayerManager playerManager, int level)
        {
            if (requiredRelicIds == null || requiredRelicIds.Count == 0) return true;

            var relicManager = UnityEngine.Object.FindObjectOfType<RelicManager>();
            if (relicManager == null) return false;

            // 보드에 장착된 모든 각인의 ID 목록을 가져옴
            var placedIds = new HashSet<string>();
            foreach (var pair in relicManager.GetPlacedBlocks())
            {
                var block = relicManager.GetBlockAt(pair.Value.y, pair.Value.x);
                if (block != null && block.SourceData != null)
                {
                    placedIds.Add(block.RelicId);
                }
            }

            // 요구하는 모든 ID가 placedIds에 포함되어 있는지 검사
            foreach (var requiredId in requiredRelicIds)
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
