using System;
using System.Collections.Generic;
using Nytherion.Core.Enums;
using Nytherion.Core.Utils;

namespace Nytherion.Gameplay.Relics.Modules
{
    /// <summary>
    /// 데몬 전용 설정입니다. 플레이어가 공격하면 마성 투사체를 발사합니다.
    /// </summary>
    [Serializable, RelicDisplayName("데몬 소환물 공격")]
    public sealed class DaemonRelicEffect : FollowerAttackRelicEffectBase
    {
        protected override string CompanionObjectName => "Daemon Companion";

        protected override void AddProjectileTraits(List<EquipmentTrait> traits)
        {
            if (!traits.Contains(EquipmentTrait.Demonic))
            {
                traits.Add(EquipmentTrait.Demonic);
            }
        }
    }
}
