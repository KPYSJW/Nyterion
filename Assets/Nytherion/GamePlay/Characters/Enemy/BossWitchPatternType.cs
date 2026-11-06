using UnityEngine;

namespace Nytherion.GamePlay.Characters.Enemy
{
    public enum BossWitchPatternType
    {
        [InspectorName("1. 화염구 난사")]
        FireballVolley = 0,
        [InspectorName("2. 화염 소용돌이")]
        FireTornado = 1,
        [InspectorName("3. 얼음 기둥")]
        IcePillar = 2,
        [InspectorName("4. 번개 투사체")]
        LightningProjectiles = 3
    }
}
