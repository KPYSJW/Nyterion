using UnityEngine;

namespace Nytherion.Core.Interfaces
{
    /// <summary>지면 공격에 사용하는 대상의 몸체 피격 영역입니다.</summary>
    public interface IGroundDamageable : IDamageable
    {
        bool TryGetGroundHitCircle(out Vector2 center, out float radius);
        bool TryGetGroundHitBounds(out Bounds bounds);
    }
}
