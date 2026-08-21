using System;
using Nytherion.Core.Utils;

namespace Nytherion.Gameplay.Relics.Modules
{
    /// <summary>
    /// CatEyes 전용 설정입니다. 공통 동반 공격 동작은 FollowerAttackRelicEffectBase에서 처리합니다.
    /// </summary>
    [Serializable, RelicDisplayName("고양이 눈 소환물 공격")]
    public sealed class CatEyesRelicEffect : FollowerAttackRelicEffectBase
    {
        protected override string CompanionObjectName => "CatEyes Companion";
    }
}
