using UnityEngine;

namespace Nytherion.GamePlay.Combat
{
    /// <summary>냉기 상태인 적의 몸체 주변에 눈을 대각선으로 내립니다.</summary>
    [DefaultExecutionOrder(1100)]
    public sealed class ChillSnowVFX : MonoBehaviour
    {
        [SerializeField] private Sprite snowSprite;
        [SerializeField] private Shader clipShader;
        [SerializeField, Min(0f), InspectorName("초당 눈 입자 수")]
        private float emissionPerSecond = 16f;
        [SerializeField, Min(1), InspectorName("최대 눈 입자 수")]
        private int maxParticles = 32;
        [SerializeField, InspectorName("눈 생성 가로 범위")]
        [Tooltip("몸체 중심을 기준으로 한 생성 너비 비율입니다. 오른쪽에는 대각선 이동을 위한 여유가 추가됩니다.")]
        private Vector2 spawnHorizontalRange = new Vector2(-0.2f, 0.525f);
        [SerializeField, InspectorName("눈 생성 세로 범위")]
        [Tooltip("몸체 중심을 기준으로 한 높이 비율입니다. X는 아래쪽, Y는 위쪽 경계입니다.")]
        private Vector2 spawnVerticalRange = new Vector2(0.05f, 0.575f);
        [SerializeField, InspectorName("눈 이동 속도 범위")]
        [Tooltip("몸체의 짧은 변에 대한 초당 이동 속도 비율입니다.")]
        private Vector2 fallSpeedRange = new Vector2(0.65f, 0.95f);
        [SerializeField, Range(0f, 80f), InspectorName("눈 낙하 기울기 (도)")]
        [Tooltip("수직 아래 방향을 0도로 하여 왼쪽으로 기울어지는 각도입니다.")]
        private float fallAngle = 30f;
        [SerializeField, InspectorName("눈 수명 범위 (초)")]
        [Tooltip("수명이 길수록 눈이 더 멀리 이동합니다.")]
        private Vector2 lifetimeRange = new Vector2(0.7f, 1.1f);
        [SerializeField, InspectorName("눈 크기 범위")]
        [Tooltip("몸체 높이에 대한 눈 입자 크기 비율입니다.")]
        private Vector2 particleSizeRange = new Vector2(0.04f, 0.08f);

        private readonly SpriteBodyBounds bodyBounds = new SpriteBodyBounds();
        private SpriteRenderer body;
        private ParticleSystem particles;
        private ParticleSystemRenderer particleRenderer;
        private Material particleMaterial;
        private MaterialPropertyBlock clipProperties;
        private static readonly int ClipRectId = Shader.PropertyToID("_ClipRect");
        private ParticleSystem.Particle[] particleBuffer;
        private float emissionTimer;

        public void Play(SpriteRenderer source)
        {
            if (source == null || source.sprite == null || snowSprite == null || clipShader == null) return;
            if (particles != null && particles.isPlaying) return;
            body = source;
            EnsureParticles();
            gameObject.SetActive(true);
            enabled = true;
            UpdatePlacement();
            particles.Play();
            emissionTimer = 0f;
        }

        private void EnsureParticles()
        {
            if (particles != null) return;
            GameObject child = new GameObject("냉기 눈 입자");
            child.transform.SetParent(transform, false);
            particles = child.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = particles.main;
            main.playOnAwake = false;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.startSpeed = 0f;
            main.maxParticles = Mathf.Max(1, maxParticles);
            ParticleSystem.EmissionModule emission = particles.emission;
            emission.enabled = false;
            ParticleSystem.ShapeModule shape = particles.shape;
            shape.enabled = false;
            ParticleSystem.TextureSheetAnimationModule animation = particles.textureSheetAnimation;
            animation.enabled = true;
            animation.mode = ParticleSystemAnimationMode.Sprites;
            animation.AddSprite(snowSprite);
            particleRenderer = child.GetComponent<ParticleSystemRenderer>();
            particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
            particleRenderer.alignment = ParticleSystemRenderSpace.View;
            particleMaterial = new Material(clipShader);
            particleMaterial.mainTexture = snowSprite.texture;
            particleRenderer.sharedMaterial = particleMaterial;
            clipProperties = new MaterialPropertyBlock();
        }

        private void LateUpdate()
        {
            if (body == null || body.sprite == null || !body.enabled || !body.gameObject.activeInHierarchy)
            {
                Stop();
                return;
            }
            Bounds bounds = UpdatePlacement();
            RemoveParticlesBelowBody(bounds.min.y);
            if (emissionPerSecond <= 0f) return;
            emissionTimer += Time.deltaTime;
            float interval = 1f / emissionPerSecond;
            while (emissionTimer >= interval)
            {
                emissionTimer -= interval;
                if (particles.particleCount >= Mathf.Max(1, maxParticles)) continue;
                float width = Mathf.Max(bounds.size.x, 0.1f);
                float height = Mathf.Max(bounds.size.y, 0.1f);
                float size = Mathf.Clamp(height, 0.35f, 2f);
                // 몸체 크기에 비례한 범위에서 생성해 좌측 아래로 내립니다.
                float speed = Mathf.Max(0f, RandomRange(fallSpeedRange)) * Mathf.Min(width, height);
                float horizontalSpeed = speed * Mathf.Tan(Mathf.Clamp(fallAngle, 0f, 80f) * Mathf.Deg2Rad);
                float lifetime = Mathf.Max(0.01f, RandomRange(lifetimeRange));
                // 오른쪽 바깥에서 들어오는 눈도 생성해 경계 근처가 비지 않도록 합니다.
                float upstreamWidth = Mathf.Min(horizontalSpeed * lifetime,
                    Mathf.Tan(Mathf.Clamp(fallAngle, 0f, 80f) * Mathf.Deg2Rad) * height);
                float left = Mathf.Min(spawnHorizontalRange.x, spawnHorizontalRange.y) * width;
                float right = Mathf.Max(spawnHorizontalRange.x, spawnHorizontalRange.y) * width;
                float particleSize = Mathf.Max(0.001f, RandomRange(particleSizeRange)) * size;
                Vector3 velocity = new Vector3(-horizontalSpeed, -speed, 0f);
                if (!TryFindSpawnPosition(bounds, left, right + upstreamWidth, height,
                    velocity, lifetime, particleSize, out Vector3 position)) continue;
                ParticleSystem.EmitParams parameters = new ParticleSystem.EmitParams();
                parameters.position = particles.transform.InverseTransformPoint(position);
                parameters.velocity = particles.transform.InverseTransformVector(velocity);
                parameters.startLifetime = lifetime;
                parameters.startSize = particleSize;
                particles.Emit(parameters, 1);
            }
        }

