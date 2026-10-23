using Nytherion.Data.ScriptableObjects.Skill;
using UnityEngine;

namespace Nytherion.GamePlay.Skills
{
    /// <summary>플레이어와 포탈의 점유 공간을 함께 쓸어 벽 너머 생성과 도착 지점 겹침을 막습니다.</summary>
    public static class PortalGatePlacement
    {
        private static readonly RaycastHit2D[] hits = new RaycastHit2D[64];
        private static readonly Collider2D[] overlaps = new Collider2D[1];
        private static readonly Plane[] cameraPlanes = new Plane[6];

        public static Bounds GetFootprint(Transform caster, PortalGateSkillData data)
        {
            Bounds footprint = new Bounds(Vector3.zero, Vector3.one * Mathf.Max(0.1f, data.placementRadius) * 2f);
            foreach (Collider2D collider in caster.GetComponentsInChildren<Collider2D>())
            {
                if (!collider.isActiveAndEnabled || collider.isTrigger) continue;
                Bounds bounds = collider.bounds;
                bounds.center -= caster.position;
                footprint.Encapsulate(bounds);
            }
            return footprint;
        }

        private static ContactFilter2D Filter(PortalGateSkillData data)
        {
            ContactFilter2D filter = new ContactFilter2D();
            filter.SetLayerMask(data.blockingLayers);
            filter.useTriggers = false;
            return filter;
        }

        public static bool IsClear(Vector2 position, Bounds footprint, PortalGateSkillData data)
        {
            return Physics2D.OverlapBox(position + (Vector2)footprint.center,
                footprint.size, 0f, Filter(data), overlaps) == 0;
        }

        public static float ClearDistance(Vector2 origin, Vector2 direction, float distance,
            Bounds footprint, PortalGateSkillData data)
        {
            int count = Physics2D.BoxCast(origin + (Vector2)footprint.center, footprint.size, 0f,
                direction, Filter(data), hits, distance);
            float nearest = distance;
            for (int i = 0; i < count; i++)
                nearest = Mathf.Min(nearest, Mathf.Max(0f, hits[i].distance - Mathf.Max(0.01f, data.wallPadding)));
            // 버퍼가 가득 찬 경우 누락된 벽을 넘어가지 않도록 배치를 거부합니다.
            return count == hits.Length ? 0f : nearest;
        }

        /// <summary>시전 순간 카메라의 네 화면 경계 안에 점유 공간이 들어가는 마지막 지점을 구합니다.</summary>
        public static bool TryGetCameraDistance(Camera camera, Vector3 origin, Vector2 direction,
            Bounds footprint, float padding, out float distance)
        {
            distance = float.PositiveInfinity;
            if (camera == null || direction.sqrMagnitude < 0.0001f) return false;
            direction.Normalize();
            GeometryUtility.CalculateFrustumPlanes(camera, cameraPlanes);
            // 앞/뒤 클리핑 평면 대신 좌/우/상/하 화면 경계만 사용합니다.
            for (int i = 0; i < 4; i++)
            {
                Plane plane = cameraPlanes[i];
                Vector3 normal = plane.normal;
                float support = Mathf.Abs(normal.x) * footprint.extents.x +
                    Mathf.Abs(normal.y) * footprint.extents.y;
                float clearance = plane.GetDistanceToPoint(origin + footprint.center) -
                    support - Mathf.Max(0f, padding);
                if (clearance < 0f) return false;
                float approach = Vector2.Dot(normal, direction);
                if (approach < -0.00001f) distance = Mathf.Min(distance, clearance / -approach);
            }
            return !float.IsInfinity(distance) && distance > 0f;
        }

        public static bool TryFindDestination(Transform caster, Vector2 direction, PortalGateSkillData data, Camera camera,
            out Vector3 destination)
        {
            Physics2D.SyncTransforms();
            Bounds footprint = GetFootprint(caster, data);
            destination = caster.position;
            if (data.blockingLayers.value == 0 || direction.sqrMagnitude < 0.0001f ||
                !IsClear(caster.position, footprint, data)) return false;
            direction.Normalize();
            if (!TryGetCameraDistance(camera, caster.position, direction, footprint,
                data.screenEdgePadding, out float cameraDistance)) return false;
            float distance = ClearDistance(caster.position, direction, cameraDistance, footprint, data);
            float separation = Mathf.Max(data.minimumSeparation, data.interactionRadius * 2f + data.wallPadding);
            if (distance < separation) return false;
            destination += (Vector3)(direction * distance);
            return IsClear(destination, footprint, data);
        }
    }
}
