using UnityEngine;
using System;
using System.Collections;
using VContainer;
using Nytherion.Core.Managers;
using Nytherion.Core.Enums;

namespace Nytherion.GamePlay.Characters.Player
{
    public class PlayerHealth : MonoBehaviour
    {
        private const string HitFlashShaderName = "Custom/SpriteFlash";
        private const string DamageEffectControllerPath = "Sprites/Effect (31)1";
        private const float DamageEffectLifetime = 0.25f;
        private static readonly int FlashColorId = Shader.PropertyToID("_FlashColor");
        private static readonly int FlashAmountId = Shader.PropertyToID("_FlashAmount");
        private static Material sharedHitFlashMaterial;
        private static RuntimeAnimatorController sharedDamageEffectController;

        public static event Action<float, float> OnHealthChanged;
        public static event Action<float> OnPlayerDamaged;
        public static event Action OnPlayerDied;

        public float MaxHealth { get; private set; }
        public float CurrentHealth { get; private set; }

        public bool IsInvulnerable { get; private set; }

        [Header("Hit Flash")]
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private Color hitColor = Color.red;
        [SerializeField, Min(0f)] private float hitFlashDuration = 0.1f;
        [SerializeField, Min(0f)] private float whiteFlashDuration = 0.04f;

        [Header("Damage Effect")]
        [SerializeField] private Vector3 damageEffectScale = Vector3.one;
        [SerializeField] private int damageEffectSortingOffset = 10;

        private IProgressionManager progressionManager;
        private Coroutine hitFlashCoroutine;
        private MaterialPropertyBlock hitFlashProperties;
        private MaterialPropertyBlock originalHitProperties;
        private Material materialBeforeHitFlash;
        private Color colorBeforeHitFlash = Color.white;
        private bool isHitFlashMaterialApplied;
        private bool isFallbackColorApplied;

        private void Awake()
        {
            hitFlashProperties = new MaterialPropertyBlock();
            originalHitProperties = new MaterialPropertyBlock();

            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            }
        }

        [Inject]
        public void Construct(IProgressionManager progressionManager)
        {
            this.progressionManager = progressionManager;
        }

        public void InitializeHealth(float health)
        {
            StopHitFlash();
            MaxHealth = health;
            CurrentHealth = MaxHealth;
            IsInvulnerable = false;
            OnHealthChanged?.Invoke(CurrentHealth, MaxHealth);
        }

        public void SetInvulnerable(bool value)
        {
            IsInvulnerable = value;
        }

        public void TakeDamage(float amount)
        {
            TakeDamage(amount, transform.position);
        }

        public void TakeDamage(float amount, Vector2 hitPoint)
        {
            if (IsInvulnerable || amount <= 0f) return;

            float previousHealth = CurrentHealth;
            CurrentHealth = Mathf.Max(0, CurrentHealth - amount);
            float appliedDamage = previousHealth - CurrentHealth;
            OnHealthChanged?.Invoke(CurrentHealth, MaxHealth);

            if (appliedDamage > 0f)
            {
                GetComponent<PlayerController>()?.NotifyCombatActivity();
                PlayHitFlash();
                PlayDamageEffect(hitPoint);
                OnPlayerDamaged?.Invoke(appliedDamage);
            }

            // 받은 데미지 진척도 업데이트
            progressionManager?.ProcessAction(ProgressionType.TakeDamage, (int)appliedDamage);

            if (CurrentHealth <= 0)
            {
                Die();
            }
        }

        public void Heal(float amount)
        {
            CurrentHealth = Mathf.Min(CurrentHealth + amount, MaxHealth);
            OnHealthChanged?.Invoke(CurrentHealth, MaxHealth);
        }
        public void UpdateMaxHealth(float newMaxHealth)
        {
            if (Mathf.Approximately(MaxHealth, newMaxHealth)) return; // 무한 루프 방지

            MaxHealth = newMaxHealth;
            CurrentHealth = Mathf.Min(CurrentHealth, MaxHealth);
            OnHealthChanged?.Invoke(CurrentHealth, MaxHealth);
        }

        private void Die()
        {
            Debug.Log("플레이어 사망");
            OnPlayerDied?.Invoke();
        }

        private void PlayHitFlash()
        {
            if (spriteRenderer == null)
            {
                return;
            }

            StopHitFlash();
            hitFlashCoroutine = StartCoroutine(HitFlashRoutine());
        }

