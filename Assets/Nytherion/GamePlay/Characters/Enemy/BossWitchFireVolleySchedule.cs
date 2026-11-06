using System;

namespace Nytherion.GamePlay.Characters.Enemy
{
    public sealed class BossWitchFireVolleySchedule
    {
        public int VolleyCount { get; private set; }
        public int ProjectilesPerVolley { get; private set; }
        public int CompletedVolleys { get; private set; }
        public float NextVolleyAt { get; private set; }
        private float interval;
        private float spread;

        public bool IsComplete => CompletedVolleys >= VolleyCount;

        public void Start(float now, int volleys, int projectiles, float volleyInterval, float spreadAngle)
        {
            VolleyCount = Math.Max(1, volleys);
            ProjectilesPerVolley = Math.Max(1, projectiles);
            CompletedVolleys = 0;
            NextVolleyAt = now;
            interval = Math.Max(0.05f, volleyInterval);
            spread = Math.Max(0f, Math.Min(360f, spreadAngle));
        }

        public bool TryAdvance(float now)
        {
            if (IsComplete || now < NextVolleyAt) return false;
            CompletedVolleys++;
            // A slow frame delays the next wave instead of stacking missed waves together.
            NextVolleyAt = now + interval;
            return true;
        }

        public float GetAngleOffset(int projectileIndex)
        {
            if (projectileIndex < 0 || projectileIndex >= ProjectilesPerVolley)
                throw new ArgumentOutOfRangeException(nameof(projectileIndex));
            return ProjectilesPerVolley == 1 ? 0f :
                -spread * 0.5f + spread * projectileIndex / (ProjectilesPerVolley - 1);
        }
    }
}
