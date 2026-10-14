using UnityEngine;

namespace Nytherion.GamePlay.Characters.Companions
{
    /// <summary>
    /// 플레이어를 따라다니며 가까운 적에게 전용 미사일을 발사하는 블래스트 소환수입니다.
    /// 이동과 공격의 공통 동작은 SummonedCompanion 설정을 사용합니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BlastCompanion : SummonedCompanion
    {
    }
}
