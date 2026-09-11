using Nytherion.Data.ScriptableObjects.Enemy;
using UnityEngine;

namespace Nytherion.GamePlay.Characters.Enemy
{
    public interface IEnemyMovementBehavior
    {
        bool RequiresNavMeshAgent { get; }
        Vector3 CurrentVelocity { get; }

        void Configure(EnemyData data);
        void MoveTowards(Vector3 destination);
        void StopMovement();
        void ResetForReuse();
    }
}
