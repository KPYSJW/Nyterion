using System.Collections.Generic;
using Nytherion.Core.Managers;
using Nytherion.Data.ScriptableObjects.Skill;
using Nytherion.GamePlay.Combat;
using UnityEngine;

namespace Nytherion.GamePlay.Skills
{
    [RequireComponent(typeof(BoxCollider2D))]
    public sealed class ProjectileGateController : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer leftEndpoint;
        [SerializeField] private SpriteRenderer rightEndpoint;
        [SerializeField] private Transform beamVisual;
        [SerializeField] private Animator beamAnimator;

        private ProjectileGateSkillData data;
        private Transform owner;
        private float expiresAt;
        private BoxCollider2D gateCollider;
        private float halfWidth;
        private float outputHalfWidth;
        private readonly List<ProjectileGateEntryEffect> effects = new List<ProjectileGateEntryEffect>();

        public int LaserOutputCount => data != null ? Mathf.Clamp(data.outputCount, 2, 32) : 0;
        public float LaserSizeMultiplier => data != null ? Mathf.Clamp(data.sizeMultiplier, 0.05f, 0.99f) : 1f;

        public bool TryIntersectLaser(Transform caster, Vector3 start, Vector2 direction,
            float maxLength, out float distance)
        {
            distance = 0f;
            if (data == null || owner == null || caster != owner || !isActiveAndEnabled || Time.time >= expiresAt)
                return false;
            float denominator = Vector2.Dot(direction, transform.up);
            if (Mathf.Abs(denominator) < 0.0001f) return false;
            distance = Vector2.Dot((Vector2)(transform.position - start), transform.up) / denominator;
            if (distance <= 0.001f || distance > maxLength) return false;
            Vector3 hitPoint = start + (Vector3)direction * distance;
            return Mathf.Abs(transform.InverseTransformPoint(hitPoint).x) <= halfWidth;
        }

        public Vector3 GetLaserOutputPosition(float normalizedOffset) =>
            transform.TransformPoint(Vector3.right * (Mathf.Clamp(normalizedOffset, -1f, 1f) * outputHalfWidth));

        public Vector2 GetLaserOutputDirection(Vector2 incomingDirection, float normalizedAngle)
        {
            Vector2 forward = transform.up;
            if (Vector2.Dot(incomingDirection, forward) < 0f) forward = -forward;
            float angle = Mathf.Clamp(normalizedAngle, -1f, 1f) * Mathf.Clamp(data.laserSpreadHalfAngle, 0f, 89f);
            return Quaternion.Euler(0f, 0f, angle) * forward;
        }

        public void Begin(ProjectileGateSkillData settings, Transform caster, Vector2 direction)
        {
            data = settings;
            owner = caster;
            direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
            // 로컬 X축이 선 BC, 로컬 Y축이 플레이어에서 A를 향하는 방향입니다.
            Vector3 center = caster.position + (Vector3)direction * Mathf.Max(0.1f, data.forwardDistance);
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
            transform.SetPositionAndRotation(center, Quaternion.Euler(0f, 0f, angle));
            transform.localScale = Vector3.one;
            halfWidth = Mathf.Max(0.1f, data.halfWidth);
            gateCollider = GetComponent<BoxCollider2D>();
            gateCollider.isTrigger = true;
            gateCollider.size = new Vector2(halfWidth * 2f, Mathf.Max(0.01f, data.detectionThickness));
            gateCollider.offset = Vector2.zero;
            SetEndpoint(leftEndpoint, -halfWidth, true);
            SetEndpoint(rightEndpoint, halfWidth, false);
            // 양 끝 구조물의 실제 스프라이트 너비와 이펙트 여백을 발사 구간에서 제외합니다.
            float endpointInset = Mathf.Max(GetEndpointInset(leftEndpoint), GetEndpointInset(rightEndpoint));
            outputHalfWidth = Mathf.Max(0f, halfWidth - endpointInset - Mathf.Max(0f, data.outputEndpointPadding));
            SpriteRenderer beam = beamVisual.GetComponent<SpriteRenderer>();
            float spriteWidth = beam.sprite.bounds.size.x;
            beamVisual.localScale = new Vector3(halfWidth * 2f / spriteWidth, 1f, 1f);
            expiresAt = Time.time + Mathf.Max(0.01f, data.lifetime);
            gameObject.SetActive(true);
            beamAnimator.Rebind();
            beamAnimator.Play(0, 0, 0f);
            beamAnimator.Update(0f);
            Physics2D.SyncTransforms();
        }

        private void SetEndpoint(SpriteRenderer renderer, float x, bool mirrored)
        {
            renderer.transform.localPosition = Vector3.right * x;
            renderer.transform.localScale = Vector3.one * Mathf.Max(0.01f, data.endpointScale);
            renderer.flipX = mirrored;
        }

        private static float GetEndpointInset(SpriteRenderer renderer)
        {
            if (renderer == null || renderer.sprite == null) return 0f;
            Bounds bounds = renderer.sprite.bounds;
            return Mathf.Max(Mathf.Abs(bounds.min.x), Mathf.Abs(bounds.max.x)) * Mathf.Abs(renderer.transform.localScale.x);
        }

        private void Update()
        {
            if (data == null || owner == null || Time.time >= expiresAt) gameObject.SetActive(false);
        }

