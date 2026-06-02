using UnityEngine;
using Nytherion.Core.Interfaces;

namespace Nytherion.GamePlay.Combat
{
    public class PoisonStatusEffect : MonoBehaviour
    {
        private float tickDamage;
        private float duration;
        private float timer;
        private float tickInterval = 1.0f;
        private float nextTickTime;
        private IDamageable targetDamageable;

        public void Initialize(float damage, float dur)
        {
            tickDamage = damage;
            duration = dur;
            timer = dur;
            targetDamageable = GetComponent<IDamageable>();
            nextTickTime = Time.time + tickInterval;
        }

        public void ResetDuration()
        {
            timer = duration;
        }

        private void Update()
        {
            timer -= Time.deltaTime;
            if (timer <= 0)
            {
                Destroy(this);
                return;
            }

            if (Time.time >= nextTickTime)
            {
                if (targetDamageable != null)
                {
                    targetDamageable.TakeDamage(tickDamage);
                }
                nextTickTime = Time.time + tickInterval;
            }
        }
    }
}
