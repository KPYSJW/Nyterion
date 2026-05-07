using UnityEngine;

namespace Nytherion.GamePlay.Combat.Weapons
{
    public class ChargedPiercingWeapon : ChargeableRangedWeapon
    {
        [Header("Piercing & Damage Settings")]
        public float maxDamageMultiplier = 2.0f;

        public float pierceThreshold = 0.8f;

        private Vector3 originalScale;

        private void Start()
        {
            originalScale = transform.localScale;
        }

        protected override void FireChargedAttack(Vector2 direction, float chargePercent)
        {
            GameObject projObj = Projectile(direction);

            if (projObj != null && projObj.TryGetComponent<CollisionObject>(out var collisionObj))
            {
                float currentDamageMultiplier = Mathf.Lerp(1.0f, maxDamageMultiplier, chargePercent);
                collisionObj.damage = weaponData.damage * currentDamageMultiplier;

                if (!projObj.TryGetComponent<PiercingEffect>(out var piercingEffect))
                {
                    piercingEffect = projObj.AddComponent<PiercingEffect>();
                }

                if (chargePercent >= pierceThreshold)
                {
                    piercingEffect.enabled = true;

                    projObj.transform.localScale *= 1.5f;
                    if (projObj.TryGetComponent<SpriteRenderer>(out var sr))
                    {
                        sr.color = Color.red;
                    }
                }
                else
                {
                    piercingEffect.enabled = false;
                }
            }
            transform.localScale = originalScale;
        }

        protected override void OnCharging(float chargePercent)
        {
            float scaleMultiplier = Mathf.Lerp(1.0f, 1.3f, chargePercent);
            transform.localScale = originalScale * scaleMultiplier;
        }
    }
}