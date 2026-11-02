using System.Collections.Generic;
using UnityEngine;

namespace Nytherion.GamePlay.Combat
{
    /// <summary>화상 지속 중 몸체 주변에 작은 불꽃을 반복해서 표시합니다.</summary>
    [DefaultExecutionOrder(1100)]
    public sealed class BurnTickVFX : MonoBehaviour
    {
        [SerializeField] private Sprite[] frames;
        [SerializeField, Min(0f), InspectorName("초당 불꽃 수")]
        [Tooltip("몬스터 한 마리에 초당 생성할 불꽃 수입니다.")]
        private float emissionPerSecond = 6f;
        [SerializeField, Min(1), InspectorName("최대 불꽃 수")]
        [Tooltip("몬스터 한 마리에 동시에 표시할 수 있는 불꽃의 최대 개수입니다.")]
        private int maxParticles = 24;
        [SerializeField, Min(0f), InspectorName("최소 상승 속도")]
        private float minRiseSpeed = 0.45f;
        [SerializeField, Min(0f), InspectorName("최대 상승 속도")]
        private float maxRiseSpeed = 0.8f;
        [SerializeField, Min(0.01f), InspectorName("최소 수명 (초)")]
        private float minLifetime = 0.2f;
        [SerializeField, Min(0.01f), InspectorName("최대 수명 (초)")]
        private float maxLifetime = 0.4f;

        private SpriteRenderer body;
        private ParticleSystem particles;
        private ParticleSystemRenderer particleRenderer;
        private Material particleMaterial;
        private readonly SpriteBodyBounds bodyBounds = new SpriteBodyBounds();
        private readonly List<List<Vector2>> outlines = new List<List<Vector2>>();
        private Sprite cachedSprite;
        private Bounds localBounds;
        private float emissionTimer;
        private float appliedMinRiseSpeed = -1f;
        private float appliedMaxRiseSpeed = -1f;
        private float appliedMinLifetime = -1f;
        private float appliedMaxLifetime = -1f;
        private int appliedMaxParticles;

