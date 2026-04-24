using System.Diagnostics;
using Nytherion.Data.ScriptableObjects.Enemy;
using UnityEngine;
namespace Nytherion.GamePlay.Characters.Enemy.States
{
    public class EnemyChaseState : EnemyBaseState
    {
        public EnemyChaseState(EnemyAIController enemyAIController) : base(enemyAIController) { }

        private const float HybridRangedBuffer=1.5f;

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
            enemy.MoveTowardsPlayer();

            /*float distance = enemy.GetDistanceToPlayer();

            if (distance > 3.0f)
            {
                enemy.MoveTowardsPlayer();
                return;
            }

            Vector2 slotTarget = enemy.GetMeleeSlotTarget(1.2f, 0.8f);
            enemy.MoveToTarget(slotTarget);*/
        }
        
        private void HandleRangedChase(EnemyAIController enemy)
        {
           /* float distance = enemy.GetDistanceToPlayer();

            if (distance <= enemy.TooCloseDistance)
            {
                enemy.MoveAwayFromPlayer();
                return;
            }*/ //원거리 몬스터는 근접하면 거리 벌리기 추후에 조정 ai컨트롤러에서도 CanAttackPlayer 조정

            if (enemy.CanAttackPlayer())
            {
                enemy.TransitionToState(enemy.attackState);
                return;
            }

            enemy.MoveTowardsPlayer();
            
        }

        private void HandleHybridChase(EnemyAIController enemy)
        {
            float distance = enemy.GetDistanceToPlayer();
            float rangedDistance=enemy.HybridSwitchDistance+HybridRangedBuffer;

            if(enemy.CanUseMeleeAttack())
            {
                enemy.TransitionToState(enemy.attackState);
                return;
            }

            if(enemy.CanUseRangedAttack()&&enemy.IsRangedAttackReady()&& distance>=rangedDistance)
            {
                enemy.TransitionToState(enemy.attackState);
                return;
            }
            enemy.MoveTowardsPlayer();
        }
    }
}
