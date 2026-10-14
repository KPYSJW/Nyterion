using System.Collections.Generic;
using Nytherion.Gameplay.Relics.Modules;
using UnityEngine;

namespace Nytherion.GamePlay.Skills
{
    /// <summary>플레이어별 루티 강화입니다. 공유 스킬 에셋을 수정하지 않고 소환된 루티에도 반영합니다.</summary>
    [DisallowMultipleComponent]
    public sealed class RootiUpgradeRuntime : MonoBehaviour
    {
        private readonly HashSet<RootiUpgradeRelicEffect> upgrades = new HashSet<RootiUpgradeRelicEffect>();

        public float AttackSpeedMultiplier { get; private set; } = 1f;
        public int AdditionalCharges { get; private set; }
        public int AdditionalProjectiles { get; private set; }
        public int AdditionalSummons { get; private set; }
        public int MaxBounces { get; private set; }
        public float BounceRadius { get; private set; }

        public void SetUpgrade(RootiUpgradeRelicEffect upgrade)
        {
            if (upgrade == null) return;
            upgrades.Add(upgrade);
            Recalculate();
        }

        public void RemoveUpgrade(RootiUpgradeRelicEffect upgrade)
        {
            upgrades.Remove(upgrade);
            Recalculate();
        }

        private void Recalculate()
        {
            AttackSpeedMultiplier = 1f;
            AdditionalCharges = AdditionalProjectiles = AdditionalSummons = MaxBounces = 0;
            BounceRadius = 0f;
            foreach (RootiUpgradeRelicEffect upgrade in upgrades)
            {
                switch (upgrade.upgradeType)
                {
                    case RootiUpgradeType.AttackSpeed:
                        AttackSpeedMultiplier += Mathf.Max(0f, upgrade.attackSpeedBonus);
                        break;
                    case RootiUpgradeType.ChargeCapacity:
                        AdditionalCharges += Mathf.Max(0, upgrade.additionalCount);
                        break;
                    case RootiUpgradeType.ProjectileCount:
                        AdditionalProjectiles += Mathf.Max(0, upgrade.additionalCount);
                        break;
                    case RootiUpgradeType.SummonCount:
                        AdditionalSummons += Mathf.Max(0, upgrade.additionalCount);
                        break;
                    case RootiUpgradeType.Bounce:
                        MaxBounces = Mathf.Max(MaxBounces, upgrade.maxBounces);
                        BounceRadius = Mathf.Max(BounceRadius, upgrade.bounceRadius);
                        break;
                }
            }
        }
    }
}
