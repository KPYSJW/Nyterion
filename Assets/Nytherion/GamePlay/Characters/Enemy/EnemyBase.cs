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
    public class EnemyBase : MonoBehaviour, IDamageable
    {
        
        public EnemyData enemyData;
        private float currentHealth;
        public bool isDead { get; private set; } = false;

        public RoomFirstDungeonGenerator.Room homeRoom { get; set; }
        private CurrencyDataManager currencyDataManager;
        public EnemyAIController aiController;
        private EventManager eventManager;
        private StatusEffectManager statusEffectManager;

        [Header("Hit Flash")]
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private Color hitColor = Color.red;
        [SerializeField] private float hitFlashDuration = 0.2f;
        private Color originalColor = Color.white;
        private Coroutine hitFlashCoroutine;

        [Inject]
        public void Construct(EventManager eventManager,CurrencyDataManager currencyDataManager)
        {
            this.eventManager = eventManager;
            this.currencyDataManager=currencyDataManager;
        }
        public void Initialize(EnemyData data)
        {
            enemyData = data;
            currentHealth = data.maxHealth;
            isDead = false;
            gameObject.SetActive(true);
            originalColor=spriteRenderer.color;
            aiController = GetComponent<EnemyAIController>();
            if (aiController != null)
            {
                aiController.ApplyEnemyData(data);
    
            }

            statusEffectManager = GetComponent<StatusEffectManager>();
            if (statusEffectManager == null)
            {
                statusEffectManager = gameObject.AddComponent<StatusEffectManager>();
            }
            else
            {
                statusEffectManager.ClearAllEffects();
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
            PlayerManager playerManager = UnityEngine.Object.FindObjectOfType<PlayerManager>();
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
                eventManager.TriggerEnemyDamagedByPlayerWithCrit(damageAmount, isCritical);
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

            currentHealth -= damageAmount;
            if (currentHealth <= 0) Die();
        }

        private void Die()
        {
            if (isDead) return;

            isDead = true;
            DropItems();
            aiController.agent.enabled=false;
            eventManager.TriggerEnemyDeathEvent(this);
            gameObject.SetActive(false);
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
            if (statusEffectManager != null)
            {
                return statusEffectManager.GetStatusEffectColor();
            }
            return originalColor;
        }

        private void DropItems()
        {
            
            if (Random.value <= enemyData.dropChance)
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