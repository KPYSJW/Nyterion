using System.Collections;
using Nytherion.Core.Interfaces;
using Nytherion.Core.Managers;
using Nytherion.Data.ScriptableObjects.Enemy;
using Nytherion.GamePlay.Dungeon;


using UnityEngine;
using VContainer;
using Nytherion.GamePlay.Combat;

namespace Nytherion.GamePlay.Characters.Enemy
{
    public class EnemyBase : MonoBehaviour, IGroundDamageable
    {
        
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
        [SerializeField] private float hitFlashDuration = 0.2f;
        private Color originalColor = Color.white;
        private Coroutine hitFlashCoroutine;

        [Header("지면 공격 피격 범위")]
        [Tooltip("지면 공격의 대상 위치를 구할 몸체 콜라이더입니다. 비워 두면 루트의 콜라이더를 먼저 사용합니다. 공격 범위 콜라이더는 지정하지 마세요.")]
        [SerializeField] private Collider2D groundBodyCollider;
        // 기존 원형 영역 API의 직렬화 호환용입니다. 지면 피해는 몸체 전체 크기로 판정합니다.
        [SerializeField, HideInInspector, Range(0.05f, 1f)] private float groundHitRadiusRatio = 0.5f;

        [Header("Death Pixel Dissolve")]
        [SerializeField, Min(0.01f)] private float deathDissolveDuration = 1.6f;
        [SerializeField, Min(1f)] private float deathDissolvePixelSize = 3f;

        private EnemyDeathDissolve deathDissolve;

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
            if (isDead) return;

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
            if (eventManager != null)
            {
                eventManager.TriggerEnemyDamagedByPlayerWithCrit(
                    damageAmount,
                    isCritical,
                    this,
                    isChain);
            }

            // 신성 가호 타격 회복 트리거
            if (statusEffectManager != null && statusEffectManager.HasEffect("Holy"))
            {
                statusEffectManager.TriggerHolyHeal();
            }

            // 감전 전격 체인 트리거 (체인 공격이 아닌 원래 공격일 때만 작동)
            if (!isChain && statusEffectManager != null && statusEffectManager.HasEffect("Lightning"))
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
            }

            hitFlashCoroutine = StartCoroutine(HitFlashRoutine());
        }

        private IEnumerator HitFlashRoutine()
        {
            spriteRenderer.color = hitColor;

            yield return new WaitForSeconds(hitFlashDuration);

            spriteRenderer.color = GetCurrentBaseColor();
            hitFlashCoroutine = null;
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
