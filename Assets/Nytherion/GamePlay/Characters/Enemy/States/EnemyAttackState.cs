
using System;
using Nytherion.Data.ScriptableObjects.Enemy;
namespace Nytherion.GamePlay.Characters.Enemy.States
{
    public class EnemyAttackState : EnemyBaseState
    {
        private readonly float attackCommitTime = 2f;
        private readonly float postAttackDelay = 0.5f;

        private float attackCommitUntil = 0f;
        private float nextActionTime = 0f;

        public EnemyAttackState(EnemyAIController enemyAIController) : base(enemyAIController) { }

        public override void EnterState(EnemyAIController enemy)
        {
            enemy.StopMovement();
        }

        public override void UpdateState(EnemyAIController enemy)
        {
            switch (enemy.CurrentCombatType)
            {
                case EnemyCombatType.Melee:
                    HandleMeleeAttack(enemy);
                    break;

                case EnemyCombatType.Ranged:
                    HandleRangedAttack(enemy);
                    break;

                case EnemyCombatType.Hybrid:
                    HandleHybridAttack(enemy);
                    break;
            }
        }

    private void HandleHybridAttack(EnemyAIController enemy)
    {
        throw new NotImplementedException();
    }

    private void HandleRangedAttack(EnemyAIController enemy)
    {
        enemy.StopMovement();

            if (UnityEngine.Time.time < attackCommitUntil)
            {
                return;
            }

            if (!enemy.CanAttackPlayer())
            {
                //enemyAIController.Obstacle.enabled=false;
                //enemyAIController.agent.enabled=true;
                enemy.TransitionToState(enemy.chaseState);
                return;
            }

            if (UnityEngine.Time.time < nextActionTime)
            {
                return;
            }

            bool attacked = enemy.TryAttackPlayer();

            if (attacked)
            {
                //enemyAIController.agent.enabled=false;
                //enemyAIController.Obstacle.enabled=true;
                enemy.PlayAnimation("Attack");

                attackCommitUntil = UnityEngine.Time.time + attackCommitTime;
                nextActionTime = UnityEngine.Time.time + postAttackDelay;
            }
    }

    private void HandleMeleeAttack(EnemyAIController enemy)
    {
        enemy.StopMovement();

            if (UnityEngine.Time.time < attackCommitUntil)
            {
                return;
            }

            if (!enemy.CanAttackPlayer())
            {
                //enemyAIController.Obstacle.enabled=false;
                //enemyAIController.agent.enabled=true;
                enemy.TransitionToState(enemy.chaseState);
                return;
            }

            if (UnityEngine.Time.time < nextActionTime)
            {
                return;
            }

            bool attacked = enemy.TryAttackPlayer();

            if (attacked)
            {
                //enemyAIController.agent.enabled=false;
                //enemyAIController.Obstacle.enabled=true;
                enemy.PlayAnimation("Attack");

                attackCommitUntil = UnityEngine.Time.time + attackCommitTime;
                nextActionTime = UnityEngine.Time.time + postAttackDelay;
            }
    }

    

        public override void ExitState(EnemyAIController enemy)
        {
        }
    }
}
