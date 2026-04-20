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
            enemy.StopMovement();

            if (UnityEngine.Time.time < attackCommitUntil)
            {
                return;
            }

            if (!enemy.CanAttackPlayer())
            {
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
