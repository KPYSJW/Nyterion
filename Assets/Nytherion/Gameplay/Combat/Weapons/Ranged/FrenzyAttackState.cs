using System;

namespace Nytherion.GamePlay.Combat.Weapons
{
    /// <summary>광란의 클릭·차징·연사 입력 상태를 관리합니다.</summary>
    internal sealed class FrenzyAttackState
    {
        private enum Phase { Ready, Pending, Charging, Armed, TimedBurst, HeldBurst, AwaitRelease }
        private Phase phase;
        private float pressedAt;
        private float burstEndsAt;
        private bool pressing;

        public bool CanBegin => phase == Phase.Ready || phase == Phase.Armed;
        public bool IsCharging => phase == Phase.Charging;
        public bool IsFiring => phase == Phase.TimedBurst || phase == Phase.HeldBurst;
        public float ChargePercent { get; private set; }

        public void Begin(float now)
        {
            if (!CanBegin) return;
            pressing = true;
            pressedAt = now;
            if (phase == Phase.Armed)
                phase = Phase.HeldBurst;
            else
            {
                ChargePercent = 0f;
                phase = Phase.Pending;
            }
        }

        public void Advance(float now, float threshold, float chargeTime, bool powerDevice)
        {
            if (phase == Phase.Pending || phase == Phase.Charging)
            {
                float elapsed = Math.Max(0f, now - pressedAt);
                if (elapsed < Math.Max(0f, threshold)) return;
                phase = Phase.Charging;
                ChargePercent = chargeTime <= 0f ? 1f
                    : Math.Min(1f, Math.Max(0f, elapsed - Math.Max(0f, threshold)) / chargeTime);
            }
            else if (phase == Phase.Armed && !powerDevice)
            {
                Reset();
            }
            else if ((phase == Phase.TimedBurst && now >= burstEndsAt) ||
                     (phase == Phase.HeldBurst && (!pressing || !powerDevice)))
            {
                phase = pressing ? Phase.AwaitRelease : Phase.Ready;
                ChargePercent = 0f;
            }
        }

        /// <summary>미완성 차징을 놓았을 때만 단발 공격을 요청합니다.</summary>
        public bool Release(float now, float burstDuration, bool powerDevice)
        {
            pressing = false;
            if (phase == Phase.Charging && ChargePercent >= 1f)
            {
                phase = powerDevice ? Phase.Armed : Phase.TimedBurst;
                burstEndsAt = now + Math.Max(0f, burstDuration);
                return false;
            }
            bool singleShot = phase == Phase.Pending || phase == Phase.Charging;
            if (phase != Phase.TimedBurst && phase != Phase.Armed)
            {
                phase = Phase.Ready;
                ChargePercent = 0f;
            }
            return singleShot;
        }

        public void Reset()
        {
            phase = Phase.Ready;
            pressing = false;
            ChargePercent = 0f;
        }
    }
}