        public bool TryConvert(CollisionObject source)
        {
            if (data == null || !isActiveAndEnabled || Time.time >= expiresAt || source == null ||
                !source.gameObject.activeInHierarchy || source.HasPassedProjectileGate ||
                source.ProjectileOwner == null || source.ProjectileOwner != owner ||
                !source.TryGetComponent(out Rigidbody2D sourceBody) || sourceBody.velocity.sqrMagnitude < 0.0001f)
                return false;

            ObjectPoolManager pool = ObjectPoolManager.Instance;
            if (pool == null || !pool.TryGetPoolTag(source.gameObject, out string poolTag)) return false;
            Vector2 direction = sourceBody.velocity.normalized;
            float speed = sourceBody.velocity.magnitude * Mathf.Max(1.01f, data.speedMultiplier);
            Vector3 scale = source.transform.localScale * Mathf.Clamp(data.sizeMultiplier, 0.05f, 0.99f);
            Quaternion rotation = source.transform.rotation;
            Vector2 exitNormal = transform.up;
            if (Vector2.Dot(direction, exitNormal) < 0f) exitNormal = -exitNormal;
            int spawned = 0;
            for (int i = 0; i < Mathf.Clamp(data.outputCount, 2, 32); i++)
            {
                Vector2 outputDirection = GetLaserOutputDirection(direction, Random.Range(-1f, 1f));
                // 원본 스프라이트의 방향 보정은 유지하면서 실제 발사 방향만큼 회전합니다.
                Quaternion outputRotation = Quaternion.Euler(0f, 0f,
                    Vector2.SignedAngle(direction, outputDirection)) * rotation;
                // 큰 산개 각도에서도 출구는 선 가까이에 두고 감지 영역 바깥에서 출발합니다.
                Vector3 point = GetLaserOutputPosition(Random.Range(-1f, 1f));
                point += (Vector3)exitNormal * (gateCollider.size.y * 0.5f + 0.01f);
                GameObject output = pool.SpawnFromPool(poolTag, point, outputRotation);
                if (output == null) continue;
                if (!output.TryGetComponent(out CollisionObject collision) || !output.TryGetComponent(out Rigidbody2D body))
                {
                    pool.ReturnToPool(poolTag, output);
                    continue;
                }
                source.CopyGatePayloadTo(collision);
                output.transform.localScale = scale;
                collision.effectSizeMultiplier = source.effectSizeMultiplier * Mathf.Clamp(data.sizeMultiplier, 0.05f, 0.99f);
                if (output.TryGetComponent(out IProj projectile)) projectile.SetSpeed(speed);
                // 유도·사인파·부메랑도 무작위 출사 방향으로 출발하며 풀 재사용 시 원상 복구합니다.
                if (!output.TryGetComponent(out ProjectileGateFlight flight)) flight = output.AddComponent<ProjectileGateFlight>();
                flight.Begin(data.outputLifetime, outputRotation, data.outputProjectileMaterial);
                body.position = point;
                body.angularVelocity = 0f;
                body.velocity = outputDirection * speed;
                PlayOutputEffect(point, outputDirection);
                spawned++;
            }
            if (spawned == 0) return false;
            PlayEntryEffect(source.transform.position, direction);
            source.ReturnToPool();
            return true;
        }

        private void PlayEntryEffect(Vector3 sourcePosition, Vector2 incomingDirection)
        {
            if (data.entryEffectPrefabs == null || data.entryEffectPrefabs.Length == 0) return;
            ProjectileGateEntryEffect prefab = data.entryEffectPrefabs[Random.Range(0, data.entryEffectPrefabs.Length)];
            if (prefab == null) return;
            ProjectileGateEntryEffect effect = GetEffect(prefab);

            // 트리거 두께와 고속탄의 초과 이동량을 제거해 실제 게이트 선에 붙입니다.
            float denominator = Vector2.Dot(incomingDirection, transform.up);
            Vector3 point = sourcePosition;
            if (Mathf.Abs(denominator) > 0.0001f)
                point -= (Vector3)incomingDirection *
                    (Vector2.Dot((Vector2)(sourcePosition - transform.position), transform.up) / denominator);
            Vector3 localPoint = transform.InverseTransformPoint(point);
            point = transform.TransformPoint(new Vector3(Mathf.Clamp(localPoint.x, -halfWidth, halfWidth), 0f, 0f));
            SpriteRenderer beam = beamVisual.GetComponent<SpriteRenderer>();
            effect.Play(prefab, point, incomingDirection, data.entryEffectScale, beam);
        }

        private void PlayOutputEffect(Vector3 point, Vector2 outputDirection)
        {
            if (data.outputEffectPrefab == null) return;
            ProjectileGateEntryEffect effect = GetEffect(data.outputEffectPrefab);
            SpriteRenderer beam = beamVisual.GetComponent<SpriteRenderer>();
            // 스프라이트의 보정 각도가 아닌 실제 분열탄의 발사 방향을 따릅니다.
            effect.PlayFacing(data.outputEffectPrefab, point, outputDirection, data.outputEffectScale, beam);
        }

        private ProjectileGateEntryEffect GetEffect(ProjectileGateEntryEffect prefab)
        {
            ProjectileGateEntryEffect effect = effects.Find(candidate => candidate != null &&
                !candidate.gameObject.activeSelf && candidate.Prefab == prefab);
            if (effect == null)
            {
                effect = Instantiate(prefab, transform);
                effects.Add(effect);
            }
            return effect;
        }

        private void OnDisable()
        {
            foreach (ProjectileGateEntryEffect effect in effects)
                if (effect != null) effect.gameObject.SetActive(false);
        }
    }
}
