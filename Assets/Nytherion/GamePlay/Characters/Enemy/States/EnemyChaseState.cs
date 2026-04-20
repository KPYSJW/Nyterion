using System.Diagnostics;
using Nytherion.Data.ScriptableObjects.Enemy;
using UnityEngine;
namespace Nytherion.GamePlay.Characters.Enemy.States
{
    public class EnemyChaseState : EnemyBaseState
    {
        public EnemyChaseState(EnemyAIController enemyAIController) : base(enemyAIController) { }

        public override void EnterState(EnemyAIController enemy)
        {
            
            enemy.PlayAnimation("Run");
        }

        public override void UpdateState(EnemyAIController enemy)
        {
            switch (enemy.CurrentCombatType)
            {
                case EnemyCombatType.Melee:
                    HandleMeleeChase(enemy);
                    break;

                case EnemyCombatType.Ranged:
                    HandleRangedChase(enemy);
                    break;

                case EnemyCombatType.Hybrid:
                    HandleHybridChase(enemy);
                    break;
            }
        }

        public override void ExitState(EnemyAIController enemy)
        {
            enemy.StopMovement();
        }

        private void HandleMeleeChase(EnemyAIController enemy)
        {
            if (enemy.CanAttackPlayer())
            {
                enemy.TransitionToState(enemy.attackState);
                return;
            }

            float distance = enemy.GetDistanceToPlayer();

            if (distance > 3.0f)
            {
                enemy.MoveTowardsPlayer();
            }
             if (enemy.IsFrontBlocked(0.7f, 0.9f))
            {
                Vector2 separation = enemy.GetSeparationDirection(0.5f);
                Vector2 flow = enemy.GetBlockedFlowDirection(0.8f);
                Vector2 finalDirection = (flow + separation * 0.8f).normalized;
                enemy.MoveInDirection(finalDirection);
                return;
            }

            Vector2 slotTarget = enemy.GetMeleeSlotTarget(1.2f, 0.8f);
            Vector2 toSlot = (slotTarget - (Vector2)enemy.transform.position).normalized;
            Vector2 separationDirection = enemy.GetSeparationDirection(0.5f);
            Vector2 moveDirection = (toSlot + separationDirection * 0.4f).normalized;

            enemy.MoveInDirection(moveDirection);
        }
        
        private void HandleRangedChase(EnemyAIController enemy)
        {
            float distance = enemy.GetDistanceToPlayer();

            if (distance <= enemy.TooCloseDistance)
            {
                enemy.MoveAwayFromPlayer();
            }
            else if (enemy.CanAttackPlayer())
            {
                enemy.TransitionToState(enemy.attackState);
            }
            else
            {
                enemy.MoveTowardsPlayer();
            }
        }

        private void HandleHybridChase(EnemyAIController enemy)
        {
            float distance = enemy.GetDistanceToPlayer();

            if (distance <= enemy.HybridSwitchDistance)
            {
                HandleMeleeChase(enemy);
            }
            else
            {
                HandleRangedChase(enemy);
            }
        }
    }
}
