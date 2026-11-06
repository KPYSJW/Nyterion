using System;

namespace Nytherion.GamePlay.Characters.Enemy
{
    public sealed class BossWitchTornadoTimeline
    {
        public enum Phase { Pending, Warning, Active, Dissipating, Finished }
        public float StartedAt { get; private set; }
        public float ActiveAt { get; private set; }
        public float ExpiresAt { get; private set; }
        public float FinishedAt { get; private set; }
        private float nextDamageAt;
        private float damageInterval;

        public void Start(float now, float warning, float duration, float dissipate, float tickInterval)
        {
            StartedAt = now;
            ActiveAt = now + Math.Max(0.05f, warning);
            ExpiresAt = ActiveAt + Math.Max(0.05f, duration);
            FinishedAt = ExpiresAt + Math.Max(0f, dissipate);
            nextDamageAt = ActiveAt;
            damageInterval = Math.Max(0.05f, tickInterval);
        }

        public Phase GetPhase(float now)
        {
            if (now < StartedAt) return Phase.Pending;
            if (now < ActiveAt) return Phase.Warning;
            if (now < ExpiresAt) return Phase.Active;
            return now < FinishedAt ? Phase.Dissipating : Phase.Finished;
        }

        public bool TryDamageTick(float now)
        {
            if (GetPhase(now) != Phase.Active || now < nextDamageAt) return false;
            nextDamageAt = now + damageInterval;
            return true;
        }
    }
}