        private void PlayDamageEffect(Vector2 hitPoint)
        {
            if (sharedDamageEffectController == null)
            {
                sharedDamageEffectController =
                    Resources.Load<RuntimeAnimatorController>(DamageEffectControllerPath);
            }

            if (sharedDamageEffectController == null)
            {
                Debug.LogWarning(
                    $"[PlayerHealth] 피격 이펙트 컨트롤러를 찾을 수 없습니다: {DamageEffectControllerPath}",
                    this);
                return;
            }

            GameObject effectObject = new GameObject("Player Damage Effect");
            effectObject.layer = gameObject.layer;

            float effectZ = spriteRenderer != null
                ? spriteRenderer.transform.position.z
                : transform.position.z;
            effectObject.transform.position = new Vector3(hitPoint.x, hitPoint.y, effectZ);
            effectObject.transform.localScale = damageEffectScale;

            SpriteRenderer effectRenderer = effectObject.AddComponent<SpriteRenderer>();
            if (spriteRenderer != null)
            {
                effectRenderer.sortingLayerID = spriteRenderer.sortingLayerID;
                effectRenderer.sortingOrder =
                    spriteRenderer.sortingOrder + damageEffectSortingOffset;
            }

            Animator effectAnimator = effectObject.AddComponent<Animator>();
            effectAnimator.runtimeAnimatorController = sharedDamageEffectController;

            Destroy(effectObject, DamageEffectLifetime);
        }

        private IEnumerator HitFlashRoutine()
        {
            float totalDuration = Mathf.Max(0f, hitFlashDuration);
            float whiteDuration = Mathf.Min(
                Mathf.Max(0f, whiteFlashDuration),
                totalDuration);
            float redDuration = Mathf.Max(0f, totalDuration - whiteDuration);

            colorBeforeHitFlash = spriteRenderer.color;
            if (TryApplyHitFlashMaterial())
            {
                SetHitFlash(Color.white);
                if (whiteDuration > 0f)
                {
                    yield return new WaitForSeconds(whiteDuration);
                }

                SetHitFlash(hitColor);
                if (redDuration > 0f)
                {
                    yield return new WaitForSeconds(redDuration);
                }
            }
            else
            {
                spriteRenderer.color = hitColor;
                isFallbackColorApplied = true;
                if (totalDuration > 0f)
                {
                    yield return new WaitForSeconds(totalDuration);
                }
            }

            RestoreHitFlashVisual();
            hitFlashCoroutine = null;
        }

        private bool TryApplyHitFlashMaterial()
        {
            Shader flashShader = Shader.Find(HitFlashShaderName);
            if (flashShader == null)
            {
                return false;
            }

            if (sharedHitFlashMaterial == null ||
                sharedHitFlashMaterial.shader != flashShader)
            {
                sharedHitFlashMaterial = new Material(flashShader)
                {
                    name = "Shared Player Hit Flash",
                    hideFlags = HideFlags.HideAndDontSave
                };
            }

            materialBeforeHitFlash = spriteRenderer.sharedMaterial;
            originalHitProperties.Clear();
            spriteRenderer.GetPropertyBlock(originalHitProperties);

            hitFlashProperties.Clear();
            spriteRenderer.GetPropertyBlock(hitFlashProperties);
            spriteRenderer.sharedMaterial = sharedHitFlashMaterial;
            spriteRenderer.SetPropertyBlock(hitFlashProperties);
            isHitFlashMaterialApplied = true;
            return true;
        }

        private void SetHitFlash(Color flashColor)
        {
            hitFlashProperties.SetColor(FlashColorId, flashColor);
            hitFlashProperties.SetFloat(FlashAmountId, 1f);
            spriteRenderer.SetPropertyBlock(hitFlashProperties);
        }

        private void StopHitFlash()
        {
            if (hitFlashCoroutine != null)
            {
                StopCoroutine(hitFlashCoroutine);
                hitFlashCoroutine = null;
            }

            RestoreHitFlashVisual();
        }

        private void RestoreHitFlashVisual()
        {
            if (spriteRenderer == null)
            {
                return;
            }

            if (isHitFlashMaterialApplied)
            {
                spriteRenderer.sharedMaterial = materialBeforeHitFlash;
                spriteRenderer.SetPropertyBlock(originalHitProperties);
                materialBeforeHitFlash = null;
                isHitFlashMaterialApplied = false;
            }

            if (isFallbackColorApplied)
            {
                spriteRenderer.color = colorBeforeHitFlash;
                isFallbackColorApplied = false;
            }
        }

        private void OnDisable()
        {
            StopHitFlash();
        }
    }
}
