using System.Collections.Generic;
using UnityEngine;

namespace Nytherion.GamePlay.Combat.Weapons
{
    /// <summary>방사 윤곽에서 작은 서리 조각을 내보내고 발사 종료 후에도 남은 수명 동안 표시합니다.</summary>
    public sealed class FrostStreamResidue : MonoBehaviour
    {
        private ParticleSystem particles;
        private ParticleSystemRenderer particleRenderer;
        private Material material;
        private readonly List<Sprite> shards = new List<Sprite>();
        private float emissionRemainder;
        private bool released;

        public void Initialize(Sprite source)
        {
            if (particles != null || source == null) return;
            particles = gameObject.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = particles.main;
            main.playOnAwake = false;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Shape;
            main.maxParticles = 128;
            main.startSpeed = 0f;
            main.gravityModifier = 0.025f;
            ParticleSystem.EmissionModule emission = particles.emission;
            emission.enabled = false;
            ParticleSystem.ShapeModule shape = particles.shape;
            shape.enabled = false;

            // ChillStatusSheet 첫 프레임 안의 큰 눈 결정, 작은 결정, 십자 결정을 각각 사용합니다.
            Rect rect = source.rect;
            Rect[] regions = { new Rect(6f, 25f, 6f, 6f), new Rect(15f, 18f, 4f, 4f), new Rect(28f, 13f, 10f, 10f) };
            ParticleSystem.TextureSheetAnimationModule animation = particles.textureSheetAnimation;
            animation.enabled = true;
            animation.mode = ParticleSystemAnimationMode.Sprites;
            foreach (Rect region in regions)
            {
                Sprite shard = Sprite.Create(source.texture,
                    new Rect(rect.x + region.x, rect.y + region.y, region.width, region.height),
                    new Vector2(0.5f, 0.5f), source.pixelsPerUnit, 0, SpriteMeshType.FullRect);
                shard.name = "ChillStatusSheet 눈 결정 " + shards.Count;
                shards.Add(shard);
                animation.AddSprite(shard);
            }
            animation.frameOverTime = new ParticleSystem.MinMaxCurve(0f);
            animation.startFrame = new ParticleSystem.MinMaxCurve(0f, 1f);
            ParticleSystem.SizeOverLifetimeModule size = particles.sizeOverLifetime;
            size.enabled = true;
            // 크게 튀어나온 결정이 초반에 빠르게 줄고 수명 후반에는 완전히 사라집니다.
            size.size = new ParticleSystem.MinMaxCurve(1f,
                new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.2f, 0.45f),
                    new Keyframe(0.45f, 0.12f), new Keyframe(0.7f, 0f), new Keyframe(1f, 0f)));
            ParticleSystem.ColorOverLifetimeModule color = particles.colorOverLifetime;
            color.enabled = true;
            Gradient fade = new Gradient();
            fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.85f, 0.2f), new GradientAlphaKey(0f, 0.7f) });
            color.color = fade;
            Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            material = new Material(shader);
            material.mainTexture = source.texture;
            particleRenderer = GetComponent<ParticleSystemRenderer>();
            particleRenderer.sharedMaterial = material;
            particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
            particleRenderer.alignment = ParticleSystemRenderSpace.View;
            particleRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            particleRenderer.receiveShadows = false;
            particles.Play();
        }

        public void ResetEmission() => emissionRemainder = 0f;

        public void EmitFrom(Transform source, Sprite frame, Vector2[] points, FrostStreamDeformation bend,
            float length, Vector2 direction, float spreadHalfAngle, float rate, float lifetime,
            float scale, float deltaTime, int sortingLayer, int sortingOrder)
        {
            if (released || particles == null || source == null || frame == null || points == null || points.Length == 0) return;
            particleRenderer.sortingLayerID = sortingLayer;
            particleRenderer.sortingOrder = sortingOrder;
            // 낮은 프레임 속도에서도 한 프레임에 과도한 입자가 쌓이지 않게 제한합니다.
            emissionRemainder += Mathf.Max(0f, rate) * Mathf.Min(deltaTime, 0.1f);
            int count = Mathf.Min(Mathf.FloorToInt(emissionRemainder), 16);
            emissionRemainder -= count;
            for (int i = 0; i < count; i++)
            {
                if (particles.particleCount >= 128) break;
                // 실제 불투명 픽셀 안에 구운 위치만 사용하고, 휘어진 표시와 같은 변형을 적용합니다.
                Vector2 localPoint = points[Random.Range(0, points.Length)];
                if (bend != null) localPoint = bend.TransformSpritePoint(frame, localPoint, length);
                float angle = Random.Range(-Mathf.Clamp(spreadHalfAngle, 0f, 89f), Mathf.Clamp(spreadHalfAngle, 0f, 89f));
                Vector2 travelDirection = Quaternion.Euler(0f, 0f, angle) * direction.normalized;
                ParticleSystem.EmitParams parameters = new ParticleSystem.EmitParams
                {
                    position = source.TransformPoint(localPoint),
                    velocity = travelDirection * Random.Range(1.3f, 2.2f) * scale,
                    startLifetime = Random.Range(0.55f, 1f) * Mathf.Max(0.1f, lifetime),
                    startSize = Random.Range(0.18f, 0.3f) * scale,
                    startColor = new Color(0.8f, 0.95f, 1f, 1f),
                    rotation = Random.Range(0f, 360f)
                };
                particles.Emit(parameters, 1);
            }
        }

        public void Release()
        {
            released = true;
            if (particles != null) particles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        private void Update()
        {
            if (released && (particles == null || !particles.IsAlive(true))) Destroy(gameObject);
        }

        private void OnDestroy()
        {
            if (Application.isPlaying)
            {
                foreach (Sprite shard in shards) if (shard != null) Destroy(shard);
                if (material != null) Destroy(material);
            }
            else
            {
                foreach (Sprite shard in shards) if (shard != null) DestroyImmediate(shard);
                if (material != null) DestroyImmediate(material);
            }
        }
    }
}
