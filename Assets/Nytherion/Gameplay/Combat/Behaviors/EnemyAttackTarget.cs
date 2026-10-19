using Nytherion.GamePlay.Characters.Player;
using Nytherion.GamePlay.Skills;
using UnityEngine;

namespace Nytherion.GamePlay.Combat
{
    /// <summary>적 공격이 피격할 수 있는 플레이어와 유인 더미만 처리합니다.</summary>
    public static class EnemyAttackTarget
    {
        public static bool TryDamage(Collider2D collider, float damage, Vector2 hitPoint)
        {
            DecoyDummy dummy = collider.GetComponentInParent<DecoyDummy>();
            if (dummy != null)
            {
                if (!dummy.IsAlive) return false;
                dummy.TakeDamage(damage);
                return true;
            }
            PlayerHealth player = collider.GetComponentInParent<PlayerHealth>();
            if (player == null) return false;
            player.TakeDamage(damage, hitPoint);
            return true;
        }
    }
}
