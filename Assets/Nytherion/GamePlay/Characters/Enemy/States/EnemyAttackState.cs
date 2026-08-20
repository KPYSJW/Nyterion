
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

        private const float HybridRangedBuffer = 1.5f;

       
        private bool returnToChaseAfterCommit = false;

        public EnemyAttackState(EnemyAIController enemyAIController) : base(enemyAIController) { }

        public void ResetForReuse()
        {
            attackCommitUntil = 0f;
            nextActionTime = 0f;
            returnToChaseAfterCommit = false;
        }

        public override void EnterState(EnemyAIController enemy)
        {
            enemy.StopMovement();
            returnToChaseAfterCommit = false;
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
            enemy.StopMovement();

            if (returnToChaseAfterCommit)
            {
                if (UnityEngine.Time.time < attackCommitUntil)
                {
                    return;
                }

                returnToChaseAfterCommit = false;
                enemy.TransitionToState(enemy.chaseState);
                return;
            }

            if (UnityEngine.Time.time < attackCommitUntil)
            {
                return;
            }

            float distance = enemy.GetDistanceToPlayer();
            float rangedHarassDistance = enemy.HybridSwitchDistance + HybridRangedBuffer;

            if (enemy.CanUseMeleeAttack())
            {
                if (UnityEngine.Time.time < nextActionTime)
                {
                    return;
                }

                if (!enemy.IsMeleeAttackReady())
                {
                    return;
                }

                if (enemy.TryMeleeAttack())
                {
                    enemy.PlayAnimation("Attack");
                    attackCommitUntil = UnityEngine.Time.time + attackCommitTime;
                    nextActionTime = UnityEngine.Time.time + postAttackDelay;
                }

                return;
            }

            if (enemy.CanUseRangedAttack() && distance >= rangedHarassDistance)
            {
                if (UnityEngine.Time.time < nextActionTime)
                {
                    return;
                }

                if (!enemy.IsRangedAttackReady())
                {
                    enemy.TransitionToState(enemy.chaseState);
                    return;
                }

                if (enemy.TryRangedAttack())
                {
                    enemy.PlayAnimation("Attack1");
                    attackCommitUntil = UnityEngine.Time.time + attackCommitTime;
                    nextActionTime = UnityEngine.Time.time + postAttackDelay;

                    // 공격 커밋이 끝날 때까지는 멈춰 있고,
                    // 끝나면 그때 다시 추격으로 복귀
                    returnToChaseAfterCommit = true;
                }

                return;
            }

            enemy.TransitionToState(enemy.chaseState);
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
