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
            GameObject projObj = SpawnProj(direction, default, chargePercent);

            if (projObj != null && projObj.TryGetComponent<CollisionObject>(out CollisionObject collisionObj))
            {
                float currentDamageMultiplier = IsChargingEnabled() ? Mathf.Lerp(1.0f, maxDamageMultiplier, chargePercent) : 1.0f;
                collisionObj.damage = weaponData.damage * currentDamageMultiplier;

                PiercingModifier piercingModifier = projObj.GetComponent<PiercingModifier>();
                if (piercingModifier == null) return;

                if (IsChargingEnabled() && chargePercent >= pierceThreshold)
                {
                    piercingModifier.enabled = true;

                    projObj.transform.localScale *= 1.5f;
                    SpriteRenderer sr;
                    if (projObj.TryGetComponent<SpriteRenderer>(out sr))
                    {
                        sr.color = Color.red;
                    }
                }
                else
                {
                    piercingModifier.enabled = false;
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
