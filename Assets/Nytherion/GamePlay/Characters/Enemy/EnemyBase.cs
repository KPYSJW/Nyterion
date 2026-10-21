using System.Collections;
using System.Collections.Generic;
using Nytherion.Core.Enums;
using Nytherion.Core.Interfaces;
using Nytherion.Core.Managers;
using Nytherion.Data.ScriptableObjects.Enemy;
using Nytherion.GamePlay.Dungeon;


using UnityEngine;
using VContainer;
using Nytherion.GamePlay.Combat;
using Nytherion.Gameplay.Relics.Modules;

namespace Nytherion.GamePlay.Characters.Enemy
{
    public class EnemyBase : MonoBehaviour, IGroundDamageable
    {
        private const string HitFlashShaderName = "Custom/SpriteFlash";
        private static readonly int FlashColorId = Shader.PropertyToID("_FlashColor");
        private static readonly int FlashAmountId = Shader.PropertyToID("_FlashAmount");
        private static Material sharedHitFlashMaterial;
        
        public EnemyData enemyData;
        private float currentHealth;
        public bool isDead { get; private set; } = false;
        protected virtual bool HasUnlimitedHealth => false;

        public RoomFirstDungeonGenerator.Room homeRoom { get; set; }
        private CurrencyDataManager currencyDataManager;
        public EnemyAIController aiController;
        private EventManager eventManager;
        private StatusEffectManager statusEffectManager;
        private PlayerManager playerManager;
        private string poolTag;

        [Header("Hit Flash")]
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private Color hitColor = Color.red;
        [SerializeField] private float hitFlashDuration = 0.08f;
        [SerializeField, Min(0f)] private float whiteFlashDuration = 0.04f;
        private Color originalColor = Color.white;
        private Coroutine hitFlashCoroutine;
        private MaterialPropertyBlock hitFlashProperties;
        private MaterialPropertyBlock originalHitProperties;
        private Material materialBeforeHitFlash;
        private bool isHitFlashMaterialApplied;

        [Header("지면 공격 피격 범위")]
        [Tooltip("지면 공격의 대상 위치를 구할 몸체 콜라이더입니다. 비워 두면 루트의 콜라이더를 먼저 사용합니다. 공격 범위 콜라이더는 지정하지 마세요.")]
        [SerializeField] private Collider2D groundBodyCollider;
        // 기존 원형 영역 API의 직렬화 호환용입니다. 지면 피해는 몸체 전체 크기로 판정합니다.
        [SerializeField, HideInInspector, Range(0.05f, 1f)] private float groundHitRadiusRatio = 0.5f;

        [Header("Death Pixel Dissolve")]
        [SerializeField, Min(0.01f)] private float deathDissolveDuration = 1.6f;
        [SerializeField, Min(1f)] private float deathDissolvePixelSize = 3f;

        private EnemyDeathDissolve deathDissolve;

        public Vector3 GetFootPosition()
        {
            // 공격 범위 콜라이더가 아닌 본체 스프라이트 하단을 발 위치로 사용합니다.
            if (spriteRenderer != null && spriteRenderer.sprite != null)
            {
                Bounds bounds = spriteRenderer.bounds;
                return new Vector3(bounds.center.x, bounds.min.y, transform.position.z);
            }
            if (TryGetGroundHitBounds(out Bounds bodyBounds))
                return new Vector3(bodyBounds.center.x, bodyBounds.min.y, transform.position.z);
            return transform.position;
        }

        public bool TryGetGroundHitBounds(out Bounds bounds)
        {
            if (groundBodyCollider == null)
            {
                groundBodyCollider = GetComponent<Collider2D>();
                if (groundBodyCollider == null && spriteRenderer != null)
                    groundBodyCollider = spriteRenderer.GetComponent<Collider2D>();
            }
            bounds = default;
            if (isDead || groundBodyCollider == null || !groundBodyCollider.enabled ||
                !groundBodyCollider.gameObject.activeInHierarchy)
                return false;

            bounds = groundBodyCollider.bounds;
            return bounds.size.x > 0f && bounds.size.y > 0f;
        }

        public bool TryGetGroundHitCircle(out Vector2 center, out float radius)
        {
            center = transform.position;
            radius = 0f;
            if (!TryGetGroundHitBounds(out Bounds bounds)) return false;
            radius = Mathf.Min(bounds.extents.x, bounds.extents.y) * Mathf.Clamp(groundHitRadiusRatio, 0.05f, 1f);
            // 시전·조준에 사용하는 평면 좌표와 맞춥니다. 아래쪽으로 옮기면 표시 타원 안에서도 적이 빗나갑니다.
            center = bounds.center;
            return radius > 0f;
        }

