using System;
using System.Collections.Generic;
using Nytherion.Core.Enums;
using Nytherion.Core.Utils;

namespace Nytherion.Gameplay.Relics.Modules
{
    /// <summary>
    /// 말레우스 전용 설정입니다. 플레이어가 공격하면 저주 투사체를 발사합니다.
    /// </summary>
    [Serializable, RelicDisplayName("말레우스 소환물 공격")]
    public sealed class MalleusRelicEffect : FollowerAttackRelicEffectBase
    {
        protected override string CompanionObjectName => "Malleus Companion";

        protected override void AddProjectileTraits(List<EquipmentTrait> traits)
        {
            if (!traits.Contains(EquipmentTrait.Curse))
            {
                traits.Add(EquipmentTrait.Curse);
            }
        }
    }
}
