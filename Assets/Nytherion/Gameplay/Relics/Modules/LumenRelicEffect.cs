using System;
using System.Collections.Generic;
using Nytherion.Core.Enums;
using Nytherion.Core.Utils;

namespace Nytherion.Gameplay.Relics.Modules
{
    /// <summary>
    /// 루멘 전용 설정입니다. 플레이어가 공격하면 성속성 투사체를 발사합니다.
    /// </summary>
    [Serializable, RelicDisplayName("루멘 소환물 공격")]
    public sealed class LumenRelicEffect : FollowerAttackRelicEffectBase
    {
        protected override string CompanionObjectName => "Lumen Companion";

        protected override void AddProjectileTraits(List<EquipmentTrait> traits)
        {
            if (!traits.Contains(EquipmentTrait.Holy))
            {
                traits.Add(EquipmentTrait.Holy);
            }
        }
    }
}