        private void Awake()
        {
            hitFlashProperties = new MaterialPropertyBlock();
            originalHitProperties = new MaterialPropertyBlock();

            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            }
            if (spriteRenderer != null)
            {
                originalColor = spriteRenderer.color;
            }

            deathDissolve = GetComponent<EnemyDeathDissolve>();
            if (deathDissolve == null)
            {
                deathDissolve = gameObject.AddComponent<EnemyDeathDissolve>();
            }

            statusEffectManager = GetComponent<StatusEffectManager>();
            if (statusEffectManager == null)
            {
                statusEffectManager = gameObject.AddComponent<StatusEffectManager>();
            }
        }

        [Inject]
        public void Construct(EventManager eventManager, CurrencyDataManager currencyDataManager, PlayerManager playerManager)
        {
            this.eventManager = eventManager;
            this.currencyDataManager = currencyDataManager;
            this.playerManager = playerManager;
        }
        public void Initialize(EnemyData data)
        {
            if (data == null) return;

            enemyData = data;
            poolTag = data.enemyName;
            currentHealth = data.maxHealth;
            isDead = false;
            homeRoom = null;
            gameObject.SetActive(true);
            deathDissolve?.ResetState(spriteRenderer);

            if (hitFlashCoroutine != null)
            {
                StopCoroutine(hitFlashCoroutine);
                hitFlashCoroutine = null;
            }
            RestoreHitFlashMaterial();
            if (spriteRenderer != null)
            {
                spriteRenderer.color = originalColor;
            }

            aiController = GetComponent<EnemyAIController>();
            if (aiController != null)
            {
                aiController.ResetForReuse(data);
            }

            if (statusEffectManager != null)
            {
                statusEffectManager.ClearAllEffects();
                statusEffectManager.ConfigureCombatContext(playerManager);
            }
            UpdateStatusColor();
        }

        public void TakeDamage(float damageAmount, bool isChain = false)
        {
            ApplyDamage(damageAmount, isChain, false);
        }

        public void TakeDamage(float damageAmount, bool isChain, IReadOnlyList<EquipmentTrait> attackTraits)
        {
            ApplyDamage(damageAmount, isChain, false, attackTraits);
        }

        public void TakeDamageOverTime(float damageAmount)
        {
            ApplyDamage(damageAmount, false, true);
        }

        private void ApplyDamage(float damageAmount, bool isChain, bool isDamageOverTime,
            IReadOnlyList<EquipmentTrait> attackTraits = null)
        {
            if (isDead) return;

            // 같은 적중 안에서 합산하므로 치명타와 적중 효과를 다시 발동시키지 않습니다.
            // 지속 피해와 유물·번개의 연쇄 피해는 새 주사위 추첨에서 제외합니다.
            if (!isDamageOverTime && !isChain && damageAmount > 0f && playerManager != null &&
                playerManager.TryGetComponent(out PipCompletionRuntime diceBonus))
            {
                damageAmount *= diceBonus.RollDamageMultiplier();
            }

            // StatusEffectManager를 통한 데미지 배율 적용
            if (statusEffectManager != null)
            {
                damageAmount *= statusEffectManager.GetReceivedDamageMultiplier();
            }

            bool isCritical = false;
            if (playerManager != null && playerManager.currentPlayerData != null)
            {
                float chance = playerManager.currentPlayerData.critChance;
                if (UnityEngine.Random.value <= chance)
                {
                    isCritical = true;
                    float multiplier = playerManager.currentPlayerData.critDamageMultiplier;
                    if (statusEffectManager != null)
                    {
                        multiplier += statusEffectManager.GetCritDamageMultiplierModifier();
                    }
                    damageAmount *= multiplier;
                }
            }

            PlayHitFlash();
            // 이번 적중 이벤트에서 새로 부여한 감전은 같은 타격을 다시 확산시키지 않습니다.
            bool hadLightningBeforeHit = !isChain && statusEffectManager != null && statusEffectManager.HasEffect("Lightning");
            if (eventManager != null)
            {
                eventManager.TriggerEnemyDamagedByPlayerWithCrit(
                    damageAmount,
                    isCritical,
                    this,
                    isChain,
                    isDamageOverTime,
                    attackTraits);
            }

            // 신성 가호 타격 회복 트리거
            if (statusEffectManager != null && statusEffectManager.HasEffect("Holy"))
            {
                statusEffectManager.TriggerHolyHeal();
            }

            // 감전 전격 체인 트리거 (체인 공격이 아닌 원래 공격일 때만 작동)
            if (hadLightningBeforeHit && statusEffectManager != null && statusEffectManager.HasEffect("Lightning"))
            {
                statusEffectManager.TriggerLightningChain(damageAmount);
            }

            // 허수아비도 피격 효과와 전투 이벤트는 처리하되 체력은 소모하지 않습니다.
            if (!HasUnlimitedHealth)
            {
                currentHealth -= damageAmount;
                if (currentHealth <= 0) Die();
            }
        }

