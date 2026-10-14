using UnityEngine;
using Nytherion.Core.Interfaces;
using Nytherion.Core.Managers;

namespace Nytherion.GamePlay.Skills
{
public class BlackholeField : MonoBehaviour
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
    private bool isDespawning;
    private float remainingDespawnTime;

    private static readonly int SummonState = Animator.StringToHash("Base Layer.Summon");
    private static readonly int DespawnState = Animator.StringToHash("Base Layer.Despawn");

    private Collider2D[] hitColliders = new Collider2D[20];

    [SerializeField] private float centerRadius = 0.5f; 

    [SerializeField] private Transform rangeVisual;  
    [SerializeField] private Transform centerVisual;
    [SerializeField] private Animator visualAnimator;
    [SerializeField] private AnimationClip despawnAnimation;

    private void OnDisable()
    {
        isInitialized = false;
        isDespawning = false;
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
        isDespawning = false;

        // 풀에서 재사용할 때도 소환 첫 프레임부터 시작합니다.
        if (visualAnimator != null && visualAnimator.runtimeAnimatorController != null)
        {
            visualAnimator.Rebind();
            visualAnimator.Play(SummonState, 0, 0f);
            visualAnimator.Update(0f);
        }

        if (rangeVisual != null)
        {
            float spriteDiameter = 1f;
            if (rangeVisual.TryGetComponent(out SpriteRenderer renderer) && renderer.sprite != null)
                spriteDiameter = Mathf.Max(renderer.sprite.rect.width, renderer.sprite.rect.height) / renderer.sprite.pixelsPerUnit;
            float scale = range * 2f / spriteDiameter;
            rangeVisual.localScale = new Vector3(scale, scale, 1f);
        }
        if (centerVisual != null)
        {
            centerVisual.localScale = new Vector3(centerRadius * 2, centerRadius * 2, 1f);
        }

        isInitialized = true;
    }

    void FixedUpdate()
    {
        // 소멸 중에는 흡인·피해 없이 마지막 프레임까지 보여줍니다.
        if (isDespawning)
        {
            remainingDespawnTime -= Time.fixedDeltaTime;
            if (remainingDespawnTime <= 0f) ReturnToPool();
            return;
        }
        if (!isInitialized) return;

        PullEnemies();
        DealTickDamage();

        currentDuration -= Time.fixedDeltaTime;
        if (currentDuration <= 0)
        {
            BeginDespawn();
        }
    }

    private void BeginDespawn()
    {
        isInitialized = false;
        if (visualAnimator == null || visualAnimator.runtimeAnimatorController == null || despawnAnimation == null)
        {
            ReturnToPool();
            return;
        }

        isDespawning = true;
        remainingDespawnTime = despawnAnimation.length;
        visualAnimator.Play(DespawnState, 0, 0f);
        visualAnimator.Update(0f);
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
            float distanceSqr = (targetPos - enemyPos).sqrMagnitude;

            if (col.TryGetComponent(out Rigidbody2D rb))
            {
                if (distanceSqr < centerRadius * centerRadius)
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
                if (distanceSqr > centerRadius * centerRadius)
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
        isDespawning = false;
        if (ObjectPoolManager.Instance != null && !string.IsNullOrEmpty(poolTag))
        {
            ObjectPoolManager.Instance.ReturnToPool(poolTag, gameObject);
        }
        else
        {
            gameObject.SetActive(false);
        }
    }
}
}
