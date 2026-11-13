using System;
using Nytherion.Core.Managers;
using Nytherion.Core.Utils;
using Nytherion.Data.ScriptableObjects.Skill;
using Nytherion.GamePlay.Skills;
using UnityEngine;

namespace Nytherion.Gameplay.Relics.Modules
{
    /// <summary>유물 활성화 중 먹구름을 유지하고 해제 시 진행 중인 낙뢰까지 정리합니다.</summary>
    [Serializable, RelicDisplayName("먹구름 소환물")]
    public sealed class DarkCloudRelicEffect : RelicEffectBase
    {
        public DarkCloudSkillData summonData;
        [NonSerialized] private GameObject summon;

        public override void ApplyEffect(PlayerManager playerManager, int level)
        {
            RemoveEffect(playerManager, level);
            if (playerManager == null || summonData == null || !summonData.HasValidFrames ||
                summonData.summonPrefab == null) return;

            summon = UnityEngine.Object.Instantiate(summonData.summonPrefab, playerManager.transform);
            if (!summon.TryGetComponent(out DarkCloudSkill cloud))
            {
                Debug.LogWarning("[DarkCloudRelicEffect] 먹구름 프리팹에 소환물 컴포넌트가 없습니다.");
                RemoveEffect(playerManager, level);
                return;
            }
            cloud.Initialize(playerManager, summonData, level);
        }

        public override void RemoveEffect(PlayerManager playerManager, int level)
        {
            if (summon == null) return;
            summon.SetActive(false);
            UnityEngine.Object.Destroy(summon);
            summon = null;
        }
    }
}
