using Nytherion.Core.Managers;
using UnityEngine;
using Nytherion.Core.Interfaces;
using VContainer;

public class BlackholeProjectile : MonoBehaviour
{
    private float damage;
    private float range; 
    private float pullForce;
    private float duration;
    private float tickRate;
    private LayerMask enemyLayer;
    private string poolTag;

    private float currentDuration;
    private float nextTickTime;
    private bool isInitialized = false;

    private Collider2D[] hitColliders = new Collider2D[20];

    [Header("블랙홀 범위 설정")]
    [SerializeField] private float centerRadius = 0.5f; 

    [Header("시각적 이펙트 (선택사항)")]
    [SerializeField] private Transform rangeVisual;  
    [SerializeField] private Transform centerVisual;

    private ObjectPoolManager poolManager;

    [Inject]
    public void Construct(ObjectPoolManager poolManager)
    {
        this.poolManager = poolManager;
    }
    public void Initialize(float damage, float range, float pullForce, float duration, float tickRate, LayerMask enemyLayer, string poolTag)
    {
        this.damage = damage;
        this.range = range;
        this.pullForce = pullForce;
        this.duration = duration;
        this.tickRate = tickRate;
        this.enemyLayer = enemyLayer;
        this.poolTag = poolTag;

        currentDuration = duration;
        nextTickTime = Time.time + tickRate;

        if (rangeVisual != null)
        {
            rangeVisual.localScale = new Vector3(range * 2, range * 2, 1f);
        }
        if (centerVisual != null)
        {
            centerVisual.localScale = new Vector3(centerRadius * 2, centerRadius * 2, 1f);
        }

        isInitialized = true;
    }

    void FixedUpdate()
    {
        if (!isInitialized) return;

        PullEnemies();
        DealTickDamage();

        currentDuration -= Time.fixedDeltaTime;
        if (currentDuration <= 0)
        {
            ReturnToPool();
        }
    }

    private void PullEnemies()
    {
        int hitCount = Physics2D.OverlapCircleNonAlloc(transform.position, range, hitColliders, enemyLayer);

        for (int i = 0; i < hitCount; i++)
        {
            Collider2D col = hitColliders[i];

            Vector3 targetPos = transform.position;
            Vector3 enemyPos = col.transform.position;

            Vector3 pullDirection = (targetPos - enemyPos).normalized;
            float distance = Vector2.Distance(targetPos, enemyPos);

            if (col.TryGetComponent(out Rigidbody2D rb))
            {
                if (distance < centerRadius)
                {
                    rb.velocity = Vector2.zero;
                    col.transform.position = Vector3.MoveTowards(enemyPos, targetPos, 2f * Time.fixedDeltaTime);
                }
                else
                {
                    rb.velocity = Vector2.Lerp(rb.velocity, pullDirection * pullForce, 15f * Time.fixedDeltaTime);
                }
            }
            else
            {
                if (distance > centerRadius)
                {
                    col.transform.position = Vector3.MoveTowards(enemyPos, targetPos, pullForce * Time.fixedDeltaTime);
                }
            }
        }
    }

    private void DealTickDamage()
    {
        if (Time.time >= nextTickTime)
        {
            int hitCount = Physics2D.OverlapCircleNonAlloc(transform.position, range, hitColliders, enemyLayer);
            for (int i = 0; i < hitCount; i++)
            {
               
                if (hitColliders[i].TryGetComponent(out IDamageable target))
                {
                    target.TakeDamage(damage);
                }
            }
            nextTickTime = Time.time + tickRate;
        }
    }

    private void ReturnToPool()
    {
        isInitialized = false;
        if (poolManager != null && !string.IsNullOrEmpty(poolTag))
        {
            poolManager.ReturnToPool(poolTag, gameObject);
        }
        else
        {
            gameObject.SetActive(false);
        }
    }
}