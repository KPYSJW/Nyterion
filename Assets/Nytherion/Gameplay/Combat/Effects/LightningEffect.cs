using UnityEngine;

namespace Nytherion.GamePlay.Combat
{
    public class LightningEffect : StatusEffect
    {
        public override string EffectId => "Lightning";
        public override Color EffectColor => new Color(0.0f, 0.5f, 1.0f); // 피격 이미지 톤에 맞춘 일렉트릭 블루 색상

        private float chainDamagePercent = 0.3f; // 본래 데미지의 30% 전이
        private float detectRadius = 4.0f; // 전이 반경

        private float vfxDuration = 0.44f; // 애니메이션 1회 재생 시간 (0.333초 / 0.75배속 = 약 0.44초)
        private float vfxCooldown = 0.56f; // 재생 완료 후 다음 재생까지의 텀(대기 시간)
        private float vfxTimer;
        private bool isVfxActive = false;

        public LightningEffect(float duration)
        {
            this.Duration = duration;
        }

        public override void OnApply()
        {
            isVfxActive = true;
            vfxTimer = vfxDuration;
            if (manager != null)
            {
                manager.PlayVFX(EffectId);
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
            if (manager != null)
            {
                manager.StopVFX(EffectId);
            }
        }

        public void TriggerChainLightning(float baseDamage)
        {
            if (target == null) return;

            float chainDamage = baseDamage * chainDamagePercent;
            Collider2D[] hitColliders = Physics2D.OverlapCircleAll(target.transform.position, detectRadius);

            for (int i = 0; i < hitColliders.Length; i++)
            {
                Collider2D hit = hitColliders[i];
                if (hit.gameObject == target.gameObject) continue;

                if (hit.CompareTag("Enemy"))
                {
                    Nytherion.GamePlay.Characters.Enemy.EnemyBase nextEnemy = hit.GetComponent<Nytherion.GamePlay.Characters.Enemy.EnemyBase>();
                    if (nextEnemy != null && !nextEnemy.isDead)
                    {
                        nextEnemy.TakeDamage(chainDamage, true);

                        StatusEffectManager effectManager = nextEnemy.GetComponent<StatusEffectManager>();
                        if (effectManager == null)
                        {
                            effectManager = nextEnemy.gameObject.AddComponent<StatusEffectManager>();
                        }
                        effectManager.ApplyEffect(new LightningEffect(this.Duration));
                    }
                }
            }
        }
    }
}
