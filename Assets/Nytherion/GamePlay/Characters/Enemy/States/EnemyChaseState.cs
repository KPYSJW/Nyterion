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
            }
            else
            {
                enemy.MoveTowardsPlayer();
            }
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
