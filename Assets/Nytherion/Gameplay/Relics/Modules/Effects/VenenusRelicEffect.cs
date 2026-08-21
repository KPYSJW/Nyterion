using System;
using System.Collections.Generic;
using Nytherion.Core.Enums;
using Nytherion.Core.Utils;

namespace Nytherion.Gameplay.Relics.Modules
{
    /// <summary>
    /// 베네누스 전용 설정입니다. 플레이어가 공격하면 독 투사체를 발사합니다.
    /// </summary>
    [Serializable, RelicDisplayName("베네누스 소환물 공격")]
    public sealed class VenenusRelicEffect : FollowerAttackRelicEffectBase
    {
        protected override string CompanionObjectName => "Venenus Companion";

        protected override void AddProjectileTraits(List<EquipmentTrait> traits)
        {
            if (!traits.Contains(EquipmentTrait.Poison))
            {
                traits.Add(EquipmentTrait.Poison);
            }
        }
    }
}
