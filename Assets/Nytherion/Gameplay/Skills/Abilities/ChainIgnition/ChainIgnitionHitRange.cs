using UnityEngine;

namespace Nytherion.GamePlay.Skills
{
    /// <summary>가로·세로 축에 정렬된 타원과 대상 피격 영역의 접촉을 검사합니다.</summary>
    public static class ChainIgnitionHitRange
    {
        public static bool OverlapsBounds(Vector2 center, Vector2 radii, Bounds bounds)
        {
            radii = new Vector2(Mathf.Max(0.01f, radii.x), Mathf.Max(0.01f, radii.y));
            // 타원 중심에 가장 가까운 몸체 사각형의 점을 사용해 세로로 긴 적도 전체 크기로 판정합니다.
            Vector2 closest = bounds.ClosestPoint(new Vector3(center.x, center.y, bounds.center.z));
            Vector2 offset = closest - center;
            float x = offset.x / radii.x;
            float y = offset.y / radii.y;
            return x * x + y * y <= 1f + 0.000001f;
        }

        public static bool OverlapsCircle(Vector2 offset, Vector2 radii, float circleRadius)
        {
            radii = new Vector2(Mathf.Max(0.01f, radii.x), Mathf.Max(0.01f, radii.y));
            float radius = Mathf.Max(0f, circleRadius);
            if (Mathf.Approximately(radii.x, radii.y))
                return offset.sqrMagnitude <= (radii.x + radius) * (radii.x + radius);

            double x = System.Math.Abs(offset.x), y = System.Math.Abs(offset.y);
            double a = radii.x, b = radii.y;
            if (x > a + radius || y > b + radius) return false;
            if (x * x / (a * a) + y * y / (b * b) <= 1d) return true;

            // 타원 경계의 최근접점을 구해 발 영역 반경과 비교합니다. 단순히 두 축에 반경을 더하면 대각선 판정이 달라집니다.
            double a2 = a * a, b2 = b * b;
            double lower = 0d, upper = System.Math.Max(a * x, b * y);
            while (BoundaryEquation(upper, a, b, a2, b2, x, y) > 1d) upper *= 2d;
            for (int iteration = 0; iteration < 32; iteration++)
            {
                double middle = (lower + upper) * 0.5d;
                if (BoundaryEquation(middle, a, b, a2, b2, x, y) > 1d) lower = middle;
                else upper = middle;
            }
            double closestX = a2 * x / (upper + a2);
            double closestY = b2 * y / (upper + b2);
            double dx = x - closestX, dy = y - closestY;
            return dx * dx + dy * dy <= radius * radius + 0.00000001d;
        }

        private static double BoundaryEquation(double t, double a, double b, double a2, double b2, double x, double y)
        {
            double horizontal = a * x / (t + a2);
            double vertical = b * y / (t + b2);
            return horizontal * horizontal + vertical * vertical;
        }
    }
}
