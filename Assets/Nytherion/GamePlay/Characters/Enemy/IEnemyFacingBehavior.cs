using UnityEngine;

namespace Nytherion.GamePlay.Characters.Enemy
{
    public interface IEnemyFacingBehavior
    {
        void UpdateFacing(Vector2 direction);
    }
}
