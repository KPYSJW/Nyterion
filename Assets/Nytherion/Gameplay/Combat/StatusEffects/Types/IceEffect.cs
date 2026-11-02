using UnityEngine;
using Nytherion.GamePlay.Characters.Enemy;

namespace Nytherion.GamePlay.Combat
{
    public class IceEffect : StatusEffect
    {
        public override string EffectId => "Ice";

        private int currentStacks = 1;
        private int maxStacks = 5;
        private float slowPerStack = 0.06f;
        private int freezeRequiredStacks = int.MaxValue;
        private float freezeDuration;
        private float freezeTimer;

        private EnemyAIController aiController;
        private Animator animator;

        public float SpeedReduction => currentStacks * slowPerStack;
        public int StackCount => currentStacks;
        public bool IsFrozen => freezeTimer > 0f;

        public IceEffect(float duration)
        {
            this.Duration = duration;
        }

        public override void OnApply()
        {
            if (target != null)
            {
                aiController = target.GetComponent<EnemyAIController>();
                animator = target.GetComponent<Animator>();
                if (animator == null)
                {
                    animator = target.GetComponentInChildren<Animator>();
                }
            }

            ApplyReduction();

            if (manager != null)
            {
                manager.PlayVFX(EffectId);
            }
        }

        public override void OnStack(StatusEffect newEffect)
        {
            currentStacks = Mathf.Min(maxStacks, currentStacks + 1);
            if (currentStacks >= freezeRequiredStacks)
            {
                freezeTimer = Mathf.Max(freezeTimer, freezeDuration);
            }
            ApplyReduction();
        }

        public void ApplySetBonus(int requiredStacks, float duration)
        {
            freezeRequiredStacks = Mathf.Max(1, requiredStacks);
            freezeDuration = Mathf.Max(0f, duration);
        }

        public bool ConsumeFreeze()
        {
            if (!IsFrozen) return false;

            freezeTimer = 0f;
            ApplyReduction();
            return true;
        }

        private void ApplyReduction()
        {
            float reductionMultiplier = IsFrozen ? 0f : Mathf.Max(0f, 1f - SpeedReduction);
            if (aiController != null && aiController.agent != null)
            {
                aiController.agent.speed = aiController.moveSpeed * reductionMultiplier;
            }
            if (animator != null)
            {
                animator.speed = 1.0f * reductionMultiplier;
            }
        }

        public override void OnUpdate(float deltaTime)
        {
            if (freezeTimer > 0f)
            {
                freezeTimer = Mathf.Max(0f, freezeTimer - deltaTime);
                if (freezeTimer <= 0f)
                {
                    ApplyReduction();
                }
            }
        }

        public override void OnRemove()
        {
            if (aiController != null && aiController.agent != null)
            {
                aiController.agent.speed = aiController.moveSpeed;
            }
            if (animator != null)
            {
                animator.speed = 1.0f;
            }

            if (manager != null)
            {
                manager.StopVFX(EffectId);
            }
        }
    }
}
