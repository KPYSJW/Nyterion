using System;
using Nytherion.Core.Managers;
using Nytherion.Core.Utils;
using Nytherion.GamePlay.Characters.Companions;
using UnityEngine;

namespace Nytherion.Gameplay.Relics.Modules
{
    /// <summary>
    /// 유물 장착 중 전투 소환수를 유지하고, 장착이 해제되면 함께 제거합니다.
    /// </summary>
    [Serializable, RelicDisplayName("소환수 소환 효과")]
    public sealed class SummonedCompanionRelicEffect : RelicEffectBase
    {
        [Tooltip("유물 장착 시 생성할 소환수 프리팹")]
        public GameObject companionPrefab;

        [Tooltip("플레이어를 기준으로 한 최초 소환 위치 보정")]
        public Vector3 spawnOffset = new Vector3(-1.2f, 0.6f, 0f);

        private SummonedCompanion activeCompanion;

        public override void ApplyEffect(PlayerManager playerManager, int level)
        {
            RemoveEffect(playerManager, level);

            if (playerManager == null || companionPrefab == null)
            {
                Debug.LogWarning("[SummonedCompanionRelicEffect] 소환수 프리팹 또는 플레이어가 없습니다.");
                return;
            }

            GameObject companionObject = UnityEngine.Object.Instantiate(
                companionPrefab,
                playerManager.transform.position + spawnOffset,
                Quaternion.identity);
            activeCompanion = companionObject.GetComponent<SummonedCompanion>();
            if (activeCompanion == null)
            {
                Debug.LogError("[SummonedCompanionRelicEffect] 소환수 프리팹에 SummonedCompanion 컴포넌트가 없습니다.");
                UnityEngine.Object.Destroy(companionObject);
                return;
            }

            activeCompanion.Initialize(playerManager, level);
        }

        public override void RemoveEffect(PlayerManager playerManager, int level)
        {
            if (activeCompanion != null)
            {
                UnityEngine.Object.Destroy(activeCompanion.gameObject);
            }
            activeCompanion = null;
        }
    }
}