        public void Play(SpriteRenderer source)
        {
            if (source == null || source.sprite == null || frames == null || frames.Length == 0) return;
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
            GameObject child = new GameObject("화상 불꽃 입자");
            child.transform.SetParent(transform, false);
            particles = child.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = particles.main;
            main.playOnAwake = false;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.45f, 0.85f);
            ParticleSystem.EmissionModule emission = particles.emission;
            emission.enabled = false;
            ParticleSystem.ShapeModule shape = particles.shape;
            shape.enabled = false;
            ParticleSystem.VelocityOverLifetimeModule velocity = particles.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.x = new ParticleSystem.MinMaxCurve(-0.08f, 0.08f);
            velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);
            ApplyParticleSettings();
            ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = particles.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f,
                new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.65f, 0.8f), new Keyframe(1f, 0.15f)));
            ParticleSystem.ColorOverLifetimeModule colorOverLifetime = particles.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient fade = new Gradient();
            fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });
            colorOverLifetime.color = fade;
            ParticleSystem.TextureSheetAnimationModule animation = particles.textureSheetAnimation;
            animation.enabled = true;
            animation.mode = ParticleSystemAnimationMode.Sprites;
            foreach (Sprite frame in frames) if (frame != null) animation.AddSprite(frame);
            particleRenderer = child.GetComponent<ParticleSystemRenderer>();
            particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
            particleRenderer.alignment = ParticleSystemRenderSpace.View;
            particleMaterial = new Material(body.sharedMaterial);
            particleMaterial.mainTexture = frames[0].texture;
            particleRenderer.sharedMaterial = particleMaterial;
        }

        private void LateUpdate()
        {
            if (body == null || body.sprite == null || !body.enabled || !body.gameObject.activeInHierarchy)
            {
                Stop();
                return;
            }
            UpdatePlacement();
            ApplyParticleSettings();
            if (emissionPerSecond <= 0f) return;
            emissionTimer += Time.deltaTime;
            float interval = 1f / emissionPerSecond;
            while (emissionTimer >= interval)
            {
                emissionTimer -= interval;
                if (particles.particleCount >= maxParticles || !TryGetBodyPoint(out Vector2 point)) continue;
                if (body.flipX) point.x = -point.x;
                if (body.flipY) point.y = -point.y;
                Vector3 worldPoint = body.transform.TransformPoint(point);
                ParticleSystem.EmitParams parameters = new ParticleSystem.EmitParams();
                parameters.position = particles.transform.InverseTransformPoint(worldPoint);
                particles.Emit(parameters, 1);
            }
        }

        private void ApplyParticleSettings()
        {
            float minimumSpeed = Mathf.Max(0f, minRiseSpeed);
            float maximumSpeed = Mathf.Max(minimumSpeed, maxRiseSpeed);
            float minimumLifetime = Mathf.Max(0.01f, minLifetime);
            float maximumLifetime = Mathf.Max(minimumLifetime, maxLifetime);
            int maximumParticles = Mathf.Max(1, maxParticles);
            if (appliedMinRiseSpeed == minimumSpeed && appliedMaxRiseSpeed == maximumSpeed &&
                appliedMinLifetime == minimumLifetime && appliedMaxLifetime == maximumLifetime &&
                appliedMaxParticles == maximumParticles) return;

            ParticleSystem.MainModule main = particles.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(minimumLifetime, maximumLifetime);
            main.maxParticles = maximumParticles;
            ParticleSystem.VelocityOverLifetimeModule velocity = particles.velocityOverLifetime;
            velocity.y = new ParticleSystem.MinMaxCurve(minimumSpeed, maximumSpeed);
            appliedMinRiseSpeed = minimumSpeed;
            appliedMaxRiseSpeed = maximumSpeed;
            appliedMinLifetime = minimumLifetime;
            appliedMaxLifetime = maximumLifetime;
            appliedMaxParticles = maximumParticles;
        }

        private void OnValidate()
        {
            emissionPerSecond = Mathf.Max(0f, emissionPerSecond);
            maxParticles = Mathf.Max(1, maxParticles);
            minRiseSpeed = Mathf.Max(0f, minRiseSpeed);
            maxRiseSpeed = Mathf.Max(minRiseSpeed, maxRiseSpeed);
            minLifetime = Mathf.Max(0.01f, minLifetime);
            maxLifetime = Mathf.Max(minLifetime, maxLifetime);
        }

        private void UpdatePlacement()
        {
            Bounds bounds = bodyBounds.GetBounds(body);
            Transform emitter = particles.transform;
            float size = Mathf.Clamp(bounds.size.y, 0.6f, 1.8f);
            emitter.position = bounds.center;
            emitter.rotation = Quaternion.identity;
            Vector3 parentScale = emitter.parent.lossyScale;
            emitter.localScale = new Vector3(InverseScale(parentScale.x), InverseScale(parentScale.y), InverseScale(parentScale.z));
            ParticleSystem.MainModule main = particles.main;
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.45f, size * 0.85f);
            particleRenderer.sortingLayerID = body.sortingLayerID;
            particleRenderer.sortingOrder = body.sortingOrder + 1;
        }

        private bool TryGetBodyPoint(out Vector2 point)
        {
            Sprite sprite = body.sprite;
            if (cachedSprite != sprite) CacheOutlines(sprite);
            point = default;
            if (outlines.Count == 0) return false;

            // 발 아래에 입자가 걸리지 않도록 몸체의 아래쪽 일부는 제외합니다.
            float minY = body.flipY ? localBounds.min.y : Mathf.Lerp(localBounds.min.y, localBounds.max.y, 0.18f);
            float maxY = body.flipY ? Mathf.Lerp(localBounds.min.y, localBounds.max.y, 0.82f) : localBounds.max.y;
            for (int attempt = 0; attempt < 32; attempt++)
            {
                // 대부분 아래쪽에서 시작하되 머리 쪽도 가끔 선택합니다.
                float height = Random.value;
                if (Random.value < 0.75f) height = Mathf.Min(height, Random.value);
                if (body.flipY) height = 1f - height;
                Vector2 candidate = new Vector2(Random.Range(localBounds.min.x, localBounds.max.x), Mathf.Lerp(minY, maxY, height));
                foreach (List<Vector2> outline in outlines)
                {
                    if (!ContainsPoint(outline, candidate)) continue;
                    point = candidate;
                    return true;
                }
            }
            return false;
        }

        private void CacheOutlines(Sprite sprite)
        {
            cachedSprite = sprite;
            outlines.Clear();
            bool hasPoint = false;
            localBounds = default;
            for (int i = 0; i < sprite.GetPhysicsShapeCount(); i++)
            {
                List<Vector2> outline = new List<Vector2>();
                sprite.GetPhysicsShape(i, outline);
                if (outline.Count < 3) continue;
                outlines.Add(outline);
                foreach (Vector2 vertex in outline)
                {
                    if (hasPoint) localBounds.Encapsulate(vertex);
                    else { localBounds = new Bounds(vertex, Vector3.zero); hasPoint = true; }
                }
            }
        }

        private static bool ContainsPoint(List<Vector2> outline, Vector2 point)
        {
            bool inside = false;
            for (int i = 0, previous = outline.Count - 1; i < outline.Count; previous = i++)
            {
                Vector2 a = outline[i];
                Vector2 b = outline[previous];
                if ((a.y > point.y) != (b.y > point.y) &&
                    point.x < (b.x - a.x) * (point.y - a.y) / (b.y - a.y) + a.x)
                    inside = !inside;
            }
            return inside;
        }

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
