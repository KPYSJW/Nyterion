using UnityEngine;

namespace Nytherion.GamePlay.Characters.Enemy
{
    /// <summary>일반 적의 피격 처리를 사용하는 무한 체력 테스트 대상입니다.</summary>
    [DisallowMultipleComponent]
    public sealed class TrainingDummy : EnemyBase
    {
        protected override bool HasUnlimitedHealth => true;
    }
}