        private void Die()
        {
            if (isDead) return;

            isDead = true;
            DropItems();

            if (aiController != null)
            {
                aiController.PrepareForPoolReturn();
            }

            RoomFirstDungeonGenerator.Room deathRoom = homeRoom;
            if (deathRoom != null)
            {
                deathRoom.enemies.Remove(this);
            }

            eventManager?.TriggerEnemyDeathEvent(this);

            if (hitFlashCoroutine != null)
            {
                StopCoroutine(hitFlashCoroutine);
                hitFlashCoroutine = null;
            }
            RestoreHitFlashMaterial();
            if (spriteRenderer != null)
            {
                spriteRenderer.color = originalColor;
            }

            if (deathDissolve != null)
            {
                deathDissolve.Play(
                    spriteRenderer,
                    deathDissolveDuration,
                    deathDissolvePixelSize,
                    ReturnToPool);
            }
            else
            {
                ReturnToPool();
            }
        }

        private void ReturnToPool()
        {
            if (ObjectPoolManager.Instance != null && !string.IsNullOrEmpty(poolTag))
            {
                ObjectPoolManager.Instance.ReturnToPool(poolTag, gameObject);
            }
            else
            {
                gameObject.SetActive(false);
            }
        }
        private void PlayHitFlash()
        {
            if (spriteRenderer == null) return;

            if (hitFlashCoroutine != null)
            {
                StopCoroutine(hitFlashCoroutine);
                RestoreHitFlashMaterial();
            }

            hitFlashCoroutine = StartCoroutine(HitFlashRoutine());
        }

        private IEnumerator HitFlashRoutine()
        {
            float totalDuration = Mathf.Max(0f, hitFlashDuration);
            float whiteDuration = Mathf.Min(
                Mathf.Max(0f, whiteFlashDuration),
                totalDuration);
            float redDuration = Mathf.Max(0f, totalDuration - whiteDuration);

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

                RestoreHitFlashMaterial();
            }
            else
            {
                // 셰이더를 찾지 못한 경우에도 기존 빨간 피격 표시는 유지합니다.
                spriteRenderer.color = hitColor;
                if (totalDuration > 0f)
                {
                    yield return new WaitForSeconds(totalDuration);
                }
            }

            spriteRenderer.color = GetCurrentBaseColor();
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
                    name = "Shared Enemy Hit Flash",
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

        private void RestoreHitFlashMaterial()
        {
            if (!isHitFlashMaterialApplied || spriteRenderer == null)
            {
                return;
            }

            spriteRenderer.sharedMaterial = materialBeforeHitFlash;
            spriteRenderer.SetPropertyBlock(originalHitProperties);
            materialBeforeHitFlash = null;
            isHitFlashMaterialApplied = false;
        }

        public void UpdateStatusColor()
        {
            if (spriteRenderer == null) return;

            if (hitFlashCoroutine == null)
            {
                spriteRenderer.color = GetCurrentBaseColor();
            }
        }

        private Color GetCurrentBaseColor()
        {
            return originalColor;
        }

        private void DropItems()
        {
            if (enemyData != null && currencyDataManager != null && Random.value <= enemyData.dropChance)
            {
                currencyDataManager.AddCurrency(Core.Enums.CurrencyType.Gold,10);
            }

        }
       /* private void OnCollisionEnter2D(Collision2D collision)
        {
            Debug.Log(collision.gameObject.tag);
            if (collision.gameObject.CompareTag(Tags.Player)||collision.gameObject.CompareTag(Tags.Weapon))
            {
                Debug.Log($"{enemyData.enemyName}이(가) 플레이어와 충돌하여 즉시 사망합니다.");
                Die();
            }
        }*/

        /*private void OnTriggerEnter2D(Collider2D other) {
            if (other.gameObject.CompareTag(Tags.Weapon))
            {
                Die();
            }
        }*/
    }
}
