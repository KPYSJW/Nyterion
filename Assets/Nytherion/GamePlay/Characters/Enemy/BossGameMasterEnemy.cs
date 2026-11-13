using Nytherion.Data.ScriptableObjects.Enemy;
using UnityEngine;

namespace Nytherion.GamePlay.Characters.Enemy
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BossGameMasterIdleBehavior))]
    public sealed class BossGameMasterEnemy : EnemyBase
    {
        [Header("Upgrade / 강화")]
        [SerializeField, Range(0f, 1f)] private float upgradeHealthFraction = 0.5f;
        [ColorUsage(true, true)]
        [SerializeField] private Color upgradedOutlineColor = new Color(1.5f, 0.45f, 3f, 1f);
        [SerializeField, Min(0f)] private float glowStrength = 1.5f;
        [SerializeField, Range(1f, 6f)] private float glowWidth = 3f;
        private static readonly int UpgradeState = Animator.StringToHash("Base Layer.Upgrade");
        private BossGameMasterIdleBehavior movement;
        private Animator animator;
        private SpriteRenderer visual;
        private BossGameMasterShieldPattern shield;
        private MaterialPropertyBlock outlineProperties;
        private float startingHealth;
        private bool initialized, upgradeAttempted, upgraded, upgrading;

        public bool IsUpgraded => upgraded;
        public bool IsUpgrading => upgrading;
        protected override bool CanReceiveDamage => !upgrading;
        protected override float ModifyIncomingDamage(float damageAmount, Vector2? hitPoint)
        {
            if (shield == null) shield = GetComponent<BossGameMasterShieldPattern>();
            return shield != null ? shield.ReceiveDamage(damageAmount, hitPoint) : damageAmount;
        }

        private void CachePhaseComponents()
        {
            if (movement == null) movement = GetComponent<BossGameMasterIdleBehavior>();
            if (animator == null) animator = GetComponent<Animator>();
            if (visual == null) visual = GetComponent<SpriteRenderer>();
            if (aiController == null) aiController = GetComponent<EnemyAIController>();
        }

        public override void Initialize(EnemyData data)
        {
            if (data == null) return;
            CachePhaseComponents();
            initialized = upgradeAttempted = upgraded = upgrading = false;
            startingHealth = Mathf.Max(1f, data.maxHealth);
            movement.PrepareNavigationAlignment();
            movement.SetPhaseAnimationLocked(false);
            base.Initialize(data);
            ApplyOutline(false);
            initialized = true;
        }

        private void Update()
        {
            if (!initialized || isDead) return;
            if (!upgradeAttempted && CurrentHealth > 0f && CurrentHealth <= startingHealth * Mathf.Clamp01(upgradeHealthFraction))
                BeginUpgrade();
            if (!upgrading || animator == null || !animator.isActiveAndEnabled) return;
            AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
            if (state.fullPathHash != UpgradeState || state.normalizedTime < 1f || animator.IsInTransition(0)) return;
            upgrading = false;
            movement.SetPhaseAnimationLocked(false);
            if (aiController != null)
            {
                aiController.ClearForcedFacingDirection();
                aiController.SetMovementAllowed(true);
                aiController.enabled = true;
                aiController.TransitionToState(aiController.chaseState);
            }
        }

        private void BeginUpgrade()
        {
            upgradeAttempted = true;
            CachePhaseComponents();
            if (animator == null || !animator.isActiveAndEnabled || animator.layerCount == 0 || !animator.HasState(0, UpgradeState))
            {
                Debug.LogError("[GameMaster] Upgrade 애니메이션 상태가 연결되지 않았습니다.", this);
                return;
            }
            upgraded = upgrading = true;
            movement.SetPhaseAnimationLocked(true);
            if (aiController != null)
            {
                aiController.SetMovementAllowed(false);
                aiController.enabled = false;
            }
            ApplyOutline(true);
            animator.Play(UpgradeState, 0, 0f);
            animator.Update(0f);
        }

        private void ApplyOutline(bool glowing)
        {
            if (visual == null) return;
            if (outlineProperties == null) outlineProperties = new MaterialPropertyBlock();
            // Preserve hit-flash and sprite properties written by the shared enemy systems.
            visual.GetPropertyBlock(outlineProperties);
            outlineProperties.SetFloat("_OutlineEnabled", 1f);
            outlineProperties.SetFloat("_OutlineWidth", 1f);
            outlineProperties.SetColor("_OutlineColor", glowing ? upgradedOutlineColor : Color.black);
            outlineProperties.SetColor("_GlowColor", upgradedOutlineColor);
            outlineProperties.SetFloat("_GlowEnabled", glowing ? 1f : 0f);
            outlineProperties.SetFloat("_GlowStrength", Mathf.Max(0f, glowStrength));
            outlineProperties.SetFloat("_GlowWidth", Mathf.Clamp(glowWidth, 1f, 6f));
            visual.SetPropertyBlock(outlineProperties);
        }

        private void OnDisable()
        {
            initialized = upgradeAttempted = upgraded = upgrading = false;
            ApplyOutline(false);
        }
    }
}
