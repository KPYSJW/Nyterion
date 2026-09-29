using System;
using Nytherion.Core.Managers;
using Nytherion.Core.Utils;
using Nytherion.GamePlay.Skills;
using UnityEngine;

namespace Nytherion.Gameplay.Relics.Modules
{
    public enum RootiUpgradeType { AttackSpeed, ChargeCapacity, ProjectileCount, SummonCount, Bounce }

    /// <summary>기존 유물 모듈의 장착·해제·침묵 수명 주기로 루티 전용 강화 값을 관리합니다.</summary>
    [Serializable, RelicDisplayName("루티 강화 효과")]
    public sealed class RootiUpgradeRelicEffect : RelicEffectBase
    {
        public RootiUpgradeType upgradeType;
        [Min(0f), Tooltip("공격속도 증가율입니다. 0.25이면 공격속도가 25% 증가합니다.")]
        public float attackSpeedBonus = 0.25f;
        [Min(0), Tooltip("추가 충전 수, 투사체 수 또는 소환 수입니다.")]
        public int additionalCount = 1;
        [Min(1)] public int maxBounces = 3;
        [Min(0.1f)] public float bounceRadius = 5f;

        public override void ApplyEffect(PlayerManager playerManager, int level)
        {
            if (playerManager == null) return;
            if (!playerManager.TryGetComponent(out RootiUpgradeRuntime runtime))
                runtime = playerManager.gameObject.AddComponent<RootiUpgradeRuntime>();
            runtime.SetUpgrade(this);
        }

        public override void RemoveEffect(PlayerManager playerManager, int level)
        {
            if (playerManager != null && playerManager.TryGetComponent(out RootiUpgradeRuntime runtime))
                runtime.RemoveUpgrade(this);
        }
    }
}
