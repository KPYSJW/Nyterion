using System.Collections.Generic;
using System.Linq;
using Nytherion.GamePlay.Combat;
using UnityEngine;

namespace Nytherion.GamePlay.Characters.Enemy
{
    public sealed class SingleAttackCombatBehavior : IEnemyCombatBehavior
    {
        private readonly IReadOnlyList<IAttackBehavior> attackBehaviors;
        private readonly IAttackSelector attackSelector;

        private bool waitingForAttackAnimation;
        private bool waitingForAttackCooldown;

        public SingleAttackCombatBehavior(
            IReadOnlyList<IAttackBehavior> attackBehaviors,
            IAttackSelector attackSelector)
        {
            this.attackBehaviors = attackBehaviors;
            this.attackSelector = attackSelector;
        }

        public bool ShouldEnterAttackState(EnemyAIController enemy)
        {
            IAttackBehavior attack = SelectAttack(enemy);
            return attack != null &&
                   attack.AttackCoolDown >= 1f &&
                   attack.IsInAttackRange(enemy.player);
        }

        public void EnterAttackState(EnemyAIController enemy)
        {
            enemy.StopMovement();
        }

        public void UpdateAttackState(EnemyAIController enemy)
        {
            enemy.StopMovement();

            IAttackBehavior attack = SelectAttack(enemy);

            if (waitingForAttackAnimation)
            {
                if (!enemy.IsAnimationFinished("Attack"))
                    return;

                waitingForAttackAnimation = false;
                waitingForAttackCooldown = true;
                enemy.PlayAnimation("Idle");
                return;
            }

            if (waitingForAttackCooldown)
            {
                if (attack != null && attack.AttackCoolDown < 1f)
                    return;

                waitingForAttackCooldown = false;
                enemy.TransitionToState(enemy.chaseState);
                return;
            }

            if (attack == null || !attack.IsInAttackRange(enemy.player))
            {
                enemy.TransitionToState(enemy.chaseState);
                return;
            }

            if (!attack.TryAttack(enemy.player))
            {
                waitingForAttackCooldown = true;
                enemy.PlayAnimation("Idle");
                return;
            }

            enemy.PlayAnimation("Attack");
            waitingForAttackAnimation = true;
        }

        public void ExitAttackState(EnemyAIController enemy)
        {
        }

        public void ResetForReuse()
        {
            waitingForAttackAnimation = false;
            waitingForAttackCooldown = false;
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

            return attackBehaviors.FirstOrDefault();
        }
    }
}
