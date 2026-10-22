using Nytherion.GamePlay.Characters.Player;
using UnityEngine;

namespace Nytherion.GamePlay.Combat
{
    /// <summary>정령의 인도의 충전 단계 이미지와 보석으로 모이는 입자를 표시합니다.</summary>
    [DefaultExecutionOrder(1200)]
    public class SpiritsGuidanceChargeEffect : MonoBehaviour
    {
        [SerializeField] private Sprite[] stageSprites;
        [SerializeField] private Sprite[] particleSprites;
        [Tooltip("무기 SpriteRenderer 기준 보석 중심입니다. 주황·초록·파랑 순서입니다.")]
        [SerializeField] private Vector3[] gemPositions;
        [SerializeField, Range(0f, 1f)] private float secondStageThreshold = 1f / 3f;
        [SerializeField, Range(0f, 1f)] private float thirdStageThreshold = 2f / 3f;
        [SerializeField, Min(0.01f)] private float gatherRadius = 0.28f;
        [SerializeField, Min(0.01f)] private float gatherDuration = 0.3f;
        [SerializeField, Min(0f)] private float particlesPerSecond = 18f;
        [SerializeField, Min(0.001f)] private float particleSize = 0.1f;

        private WeaponBase weapon;
        private PlayerCombat combat;
        private SpriteRenderer weaponRenderer;
        private Sprite idleSprite;
        private ParticleSystem[] particles;
        private ParticleSystemRenderer[] particleRenderers;
        private Material[] particleMaterials;
        private int activeStage;

        private void LateUpdate()
        {
            if (weapon == null)
            {
                weapon = GetComponentInParent<WeaponBase>();
                if (weapon == null) return;
                combat = weapon.GetComponentInParent<PlayerCombat>();
                weaponRenderer = weapon.GetComponent<SpriteRenderer>();
                if (weaponRenderer == null) weaponRenderer = weapon.GetComponentInChildren<SpriteRenderer>();
                if (weaponRenderer == null) return;
                idleSprite = weapon.weaponData != null ? weapon.weaponData.weaponSprite : weaponRenderer.sprite;
            }

            if (combat == null || combat.currentWeapon != weapon || !combat.IsGenericCharging)
            {
                RestoreWeapon();
                StopParticles();
                return;
            }
            ApplyCharge(combat.GenericChargePercent);
        }

        private void ApplyCharge(float chargePercent)
        {
            if (weaponRenderer == null || stageSprites == null || stageSprites.Length != 3 ||
                particleSprites == null || particleSprites.Length != 3 ||
                gemPositions == null || gemPositions.Length != 3) return;
            int stage = chargePercent >= thirdStageThreshold ? 3 : chargePercent >= secondStageThreshold ? 2 : 1;
            weaponRenderer.sprite = stageSprites[stage - 1];
            EnsureParticles();
            for (int i = 0; i < particles.Length; i++)
            {
                // 무기 그림에 맞춰 보석 위치를 추적합니다.
                particles[i].transform.position = weaponRenderer.transform.TransformPoint(gemPositions[i]);
                particleRenderers[i].sortingLayerID = weaponRenderer.sortingLayerID;
                particleRenderers[i].sortingOrder = weaponRenderer.sortingOrder + 1;
                if (i < stage && !particles[i].isPlaying) particles[i].Play();
                else if (i >= stage && i < activeStage)
                    particles[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
            activeStage = stage;
        }

        private void EnsureParticles()
        {
            if (particles != null) return;
            particles = new ParticleSystem[3];
            particleRenderers = new ParticleSystemRenderer[3];
            particleMaterials = new Material[3];
            string[] names = { "주황 보석 입자", "초록 보석 입자", "파랑 보석 입자" };
            for (int i = 0; i < particles.Length; i++)
            {
                GameObject source = new GameObject(names[i]);
                source.transform.SetParent(transform, false);
                ParticleSystem system = source.AddComponent<ParticleSystem>();
                system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                ParticleSystem.MainModule main = system.main;
                main.playOnAwake = false;
                main.loop = true;
                main.simulationSpace = ParticleSystemSimulationSpace.Local;
                main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                main.startLifetime = gatherDuration;
                main.startSpeed = -gatherRadius / gatherDuration;
                main.startSize = particleSize;
                main.startColor = Color.white;
                main.maxParticles = 32;
                ParticleSystem.EmissionModule emission = system.emission;
                emission.rateOverTime = particlesPerSecond;
                ParticleSystem.ShapeModule shape = system.shape;
                shape.enabled = true;
                shape.shapeType = ParticleSystemShapeType.Circle;
                shape.radius = gatherRadius;
                shape.radiusThickness = 0f;
                ParticleSystem.TextureSheetAnimationModule texture = system.textureSheetAnimation;
                texture.enabled = true;
                texture.mode = ParticleSystemAnimationMode.Sprites;
                texture.AddSprite(particleSprites[i]);
                texture.frameOverTime = new ParticleSystem.MinMaxCurve(0f);
                ParticleSystem.ColorOverLifetimeModule color = system.colorOverLifetime;
                color.enabled = true;
                Gradient gradient = new Gradient();
                gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                    new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f),
                        new GradientAlphaKey(1f, 0.75f), new GradientAlphaKey(0f, 1f) });
                color.color = gradient;
                ParticleSystemRenderer renderer = source.GetComponent<ParticleSystemRenderer>();
                renderer.renderMode = ParticleSystemRenderMode.Billboard;
                Material material = new Material(weaponRenderer.sharedMaterial);
                material.mainTexture = particleSprites[i].texture;
                renderer.sharedMaterial = material;
                particles[i] = system;
                particleRenderers[i] = renderer;
                particleMaterials[i] = material;
            }
        }

        private void RestoreWeapon()
        {
            if (weaponRenderer == null || stageSprites == null) return;
            foreach (Sprite sprite in stageSprites)
            {
                if (weaponRenderer.sprite != sprite) continue;
                weaponRenderer.sprite = idleSprite;
                break;
            }
        }

        private void StopParticles()
        {
            if (particles != null)
                foreach (ParticleSystem system in particles)
                    if (system != null) system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            activeStage = 0;
        }

        private void OnDisable()
        {
            RestoreWeapon();
            StopParticles();
            weapon = null;
            combat = null;
            weaponRenderer = null;
            idleSprite = null;
        }

        private void OnDestroy()
        {
            if (particleMaterials != null)
                foreach (Material material in particleMaterials)
                    if (material != null) Destroy(material);
        }
    }
}
