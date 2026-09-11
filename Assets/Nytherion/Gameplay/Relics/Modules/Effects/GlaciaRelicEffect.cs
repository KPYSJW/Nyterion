using System;
using System.Collections.Generic;
using Nytherion.Core.Enums;
using Nytherion.Core.Utils;

namespace Nytherion.Gameplay.Relics.Modules
{
    /// <summary>
    /// 글레이시아 전용 설정입니다. 플레이어가 공격하면 얼음 투사체를 발사합니다.
    /// </summary>
    [Serializable, RelicDisplayName("글레이시아 소환물 공격")]
    public sealed class GlaciaRelicEffect : FollowerAttackRelicEffectBase
    {
        protected override string CompanionObjectName => "Glacia Companion";

        protected override void AddProjectileTraits(List<EquipmentTrait> traits)
        {
            if (!traits.Contains(EquipmentTrait.Ice))
            {
                traits.Add(EquipmentTrait.Ice);
            }
        }
    }
}
