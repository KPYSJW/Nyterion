using UnityEngine;
using Nytherion.GamePlay.Characters.Enemy;

namespace Nytherion.GamePlay.Combat
{
    public class IceEffect : StatusEffect
    {
        public override string EffectId => "Ice";
        public override Color EffectColor => new Color(0.75f, 0.95f, 1.0f); // 전격과의 톤 분리를 위한 차가운 백하늘색 서리 빛

        private int currentStacks = 1;
        private int maxStacks = 5;
        private float slowPerStack = 0.06f;

        private EnemyAIController aiController;
        private Animator animator;

        private float vfxDuration = 0.67f; // 애니메이션 1회 재생에 걸리는 시간 (0.333초 / 0.5배속 = 약 0.67초)
        private float vfxCooldown = 1.2f; // 재생 완료 후 다음 재생까지의 텀(대기 시간)
        private float vfxTimer;
        private bool isVfxActive = false;

        public float SpeedReduction => currentStacks * slowPerStack;

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

            isVfxActive = true;
            vfxTimer = vfxDuration;
            if (manager != null)
            {
                manager.PlayVFX(EffectId);
            }
        }

        public override void OnStack(StatusEffect newEffect)
        {
            currentStacks = Mathf.Min(maxStacks, currentStacks + 1);
            ApplyReduction();
        }

        private void ApplyReduction()
        {
            float reductionMultiplier = 1f - SpeedReduction;
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
            vfxTimer -= deltaTime;
            if (vfxTimer <= 0f)
            {
                if (isVfxActive)
                {
                    isVfxActive = false;
                    vfxTimer = vfxCooldown;
                    if (manager != null)
                    {
                        manager.StopVFX(EffectId);
                    }
                }
                else
                {
                    isVfxActive = true;
                    vfxTimer = vfxDuration;
                    if (manager != null)
                    {
                        manager.PlayVFX(EffectId);
                    }
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
