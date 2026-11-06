using UnityEngine;

namespace Nytherion.GamePlay.Characters.Enemy
{
    public static class BossDemonSlimeAttackArea
    {
        public static bool ContainsCircle(Vector2 point, Vector2 origin, float radius)
        {
            radius = Mathf.Max(0.1f, radius);
            return (point - origin).sqrMagnitude <= radius * radius + 0.0001f;
        }

        public static Vector2 CirclePoint(Vector2 origin, float radius, float fraction)
        {
            float radians = Mathf.Clamp01(fraction) * Mathf.PI * 2f;
            return origin + new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * Mathf.Max(0.1f, radius);
        }

        public static bool Contains(Vector2 point, Vector2 origin, Vector2 direction, float radius, float angle)
        {
            Vector2 offset = point - origin;
            radius = Mathf.Max(0.1f, radius);
            if (offset.sqrMagnitude > radius * radius + 0.0001f) return false;
            if (offset.sqrMagnitude < 0.0001f) return true;
            Vector2 forward = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
            float limit = Mathf.Cos(Mathf.Clamp(angle, 10f, 180f) * 0.5f * Mathf.Deg2Rad);
            return Vector2.Dot(offset.normalized, forward) >= limit - 0.0001f;
        }

        public static Vector2 ArcPoint(Vector2 origin, Vector2 direction, float radius, float angle, float fraction)
        {
            float facing = direction.sqrMagnitude > 0.0001f ? Mathf.Atan2(direction.y, direction.x) : 0f;
            float radians = facing + (Mathf.Clamp01(fraction) - 0.5f) *
                Mathf.Clamp(angle, 10f, 180f) * Mathf.Deg2Rad;
            return origin + new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * Mathf.Max(0.1f, radius);
        }
    }
}
