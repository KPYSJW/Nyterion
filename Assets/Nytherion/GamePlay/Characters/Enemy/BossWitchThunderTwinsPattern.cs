using UnityEngine;

namespace Nytherion.GamePlay.Characters.Enemy
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BossWitchOrbitController))]
    public sealed class BossWitchThunderTwinsPattern : MonoBehaviour, IBossWitchPattern
    {
        [SerializeField] private bool reverseCastingFacing;
        private BossWitchOrbitController orbit;
        private BossWitchMovementBehavior movement;
        private bool casting;
        private static readonly int CastState = Animator.StringToHash("Base Layer.ThunderTwinsCast");
        public BossWitchPatternType PatternType => BossWitchPatternType.LightningProjectiles;

        private void Cache()
        {
            if (orbit == null) orbit = GetComponent<BossWitchOrbitController>();
            if (movement == null) movement = GetComponent<BossWitchMovementBehavior>();
        }

        public bool CanStart(EnemyAIController enemy)
        {
            Cache();
            return isActiveAndEnabled && !casting && orbit != null && orbit.CanRecharge() &&
                enemy != null && enemy.CurrentTarget != null && enemy.animator != null &&
                enemy.animator.isActiveAndEnabled && enemy.animator.layerCount > 0 && enemy.animator.HasState(0, CastState);
        }

        public void Begin(EnemyAIController enemy)
        {
            Cache();
            casting = true;
            movement.SetCastingFacingReversed(reverseCastingFacing);
            float horizontal = enemy.CurrentTarget.position.x - transform.position.x;
            if (Mathf.Abs(horizontal) > 0.01f)
            {
                Vector2 facing = horizontal > 0f ? Vector2.right : Vector2.left;
                enemy.SetForcedFacingDirection(facing);
                movement.UpdateFacing(facing);
            }
            enemy.PlayAnimation("ThunderTwinsCast");
        }

        public bool UpdatePattern(EnemyAIController enemy)
        {
            if (!casting) return true;
            if (enemy.animator == null || !enemy.animator.isActiveAndEnabled || enemy.animator.layerCount == 0) return false;
            AnimatorStateInfo state = enemy.animator.GetCurrentAnimatorStateInfo(0);
            if (state.fullPathHash != CastState || state.normalizedTime < 1f || enemy.animator.IsInTransition(0)) return false;
            orbit.Recharge();
            casting = false;
            return true;
        }

        public void End(EnemyAIController enemy, bool completed)
        {
            casting = false;
            movement.SetCastingFacingReversed(false);
            enemy.ClearForcedFacingDirection();
        }

        public void ResetForReuse()
        {
            Cache();
            casting = false;
            if (orbit != null) orbit.ClearAll();
        }

        private void OnDisable() => casting = false;
    }
}
