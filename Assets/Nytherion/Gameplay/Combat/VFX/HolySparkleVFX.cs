using UnityEngine;

namespace Nytherion.GamePlay.Combat
{
    /// <summary>신성 상태인 적 주변에 제자리에서 반짝이는 입자를 표시합니다.</summary>
    [DefaultExecutionOrder(1100)]
    public sealed class HolySparkleVFX : MonoBehaviour
    {
        [SerializeField] private Texture2D yellowSheet;
        [SerializeField] private Texture2D whiteSheet;
        [SerializeField] private Material particleMaterial;
        [SerializeField, Min(0f), InspectorName("초당 반짝임 수")]
        private float emissionPerSecond = 12f;
        [SerializeField, Min(0.01f), InspectorName("반짝임 수명 (초)")]
        private float particleLifetime = 0.6f;
        [SerializeField, Min(0f), InspectorName("몸체 대비 생성 범위")]
        private float areaMultiplier = 1.5f;
        [SerializeField, Min(0f), InspectorName("몸체 대비 입자 크기")]
        private float particleSizeMultiplier = 0.5f;

        private readonly ParticleSystem[] systems = new ParticleSystem[2];
        private readonly ParticleSystemRenderer[] renderers = new ParticleSystemRenderer[2];
        private readonly Material[] materials = new Material[2];
        private SpriteRenderer body;

        public void Play(SpriteRenderer source)
        {
            if (source == null || source.sprite == null || particleMaterial == null ||
                yellowSheet == null || whiteSheet == null) return;
            body = source;
            EnsureParticles();
            gameObject.SetActive(true);
            enabled = true;
            UpdatePlacement();
            foreach (ParticleSystem system in systems) system.Play();
        }

        private void EnsureParticles()
        {
            if (systems[0] != null) return;
            Texture2D[] sheets = { yellowSheet, whiteSheet };
            for (int i = 0; i < systems.Length; i++)
            {
                GameObject child = new GameObject(i == 0 ? "노란 신성 반짝임" : "흰 신성 반짝임");
                child.transform.SetParent(transform, false);
                ParticleSystem system = child.AddComponent<ParticleSystem>();
                systems[i] = system;
                system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

                ParticleSystem.MainModule main = system.main;
                main.playOnAwake = false;
                main.loop = true;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.scalingMode = ParticleSystemScalingMode.Shape;
                main.startSpeed = 0f;
                main.gravityModifier = 0f;
                main.startLifetime = Mathf.Max(0.01f, particleLifetime);
                main.maxParticles = 48;

                ParticleSystem.EmissionModule emission = system.emission;
                emission.rateOverTime = Mathf.Max(0f, emissionPerSecond) * 0.5f;
                ParticleSystem.ShapeModule shape = system.shape;
                shape.enabled = true;
                shape.shapeType = ParticleSystemShapeType.Box;
                ParticleSystem.TextureSheetAnimationModule animation = system.textureSheetAnimation;
                animation.enabled = true;
                animation.mode = ParticleSystemAnimationMode.Grid;
                animation.numTilesX = 6;
                animation.numTilesY = 1;
                animation.animation = ParticleSystemAnimationType.WholeSheet;
                animation.frameOverTime = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0f, 1f, 1f));
                animation.startFrame = 0f;
                animation.cycleCount = 1;

                ParticleSystemRenderer particleRenderer = child.GetComponent<ParticleSystemRenderer>();
                renderers[i] = particleRenderer;
                particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
                particleRenderer.alignment = ParticleSystemRenderSpace.View;
                materials[i] = new Material(particleMaterial);
                materials[i].mainTexture = sheets[i];
                particleRenderer.sharedMaterial = materials[i];
            }
        }

        private void LateUpdate()
        {
            if (body == null || !body.gameObject.activeInHierarchy)
            {
                Stop();
                return;
            }
            UpdatePlacement();
        }

        private void UpdatePlacement()
        {
            bool visible = body.sprite != null && body.enabled;
            for (int i = 0; i < systems.Length; i++)
            {
                renderers[i].enabled = visible;
                ParticleSystem.EmissionModule emission = systems[i].emission;
                emission.enabled = visible;
            }
            if (!visible) return;

            Bounds bounds = GetVisibleWorldBounds(body);
            transform.position = bounds.center;
            transform.rotation = Quaternion.identity;
            Vector3 parentScale = transform.parent != null ? transform.parent.lossyScale : Vector3.one;
            transform.localScale = new Vector3(1f / SafeScale(parentScale.x),
                1f / SafeScale(parentScale.y), 1f / SafeScale(parentScale.z));
            float size = Mathf.Max(bounds.size.x, bounds.size.y) * Mathf.Max(0f, particleSizeMultiplier);
            for (int i = 0; i < systems.Length; i++)
            {
                ParticleSystem.ShapeModule shape = systems[i].shape;
                shape.scale = new Vector3(bounds.size.x * areaMultiplier, bounds.size.y * areaMultiplier, 0f);
                ParticleSystem.MainModule main = systems[i].main;
                main.startSize = new ParticleSystem.MinMaxCurve(size * 0.75f, size * 1.25f);
                renderers[i].sortingLayerID = body.sortingLayerID;
                renderers[i].sortingOrder = body.sortingOrder + 1;
            }
        }

        private static Bounds GetVisibleWorldBounds(SpriteRenderer renderer)
        {
            Bounds local = HolyVisibleSpriteBounds.GetLocalBounds(renderer.sprite);
            Vector3 center = local.center;
            if (renderer.flipX) center.x = -center.x;
            if (renderer.flipY) center.y = -center.y;
            Bounds world = new Bounds(renderer.transform.TransformPoint(center), Vector3.zero);
            for (int i = 0; i < 4; i++)
            {
                Vector3 corner = center + new Vector3(
                    (i % 2 == 0 ? -1f : 1f) * local.extents.x,
                    (i < 2 ? -1f : 1f) * local.extents.y, 0f);
                world.Encapsulate(renderer.transform.TransformPoint(corner));
            }
            return world;
        }

        private static float SafeScale(float value) => Mathf.Abs(value) < 0.001f ? 0.001f : value;

        public void Stop()
        {
            body = null;
            foreach (ParticleSystem system in systems)
                if (system != null) system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            enabled = false;
        }

        private void OnDisable() => Stop();

        private void OnDestroy()
        {
            foreach (Material material in materials)
                if (material != null) Destroy(material);
        }
    }
}
