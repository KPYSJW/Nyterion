using System.Collections.Generic;
using UnityEngine;

namespace Nytherion.GamePlay.Combat
{
    /// <summary>냉기 상태인 적의 몸체에 두 종류의 눈 결정 입자를 반복합니다.</summary>
    [DefaultExecutionOrder(1100)]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class ChillSnowVFX : MonoBehaviour
    {
        // 기존 프리팹의 직렬화 참조를 보존합니다.
        [SerializeField, HideInInspector] private Sprite[] frames;
        [SerializeField] private Texture2D animationSheet;
        [SerializeField, Min(1f)] private float framesPerSecond = 12f;
        [SerializeField, InspectorName("입자 생성 범위")]
        [Tooltip("X는 몸체 대비 좌우 범위, Y는 세로 범위입니다. (1, 1)이면 각 가로 위치의 위쪽 윤곽에서 생성됩니다.")]
        private Vector2 bodyCoverage = Vector2.one;
        [SerializeField, Min(0f), InspectorName("초당 눈 결정 수")] private float emissionPerSecond = 8f;
        [SerializeField, Min(1), InspectorName("최대 눈 결정 수")] private int maxParticles = 16;
        [SerializeField, Min(0.01f), InspectorName("입자 크기 배율")]
        [Tooltip("1은 현재 기본 크기입니다. 플레이 중 변경하면 새로 생성되는 입자부터 적용됩니다.")]
        private float particleSizeMultiplier = 1f;
        [SerializeField, Min(0f), InspectorName("최소 낙하 속도 (몸체 높이/초)")]
        [Tooltip("1이면 1초에 적 몸체 높이만큼 내려갑니다. 플레이 중 변경도 즉시 적용됩니다.")]
        private float minFallSpeed = 0.7f;
        [SerializeField, Min(0f), InspectorName("최대 낙하 속도 (몸체 높이/초)")]
        private float maxFallSpeed = 1.2f;

        private readonly SpriteBodyBounds bodyBounds = new SpriteBodyBounds();
        private readonly Dictionary<Sprite, Vector2[][]> outlineCache = new Dictionary<Sprite, Vector2[][]>();
        private readonly List<Vector2[]> worldOutlines = new List<Vector2[]>();
        private SpriteRenderer body;
        private ParticleSystem particles;
        private ParticleSystemRenderer particleRenderer;
        private Material particleMaterial;
        private ParticleSystem.Particle[] particleBuffer;
        private float emissionTimer;
        private bool playing;

        public void Play(SpriteRenderer source)
        {
            if (playing || source == null || source.sprite == null || animationSheet == null) return;
            body = source;
            EnsureParticles();
            GetComponent<SpriteRenderer>().enabled = false;
            gameObject.SetActive(true);
            enabled = true;
            playing = true;
            emissionTimer = 0f;
            UpdatePlacement();
            particles.Play();
            EmitParticle();
        }

        private void EnsureParticles()
        {
            if (particles != null) return;
            GameObject child = new GameObject("냉기 눈 결정 입자");
            child.transform.SetParent(transform, false);
            particles = child.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = particles.main;
            main.playOnAwake = false;
            main.loop = true;
            main.startSpeed = 0f;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            ParticleSystem.EmissionModule emission = particles.emission;
            emission.enabled = false;
            ParticleSystem.ShapeModule shape = particles.shape;
            shape.enabled = false;
            ParticleSystem.VelocityOverLifetimeModule velocity = particles.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.x = new ParticleSystem.MinMaxCurve(-0.04f, 0.04f);
            velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);
            ParticleSystem.TextureSheetAnimationModule animation = particles.textureSheetAnimation;
            animation.enabled = true;
            animation.mode = ParticleSystemAnimationMode.Grid;
            animation.numTilesX = 10;
            animation.numTilesY = 2;
            animation.animation = ParticleSystemAnimationType.SingleRow;
            // 생성 시 한 행을 고르고 같은 종류의 10프레임을 끝까지 재생합니다.
            animation.rowMode = ParticleSystemAnimationRowMode.Random;
            animation.timeMode = ParticleSystemAnimationTimeMode.Lifetime;
            animation.frameOverTime = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0f, 1f, 1f));
            animation.startFrame = new ParticleSystem.MinMaxCurve(0f);
            animation.cycleCount = 1;
            particleRenderer = child.GetComponent<ParticleSystemRenderer>();
            particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
            particleRenderer.alignment = ParticleSystemRenderSpace.View;
            particleMaterial = new Material(GetComponent<SpriteRenderer>().sharedMaterial);
            particleMaterial.mainTexture = animationSheet;
            particleRenderer.sharedMaterial = particleMaterial;
        }

        private void LateUpdate()
        {
            if (!playing) return;
            if (body == null || body.sprite == null || !body.enabled || !body.gameObject.activeInHierarchy)
            {
                Stop();
                return;
            }
            UpdatePlacement();
            RemoveParticlesBelowBody();
            if (emissionPerSecond <= 0f) return;
            float interval = 1f / emissionPerSecond;
            emissionTimer = Mathf.Min(emissionTimer + Time.deltaTime, interval * Mathf.Max(1, maxParticles));
            while (emissionTimer >= interval)
            {
                emissionTimer -= interval;
                EmitParticle();
            }
        }

        private void UpdatePlacement()
        {
            Bounds bounds = bodyBounds.GetBounds(body);
            UpdateBodyOutlines();
            Transform emitter = particles.transform;
            emitter.position = bounds.center;
            emitter.rotation = Quaternion.identity;
            Vector3 parentScale = emitter.parent.lossyScale;
            emitter.localScale = new Vector3(InverseScale(parentScale.x), InverseScale(parentScale.y), InverseScale(parentScale.z));
            ParticleSystem.MainModule main = particles.main;
            main.maxParticles = Mathf.Max(1, maxParticles);
            main.startLifetime = 10f / Mathf.Max(1f, framesPerSecond);
            // 32픽셀 프레임의 여백을 포함한 입자 크기입니다.
            float size = Mathf.Clamp(bounds.size.y, 0.6f, 1.8f) * Mathf.Max(0.01f, particleSizeMultiplier);
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.85f, size);
            // Inspector에서 지정한 속도를 몸체 높이에 비례해 적용합니다.
            float minimumSpeed = Mathf.Max(0f, minFallSpeed);
            float maximumSpeed = Mathf.Max(minimumSpeed, maxFallSpeed);
            ParticleSystem.VelocityOverLifetimeModule velocity = particles.velocityOverLifetime;
            velocity.y = new ParticleSystem.MinMaxCurve(-maximumSpeed * bounds.size.y, -minimumSpeed * bounds.size.y);
            particleRenderer.sortingLayerID = body.sortingLayerID;
            particleRenderer.sortingOrder = body.sortingOrder + 1;
        }

        private void EmitParticle()
        {
            if (particles.particleCount >= Mathf.Max(1, maxParticles)) return;
            Bounds bounds = bodyBounds.GetBounds(body);
            Vector3 extent = bounds.extents;
            float bottomMargin = particles.main.startSize.constantMax * 0.25f;
            for (int attempt = 0; attempt < 16; attempt++)
            {
                float x = bounds.center.x + Random.Range(-extent.x, extent.x) * Mathf.Clamp01(bodyCoverage.x);
                if (!TryGetColumnRange(x, out float bottom, out float top)) continue;
                float y = Mathf.Min(top, bounds.center.y + extent.y * Mathf.Clamp01(bodyCoverage.y));
                if (y - bottomMargin <= bottom) continue;
                ParticleSystem.EmitParams parameters = new ParticleSystem.EmitParams();
                parameters.position = particles.transform.InverseTransformPoint(new Vector3(x, y, bounds.center.z));
                particles.Emit(parameters, 1);
                return;
            }
        }

        private void RemoveParticlesBelowBody()
        {
            int capacity = particles.main.maxParticles;
            if (particleBuffer == null || particleBuffer.Length < capacity)
                particleBuffer = new ParticleSystem.Particle[capacity];
            int count = particles.GetParticles(particleBuffer);
            bool changed = false;
            for (int i = 0; i < count; i++)
            {
                Vector3 position = particles.transform.TransformPoint(particleBuffer[i].position);
                float bottomMargin = particleBuffer[i].GetCurrentSize(particles) * 0.25f;
                // 현재 가로 위치의 아래쪽 윤곽에 닿거나 옆으로 벗어나면 제거합니다.
                if (TryGetColumnRange(position.x, out float bottom, out _) &&
                    position.y - bottomMargin >= bottom) continue;
                particleBuffer[i].remainingLifetime = 0f;
                changed = true;
            }
            if (changed) particles.SetParticles(particleBuffer, count);
        }

        private void UpdateBodyOutlines()
        {
            Sprite sprite = body.sprite;
            if (!outlineCache.TryGetValue(sprite, out Vector2[][] outlines))
            {
                List<Vector2[]> shapes = new List<Vector2[]>();
                List<Vector2> points = new List<Vector2>();
                for (int i = 0; i < sprite.GetPhysicsShapeCount(); i++)
                {
                    sprite.GetPhysicsShape(i, points);
                    if (points.Count >= 3) shapes.Add(points.ToArray());
                }
                // 물리 윤곽이 없는 스프라이트는 실제 렌더 메시의 삼각형으로 범위를 구합니다.
                if (shapes.Count == 0)
                {
                    Vector2[] vertices = sprite.vertices;
                    ushort[] triangles = sprite.triangles;
                    for (int i = 0; i + 2 < triangles.Length; i += 3)
                        shapes.Add(new[] { vertices[triangles[i]], vertices[triangles[i + 1]], vertices[triangles[i + 2]] });
                }
                outlines = shapes.ToArray();
                outlineCache.Add(sprite, outlines);
            }
            while (worldOutlines.Count < outlines.Length) worldOutlines.Add(null);
            if (worldOutlines.Count > outlines.Length)
                worldOutlines.RemoveRange(outlines.Length, worldOutlines.Count - outlines.Length);
            for (int shape = 0; shape < outlines.Length; shape++)
            {
                Vector2[] points = outlines[shape];
                if (worldOutlines[shape] == null || worldOutlines[shape].Length != points.Length)
                    worldOutlines[shape] = new Vector2[points.Length];
                for (int i = 0; i < points.Length; i++)
                {
                    Vector2 point = points[i];
                    if (body.flipX) point.x = -point.x;
                    if (body.flipY) point.y = -point.y;
                    worldOutlines[shape][i] = body.transform.TransformPoint(point);
                }
            }
        }

        private bool TryGetColumnRange(float x, out float bottom, out float top)
        {
            bottom = float.PositiveInfinity;
            top = float.NegativeInfinity;
            foreach (Vector2[] outline in worldOutlines)
            {
                for (int i = 0, previous = outline.Length - 1; i < outline.Length; previous = i++)
                {
                    Vector2 a = outline[previous];
                    Vector2 b = outline[i];
                    // 꼭짓점을 중복 계산하지 않도록 한쪽 끝만 포함합니다.
                    if ((a.x > x) == (b.x > x)) continue;
                    float y = Mathf.LerpUnclamped(a.y, b.y, (x - a.x) / (b.x - a.x));
                    bottom = Mathf.Min(bottom, y);
                    top = Mathf.Max(top, y);
                }
            }
            return bottom < top;
        }

        private static float InverseScale(float value) => Mathf.Abs(value) < 0.001f ? 1f : 1f / value;

        private void OnValidate()
        {
            particleSizeMultiplier = Mathf.Max(0.01f, particleSizeMultiplier);
            minFallSpeed = Mathf.Max(0f, minFallSpeed);
            maxFallSpeed = Mathf.Max(minFallSpeed, maxFallSpeed);
        }

        public void Stop()
        {
            playing = false;
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
