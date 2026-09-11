using System.Collections.Generic;
using System.Linq;
using Nytherion.GamePlay.Combat;
using Nytherion.GamePlay.Combat.Behaviors;
using UnityEngine;

namespace Nytherion.GamePlay.Characters.Enemy
{
    public sealed class HybridCombatBehavior : IEnemyCombatBehavior
    {
        private const float RangedBuffer = 1.5f;

        private readonly IReadOnlyList<IAttackBehavior> attackBehaviors;
        private readonly IAttackSelector attackSelector;
        private readonly float switchDistance;
        private readonly float attackCommitTime;
        private readonly float postAttackDelay;

        private float attackCommitUntil;
        private float nextActionTime;
        private bool returnToChaseAfterCommit;

        public HybridCombatBehavior(
            IReadOnlyList<IAttackBehavior> attackBehaviors,
            IAttackSelector attackSelector,
            float switchDistance,
            float attackCommitTime = 2f,
            float postAttackDelay = 0.5f)
        {
            this.attackBehaviors = attackBehaviors;
            this.attackSelector = attackSelector;
            this.switchDistance = switchDistance;
            this.attackCommitTime = attackCommitTime;
            this.postAttackDelay = postAttackDelay;
        }

        public bool ShouldEnterAttackState(EnemyAIController enemy)
        {
            IAttackBehavior attack = SelectAttack(enemy);
            if (attack == null || !attack.IsInAttackRange(enemy.player))
                return false;

            if (attack is MeleeAttackBehavior)
                return true;

            return attack is RangedAttackBehavior &&
                   attack.AttackCoolDown >= 1f &&
                   enemy.GetDistanceToPlayer() >= switchDistance + RangedBuffer;
        }

        public void EnterAttackState(EnemyAIController enemy)
        {
            enemy.StopMovement();
            returnToChaseAfterCommit = false;
        }

        public void UpdateAttackState(EnemyAIController enemy)
        {
            enemy.StopMovement();

            if (returnToChaseAfterCommit)
            {
                if (Time.time < attackCommitUntil)
                    return;

                returnToChaseAfterCommit = false;
                enemy.TransitionToState(enemy.chaseState);
                return;
            }

            if (Time.time < attackCommitUntil)
                return;

            IAttackBehavior attack = SelectAttack(enemy);
            if (attack == null || !attack.IsInAttackRange(enemy.player))
            {
                enemy.TransitionToState(enemy.chaseState);
                return;
            }

            if (attack is RangedAttackBehavior &&
                enemy.GetDistanceToPlayer() < switchDistance + RangedBuffer)
            {
                enemy.TransitionToState(enemy.chaseState);
                return;
            }

            if (Time.time < nextActionTime)
                return;

            if (attack.AttackCoolDown < 1f)
            {
                if (attack is RangedAttackBehavior)
                {
                    enemy.TransitionToState(enemy.chaseState);
                }

                return;
            }

            if (!attack.TryAttack(enemy.player))
                return;

            bool isRangedAttack = attack is RangedAttackBehavior;
            enemy.PlayAnimation(isRangedAttack ? "Attack1" : "Attack");

            attackCommitUntil = Time.time + attackCommitTime;
            nextActionTime = Time.time + postAttackDelay;
            returnToChaseAfterCommit = isRangedAttack;
        }

        public void ExitAttackState(EnemyAIController enemy)
        {
        }

        public void ResetForReuse()
        {
            attackCommitUntil = 0f;
            nextActionTime = 0f;
            returnToChaseAfterCommit = false;
        }

        private IAttackBehavior SelectAttack(EnemyAIController enemy)
        {
            if (enemy == null || enemy.player == null || attackBehaviors == null)
                return null;

            if (attackSelector != null)
            {
                return attackSelector.SelectAttackBehavior(
                    enemy.transform,
                    enemy.player,
                    attackBehaviors);
            }

            return attackBehaviors
                .Where(attack => attack.IsInAttackRange(enemy.player))
                .OrderByDescending(attack => attack.AttackCoolDown)
                .FirstOrDefault();
        }
    }
}
