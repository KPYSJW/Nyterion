using Nytherion.Data.ScriptableObjects.Enemy;
using UnityEngine;
using UnityEngine.AI;

namespace Nytherion.GamePlay.Characters.Enemy
{
    public sealed class NavMeshChaseMovement : IEnemyMovementBehavior
    {
        private readonly NavMeshAgent agent;
        private readonly Rigidbody2D rb;

        public bool RequiresNavMeshAgent => true;
        public Vector3 CurrentVelocity =>
            agent != null && agent.isActiveAndEnabled
                ? agent.desiredVelocity
                : Vector3.zero;

        public NavMeshChaseMovement(NavMeshAgent agent, Rigidbody2D rb)
        {
            this.agent = agent;
            this.rb = rb;
        }

        public void Configure(EnemyData data)
        {
            if (agent != null && data != null)
            {
                agent.speed = data.moveSpeed;
            }
        }

        public void MoveTowards(Vector3 destination)
        {
            if (agent == null || !agent.isActiveAndEnabled || !agent.isOnNavMesh)
                return;

            agent.isStopped = false;
            agent.SetDestination(destination);
        }

        public void StopMovement()
        {
            if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
            {
                agent.isStopped = true;
                agent.ResetPath();
            }

            if (rb != null)
            {
                rb.velocity = Vector2.zero;
            }
        }

        public void ResetForReuse()
        {
            StopMovement();
        }
    }
}
