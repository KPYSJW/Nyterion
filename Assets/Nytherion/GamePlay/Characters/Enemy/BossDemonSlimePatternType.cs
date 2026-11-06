using UnityEngine;

namespace Nytherion.GamePlay.Characters.Enemy
{
    public enum BossDemonSlimePatternType
    {
        [InspectorName("1. 돌진 베기")] DashCleave,
        [InspectorName("2. 메테오 낙하")] MeteorShower,
        [InspectorName("3. 화염구 탄막")] FireballBarrage,
        [InspectorName("4. 화염 구체 소환")] FireOrbSummon
    }
}
