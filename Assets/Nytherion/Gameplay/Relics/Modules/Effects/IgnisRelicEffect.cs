using System;
using System.Collections.Generic;
using Nytherion.Core.Enums;
using Nytherion.Core.Utils;

namespace Nytherion.Gameplay.Relics.Modules
{
    /// <summary>
    /// 이그니스 전용 설정입니다. 플레이어가 공격하면 화염 투사체를 발사합니다.
    /// </summary>
    [Serializable, RelicDisplayName("이그니스 소환물 공격")]
    public sealed class IgnisRelicEffect : FollowerAttackRelicEffectBase
    {
        protected override string CompanionObjectName => "Ignis Companion";

        protected override void AddProjectileTraits(List<EquipmentTrait> traits)
        {
            if (!traits.Contains(EquipmentTrait.Fire))
            {
                traits.Add(EquipmentTrait.Fire);
            }
        }
    }
}
