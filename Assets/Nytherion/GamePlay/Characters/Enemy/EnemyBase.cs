using System.Collections;
using Nytherion.Core.Interfaces;
using Nytherion.Core.Managers;
using Nytherion.Core.Systems;
using Nytherion.Data.ScriptableObjects.Enemy;
using Nytherion.GamePlay.Dungeon;
using Nytherion.GamePlay.Items;


using UnityEngine;
using VContainer;

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
        }

        public void TakeDamage(float damageAmount)
        {
            if (isDead) return;

            bool isCritical = false;
            PlayerManager playerManager = UnityEngine.Object.FindObjectOfType<PlayerManager>();
            if (playerManager != null && playerManager.currentPlayerData != null)
            {
                float chance = playerManager.currentPlayerData.critChance;
                if (UnityEngine.Random.value <= chance)
                {
                    isCritical = true;
                    float multiplier = playerManager.currentPlayerData.critDamageMultiplier;
                    damageAmount *= multiplier;
                    Debug.Log($"[Critical Hit] Damage scaled to {damageAmount} (crit chance: {chance})");
                }
            }

            PlayHitFlash();
            if (eventManager != null)
            {
                eventManager.TriggerEnemyDamagedByPlayerWithCrit(damageAmount, isCritical);
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

            spriteRenderer.color = originalColor;
            hitFlashCoroutine = null;
        }
        private void DropItems()
        {
            
            if (Random.value <= enemyData.dropChance)
            {
                Debug.Log($"골드 드랍: {enemyData.goldDropAmount}G ");
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