using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Nytherion.GamePlay.Characters.Enemy
{
    [DisallowMultipleComponent]
    public sealed class EnemyDeathDissolve : MonoBehaviour
    {
        private const string ShaderResourcePath = "EnemyPixelDissolve";
        private const string FragmentShaderResourcePath = "EnemyPixelFragment";
        private const float ReferencePixelsPerUnit = 32f;

        private static readonly int DissolveAmountId =
            Shader.PropertyToID("_DissolveAmount");
        private static readonly int MainTextureId =
            Shader.PropertyToID("_MainTex");
        private static readonly int PixelGridId =
            Shader.PropertyToID("_PixelGrid");
        private static readonly int SpriteUvRectId =
            Shader.PropertyToID("_SpriteUVRect");
        private static readonly int SeedId = Shader.PropertyToID("_Seed");

        private static Material sharedDissolveMaterial;
        private static Material sharedFragmentMaterial;

        private readonly List<ColliderState> colliderStates = new();
        private MaterialPropertyBlock dissolveProperties;
        private MaterialPropertyBlock originalProperties;

        private SpriteRenderer targetRenderer;
        private Material originalMaterial;
        private Rigidbody2D cachedRigidbody;
        private Animator cachedAnimator;
        private ParticleSystem fragmentParticles;
        private ParticleSystemRenderer fragmentRenderer;
        private Coroutine dissolveCoroutine;
        private bool hasOriginalVisualState;
        private bool hasOriginalRigidbodyState;
        private bool hasOriginalAnimatorSpeed;
        private bool originalRigidbodySimulated;
        private float originalAnimatorSpeed;
        private bool freezePose;
        private Vector3 frozenPosition;
        private Quaternion frozenRotation;

        private readonly struct ColliderState
        {
            public readonly Collider2D Collider;
            public readonly bool InitiallyEnabled;

            public ColliderState(Collider2D collider)
            {
                Collider = collider;
                InitiallyEnabled = collider.enabled;
            }
        }

        private void Awake()
        {
            dissolveProperties = new MaterialPropertyBlock();
            originalProperties = new MaterialPropertyBlock();
            cachedRigidbody = GetComponent<Rigidbody2D>();
            cachedAnimator = GetComponentInChildren<Animator>(true);
          //  CreateFragmentParticleSystem();

            Collider2D[] colliders = GetComponentsInChildren<Collider2D>(true);
            foreach (Collider2D enemyCollider in colliders)
            {
                colliderStates.Add(new ColliderState(enemyCollider));
            }
        }

        public void Play(
            SpriteRenderer spriteRenderer,
            float duration,
            float pixelSize,
            Action onComplete)
        {
            if (dissolveCoroutine != null)
            {
                StopCoroutine(dissolveCoroutine);
                dissolveCoroutine = null;
            }

            targetRenderer = spriteRenderer;
            if (targetRenderer == null || !TryGetDissolveMaterial(out Material material))
            {
                onComplete?.Invoke();
                return;
            }

            originalMaterial = targetRenderer.sharedMaterial;
            originalProperties.Clear();
            targetRenderer.GetPropertyBlock(originalProperties);
            targetRenderer.sharedMaterial = material;
            hasOriginalVisualState = true;
            FreezeAnimation();

            ConfigureProperties(Mathf.Max(1f, pixelSize));
            SetDissolveAmount(0f);
            SetCollidersEnabled(false);
            FreezeMovement();
           // PrepareFragmentParticles(duration);

            dissolveCoroutine = StartCoroutine(
                DissolveRoutine(Mathf.Max(0.01f, duration), onComplete));
        }

        public void ResetState(SpriteRenderer spriteRenderer)
        {
            if (dissolveCoroutine != null)
            {
                StopCoroutine(dissolveCoroutine);
                dissolveCoroutine = null;
            }

            if (targetRenderer != null && hasOriginalVisualState)
            {
                targetRenderer.sharedMaterial = originalMaterial;
                targetRenderer.SetPropertyBlock(originalProperties);
            }

            targetRenderer = spriteRenderer;
            originalMaterial = null;
            hasOriginalVisualState = false;
            SetCollidersToInitialState();
            RestoreMovement();
            RestoreAnimation();
            //StopFragmentParticles();
        }

        private IEnumerator DissolveRoutine(float duration, Action onComplete)
        {
            float holdDuration = Mathf.Min(0.15f, duration * 0.25f);
            float dissolveDuration = Mathf.Max(0.01f, duration - holdDuration);

            if (holdDuration > 0f)
            {
                yield return new WaitForSeconds(holdDuration);
            }

            float elapsed = 0f;
            bool particlesStarted = false;
            while (elapsed < dissolveDuration)
            {
                elapsed += Time.deltaTime;
                float normalizedTime = Mathf.Clamp01(elapsed / dissolveDuration);
                if (!particlesStarted && normalizedTime >= 0.25f)
                {
                    fragmentParticles?.Play();
                    particlesStarted = true;
                }
                SetDissolveAmount(normalizedTime);
                yield return null;
            }

            SetDissolveAmount(1f);
            dissolveCoroutine = null;
            onComplete?.Invoke();
        }

        private void ConfigureProperties(float pixelSize)
        {
            dissolveProperties.Clear();

            Sprite sprite = targetRenderer.sprite;
            Vector4 uvRect = CalculateUvRect(sprite);
            Vector2 spriteSize = sprite != null
                ? sprite.rect.size
                : new Vector2(32f, 32f);
            float spritePixelsPerUnit = sprite != null
                ? sprite.pixelsPerUnit
                : ReferencePixelsPerUnit;
            float sourcePixelsPerCell = Mathf.Max(
                1f,
                pixelSize * spritePixelsPerUnit / ReferencePixelsPerUnit);
            Vector4 pixelGrid = new Vector4(
                Mathf.Max(1f, spriteSize.x / sourcePixelsPerCell),
                Mathf.Max(1f, spriteSize.y / sourcePixelsPerCell),
                0f,
                0f);

            if (sprite != null && sprite.texture != null)
            {
                dissolveProperties.SetTexture(MainTextureId, sprite.texture);
            }
            dissolveProperties.SetVector(PixelGridId, pixelGrid);
            dissolveProperties.SetVector(SpriteUvRectId, uvRect);
            dissolveProperties.SetFloat(
                SeedId,
                UnityEngine.Random.value);
        }

        /*private void CreateFragmentParticleSystem()
        {
            GameObject particleObject = new GameObject("Death Pixel Fragments");
            particleObject.transform.SetParent(transform, false);

            fragmentParticles = particleObject.AddComponent<ParticleSystem>();
            fragmentRenderer = particleObject.GetComponent<ParticleSystemRenderer>();

            ParticleSystem.MainModule main = fragmentParticles.main;
            main.loop = false;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0f, 0f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.055f, 0.095f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 0.5f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(1f, 0.02f, 0.82f, 1f),
                new Color(0.55f, 0.005f, 0.48f, 0.9f));
            main.maxParticles = 120;

            ParticleSystem.EmissionModule emission = fragmentParticles.emission;
            emission.rateOverTime = 42f;

            ParticleSystem.ShapeModule shape = fragmentParticles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.SpriteRenderer;

            ParticleSystem.VelocityOverLifetimeModule velocity =
                fragmentParticles.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(-0.28f, 0.28f);
            velocity.y = new ParticleSystem.MinMaxCurve(0.08f, 0.48f);
            velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);

            Gradient fragmentGradient = new Gradient();
            fragmentGradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(1f, 0.04f, 0.88f), 0f),
                    new GradientColorKey(new Color(0.7f, 0.005f, 0.55f), 0.65f),
                    new GradientColorKey(new Color(0.25f, 0f, 0.2f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(0.8f, 0.55f),
                    new GradientAlphaKey(0f, 1f)
                });
            ParticleSystem.ColorOverLifetimeModule colorOverLifetime =
                fragmentParticles.colorOverLifetime;
            colorOverLifetime.enabled = true;
            colorOverLifetime.color = fragmentGradient;

            fragmentRenderer.renderMode = ParticleSystemRenderMode.Billboard;
            fragmentRenderer.sortMode = ParticleSystemSortMode.YoungestInFront;
            if (TryGetFragmentMaterial(out Material material))
            {
                fragmentRenderer.sharedMaterial = material;
            }

            StopFragmentParticles();
        }*/

        /*private void PrepareFragmentParticles(float duration)
        {
            if (fragmentParticles == null || targetRenderer == null)
                return;

            StopFragmentParticles();

            ParticleSystem.MainModule main = fragmentParticles.main;
            main.duration = Mathf.Max(0.1f, duration * 0.45f);

            ParticleSystem.ShapeModule shape = fragmentParticles.shape;
            shape.spriteRenderer = targetRenderer;
            shape.textureClipChannel = ParticleSystemShapeTextureChannel.Alpha;
            shape.textureClipThreshold = 0.05f;

            if (fragmentRenderer != null)
            {
                fragmentRenderer.sortingLayerID = targetRenderer.sortingLayerID;
                fragmentRenderer.sortingOrder = targetRenderer.sortingOrder + 1;
            }
        }*/

        /*private void StopFragmentParticles()
        {
            if (fragmentParticles != null)
            {
                fragmentParticles.Stop(
                    true,
                    ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }*/

        private void FreezeMovement()
        {
            frozenPosition = transform.position;
            frozenRotation = transform.rotation;
            freezePose = true;

            if (cachedRigidbody == null)
                return;

            originalRigidbodySimulated = cachedRigidbody.simulated;
            hasOriginalRigidbodyState = true;
            cachedRigidbody.velocity = Vector2.zero;
            cachedRigidbody.angularVelocity = 0f;
            cachedRigidbody.simulated = false;
        }

        private void RestoreMovement()
        {
            freezePose = false;

            if (cachedRigidbody != null && hasOriginalRigidbodyState)
            {
                cachedRigidbody.simulated = originalRigidbodySimulated;
                cachedRigidbody.velocity = Vector2.zero;
                cachedRigidbody.angularVelocity = 0f;
            }

            hasOriginalRigidbodyState = false;
        }

        private void FreezeAnimation()
        {
            if (cachedAnimator == null || hasOriginalAnimatorSpeed)
                return;

            originalAnimatorSpeed = cachedAnimator.speed;
            hasOriginalAnimatorSpeed = true;
            cachedAnimator.speed = 0f;
        }

        private void RestoreAnimation()
        {
            if (cachedAnimator != null && hasOriginalAnimatorSpeed)
            {
                cachedAnimator.speed = originalAnimatorSpeed;
            }

            hasOriginalAnimatorSpeed = false;
        }

        private void LateUpdate()
        {
            if (freezePose)
            {
                transform.SetPositionAndRotation(frozenPosition, frozenRotation);
            }
        }

        private static Vector4 CalculateUvRect(Sprite sprite)
        {
            if (sprite == null || sprite.uv == null || sprite.uv.Length == 0)
                return new Vector4(0f, 0f, 1f, 1f);

            Vector2 minimum = sprite.uv[0];
            Vector2 maximum = sprite.uv[0];
            foreach (Vector2 uv in sprite.uv)
            {
                minimum = Vector2.Min(minimum, uv);
                maximum = Vector2.Max(maximum, uv);
            }

            Vector2 size = maximum - minimum;
            return new Vector4(minimum.x, minimum.y, size.x, size.y);
        }

        private void SetDissolveAmount(float amount)
        {
            if (targetRenderer == null)
                return;

            dissolveProperties.SetFloat(DissolveAmountId, amount);
            targetRenderer.SetPropertyBlock(dissolveProperties);
        }

        private void SetCollidersEnabled(bool enabled)
        {
            foreach (ColliderState state in colliderStates)
            {
                if (state.Collider != null)
                {
                    state.Collider.enabled = enabled;
                }
            }
        }

        private void SetCollidersToInitialState()
        {
            foreach (ColliderState state in colliderStates)
            {
                if (state.Collider != null)
                {
                    state.Collider.enabled = state.InitiallyEnabled;
                }
            }
        }

        private static bool TryGetDissolveMaterial(out Material material)
        {
            if (sharedDissolveMaterial == null)
            {
                Shader dissolveShader = Resources.Load<Shader>(ShaderResourcePath);
                if (dissolveShader != null)
                {
                    sharedDissolveMaterial = new Material(dissolveShader)
                    {
                        name = "Enemy Pixel Dissolve (Runtime)",
                        hideFlags = HideFlags.HideAndDontSave
                    };
                }
            }

            material = sharedDissolveMaterial;
            return material != null;
        }

        private static bool TryGetFragmentMaterial(out Material material)
        {
            if (sharedFragmentMaterial == null)
            {
                Shader fragmentShader = Resources.Load<Shader>(
                    FragmentShaderResourcePath);
                if (fragmentShader != null)
                {
                    sharedFragmentMaterial = new Material(fragmentShader)
                    {
                        name = "Enemy Pixel Fragments (Runtime)",
                        hideFlags = HideFlags.HideAndDontSave
                    };
                }
            }

            material = sharedFragmentMaterial;
            return material != null;
        }

        private void OnDisable()
        {
            ResetState(targetRenderer);
        }
    }
}
