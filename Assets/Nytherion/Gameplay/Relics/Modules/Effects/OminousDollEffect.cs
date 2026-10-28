using System;
using Nytherion.Core.Managers;
using Nytherion.Core.Utils;
using UnityEngine;

namespace Nytherion.Gameplay.Relics.Modules
{
    [Serializable, RelicDisplayName("불길한 인형 효과")]
    public sealed class OminousDollEffect : RelicEffectBase
    {
        public float attackBonus = 0.4f;
        public float attackBonusPerLevel = 0.05f;
        public float defensePenalty = 5f;
        public float explosionDamage = 60f;
        public float explosionDamagePerLevel = 15f;
        public float explosionRadius = 2f;
        public GameObject explosionPrefab;
        private OminousDollRuntime runtime;

        public override void ApplyEffect(PlayerManager playerManager, int level)
        {
            if (playerManager == null) return;
            runtime = playerManager.GetComponent<OminousDollRuntime>();
            if (runtime == null) runtime = playerManager.gameObject.AddComponent<OminousDollRuntime>();
            runtime.SetBonus(this, playerManager, attackBonus + attackBonusPerLevel * Mathf.Max(0, level - 1),
                defensePenalty, explosionDamage + explosionDamagePerLevel * Mathf.Max(0, level - 1),
                explosionRadius, explosionPrefab);
        }

        public override void RemoveEffect(PlayerManager playerManager, int level)
        {
            if (runtime != null) runtime.RemoveBonus(this);
            runtime = null;
        }
    }
}