        private bool TryFindSpawnPosition(Bounds bounds, float left, float right, float height,
            Vector3 velocity, float lifetime, float size, out Vector3 position)
        {
            int count = particles.particleCount;
            EnsureParticleBuffer(count);
            int alive = count > 0 ? particles.GetParticles(particleBuffer) : 0;
            float bestDistance = -1f;
            position = default;
            float width = Mathf.Max(bounds.size.x, 0.1f);
            for (int attempt = 0; attempt < 12; attempt++)
            {
                Vector3 candidate = bounds.center + new Vector3(
                    Random.Range(left, right), RandomRange(spawnVerticalRange) * height, 0f);
                float nearest = float.PositiveInfinity;
                bool overlaps = false;
                for (int i = 0; i < alive && !overlaps; i++)
                {
                    ParticleSystem.Particle existing = particleBuffer[i];
                    Vector3 otherPosition = particles.transform.TransformPoint(existing.position);
                    Vector3 otherVelocity = particles.transform.TransformVector(existing.velocity);
                    float minimumDistance = (size + existing.GetCurrentSize(particles)) * 0.5f;
                    Vector3 delta = candidate - otherPosition;
                    Vector3 relativeVelocity = velocity - otherVelocity;
                    float speedSquared = relativeVelocity.x * relativeVelocity.x + relativeVelocity.y * relativeVelocity.y;
                    float closestTime = speedSquared > 0.000001f
                        ? Mathf.Clamp(-(delta.x * relativeVelocity.x + delta.y * relativeVelocity.y) / speedSquared,
                            0f, Mathf.Min(existing.remainingLifetime, lifetime))
                        : 0f;
                    Vector3 closest = delta + relativeVelocity * closestTime;
                    if (closest.x * closest.x + closest.y * closest.y < minimumDistance * minimumDistance)
                    {
                        overlaps = true;
                        break;
                    }
                    float x = delta.x / width;
                    float y = delta.y / height;
                    nearest = Mathf.Min(nearest, x * x + y * y);
                }
                if (overlaps || nearest <= bestDistance) continue;
                bestDistance = nearest;
                position = candidate;
            }
            return bestDistance >= 0f;
        }

        private void EnsureParticleBuffer(int count)
        {
            if (particleBuffer == null || particleBuffer.Length < count)
                particleBuffer = new ParticleSystem.Particle[Mathf.Max(count, particles.main.maxParticles)];
        }

        private void RemoveParticlesBelowBody(float bottom)
        {
            int count = particles.particleCount;
            if (count == 0) return;
            EnsureParticleBuffer(count);
            int alive = particles.GetParticles(particleBuffer);
            bool removed = false;
            for (int i = 0; i < alive; i++)
            {
                float radius = particleBuffer[i].GetCurrentSize(particles) * 0.5f;
                if (particles.transform.TransformPoint(particleBuffer[i].position).y + radius >= bottom) continue;
                particleBuffer[i].remainingLifetime = -1f;
                removed = true;
            }
            if (removed) particles.SetParticles(particleBuffer, alive);
        }

        private Bounds UpdatePlacement()
        {
            Bounds bounds = bodyBounds.GetBounds(body);
            Transform emitter = particles.transform;
            emitter.position = bounds.center;
            emitter.rotation = Quaternion.identity;
            Vector3 parentScale = emitter.parent.lossyScale;
            emitter.localScale = new Vector3(InverseScale(parentScale.x), InverseScale(parentScale.y), InverseScale(parentScale.z));
            particleRenderer.sortingLayerID = body.sortingLayerID;
            particleRenderer.sortingOrder = body.sortingOrder + 1;
            clipProperties.SetVector(ClipRectId, new Vector4(bounds.min.x, bounds.min.y, bounds.max.x, bounds.max.y));
            particleRenderer.SetPropertyBlock(clipProperties);
            return bounds;
        }

        private static float RandomRange(Vector2 range) => Random.Range(Mathf.Min(range.x, range.y), Mathf.Max(range.x, range.y));
        private static float InverseScale(float value) => Mathf.Abs(value) < 0.001f ? 1f : 1f / value;

        public void Stop()
        {
            if (particles != null) particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            emissionTimer = 0f;
            body = null;
            enabled = false;
        }

        private void OnDisable() => Stop();

        private void OnDestroy()
        {
            if (particleMaterial != null) Destroy(particleMaterial);
        }
    }
}
