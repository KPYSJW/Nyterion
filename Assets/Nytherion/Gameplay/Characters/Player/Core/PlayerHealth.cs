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
        public event Action<float> DamageTaken;
        public static event Action OnPlayerDied;

        public float MaxHealth { get; private set; }
        public float CurrentHealth { get; private set; }

        public float CurrentShield { get; private set; }
        public float MaxShield { get; private set; }
        public Sprite ShieldBarSprite { get; private set; }
        public event Action<float, float> ShieldChanged;
        public event Action<Vector2> ShieldHit;
        private UnityEngine.Object shieldSource;

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
        private Color flashColorBeforeHit;
        private float flashAmountBeforeHit;
        private Material materialBeforeHitFlash;
        private Color colorBeforeHitFlash = Color.white;
        private bool isHitFlashMaterialApplied;
        private bool isFallbackColorApplied;

        private void Awake()
        {
            hitFlashProperties = new MaterialPropertyBlock();

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
            ClearShield();
            MaxHealth = health;
            CurrentHealth = MaxHealth;
            IsInvulnerable = false;
            OnHealthChanged?.Invoke(CurrentHealth, MaxHealth);
        }

        public void SetInvulnerable(bool value)
        {
            IsInvulnerable = value;
        }

        public void ApplyShield(float amount, UnityEngine.Object source, Sprite barSprite = null)
        {
            if (CurrentHealth <= 0f || amount <= 0f || source == null) return;
            shieldSource = source;
            ShieldBarSprite = barSprite;
            CurrentShield = MaxShield = amount;
            ShieldChanged?.Invoke(CurrentShield, MaxShield);
        }

        public void ClearShield(UnityEngine.Object source = null)
        {
            if (source != null && shieldSource != source) return;
            if (CurrentShield <= 0f && MaxShield <= 0f) return;
            CurrentShield = MaxShield = 0f;
            ShieldBarSprite = null;
            shieldSource = null;
            ShieldChanged?.Invoke(0f, 0f);
        }

        public void TakeDamage(float amount)
        {
            TakeDamage(amount, transform.position);
        }

        public void TakeDamage(float amount, Vector2 hitPoint)
        {
            if (IsInvulnerable || amount <= 0f || CurrentHealth <= 0f) return;

            PlayerManager owner = GetComponent<PlayerManager>();
            float defense = owner != null && owner.currentPlayerData != null ? owner.currentPlayerData.defense : 0f;
            // 방어력 100은 피해를 절반으로 줄이며, 음수 방어력은 같은 곡선으로 피해를 늘립니다.
            amount *= defense >= 0f ? 100f / (100f + defense) : 2f - 100f / (100f - defense);

            float absorbed = Mathf.Min(CurrentShield, amount);
            if (absorbed > 0f)
            {
                CurrentShield -= absorbed;
                amount -= absorbed;
                if (CurrentShield <= 0f) { MaxShield = 0f; ShieldBarSprite = null; shieldSource = null; }
                ShieldHit?.Invoke(hitPoint);
                ShieldChanged?.Invoke(CurrentShield, MaxShield);
                GetComponent<PlayerController>()?.NotifyCombatActivity();
                if (amount <= 0f) return;
            }

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
                DamageTaken?.Invoke(appliedDamage);
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
            if (!Application.isPlaying) return;
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
            spriteRenderer.GetPropertyBlock(hitFlashProperties);
            flashColorBeforeHit = hitFlashProperties.GetColor(FlashColorId);
            flashAmountBeforeHit = hitFlashProperties.GetFloat(FlashAmountId);
            spriteRenderer.sharedMaterial = sharedHitFlashMaterial;
            isHitFlashMaterialApplied = true;
            return true;
        }

        private void SetHitFlash(Color flashColor)
        {
            // Read the current sprite properties instead of reapplying an earlier texture.
            spriteRenderer.GetPropertyBlock(hitFlashProperties);
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
                spriteRenderer.GetPropertyBlock(hitFlashProperties);
                hitFlashProperties.SetColor(FlashColorId, flashColorBeforeHit);
                hitFlashProperties.SetFloat(FlashAmountId, flashAmountBeforeHit);
                spriteRenderer.SetPropertyBlock(hitFlashProperties);
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
            ClearShield();
        }
    }
}
