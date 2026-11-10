using Nytherion.Core.Interfaces;
using Nytherion.Core.Managers;
using Nytherion.GamePlay.Characters.Enemy;
using System;
using UnityEngine;

namespace Nytherion.GamePlay.Skills
{
public class SoulEaterProj : MonoBehaviour
{
    [Header("영혼 이펙트")]
    [SerializeField] private Sprite[] flightFrames;
    [SerializeField] private Sprite[] hitFrames;
    [SerializeField, Min(1f)] private float animationFps = 12f;
    [SerializeField] private SoulEaterVFX killEffectPrefab;

    private SpriteRenderer spriteRenderer;
    private Collider2D hitCollider;
    private float elapsed;
    private bool isImpact;
    private float damage;
    private float speed;
    private float range;
    private string poolTag;
    private Vector3 startPosition;
    private bool isInitialized = false;
    private Action onKillCallback;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        hitCollider = GetComponent<Collider2D>();
    }

    public void Initialize(float damage, float speed, float range, string poolTag, Action onKillCallback = null)
    {
        this.damage = damage;
        this.speed = speed;
        this.range = range;
        this.poolTag = poolTag;
        this.onKillCallback = onKillCallback;
        startPosition = transform.position;
        elapsed = 0f;
        isImpact = false;
        isInitialized = true;
        hitCollider.enabled = true;
        if (flightFrames != null && flightFrames.Length > 0) spriteRenderer.sprite = flightFrames[0];
    }

    void Update()
    {
        if (!isInitialized) return;

        elapsed += Time.deltaTime;
        int frame = Mathf.FloorToInt(elapsed * Mathf.Max(1f, animationFps));
        if (isImpact)
        {
            if (hitFrames == null || frame >= hitFrames.Length) ReturnToPool();
            else spriteRenderer.sprite = hitFrames[frame];
            return;
        }
        if (flightFrames != null && flightFrames.Length > 0)
            spriteRenderer.sprite = flightFrames[frame % flightFrames.Length];

        transform.Translate(Vector3.right * (speed * Time.deltaTime));

        if ((transform.position - startPosition).sqrMagnitude >= range * range)
        {
            ReturnToPool();
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!isInitialized || isImpact) return;
        if (other.CompareTag("Player") || other.CompareTag("Weapon"))
        {
            return;
        }

        bool hitValidTarget = false;

        EnemyBase enemy = other.GetComponentInParent<EnemyBase>();
        if (enemy != null && enemy.isDead) return;
        IDamageable damageable = enemy != null ? enemy : other.GetComponent<IDamageable>();
        if (damageable != null)
        {
            bool wasDead = false;
            Vector3 footPosition = other.bounds.center;
            float bodyHeight = Mathf.Max(0.3f, other.bounds.size.y);
            if (enemy != null)
            {
                wasDead = enemy.isDead;
                // 사망 처리로 몸체가 비활성화되기 전에 발 위치와 크기를 보관합니다.
                footPosition = enemy.GetFootPosition();
                if (enemy.BodyRenderer != null) bodyHeight = Mathf.Max(0.3f, enemy.BodyRenderer.bounds.size.y);
            }

            BeginImpact();
            damageable.TakeDamage(damage);

            if (enemy != null && !wasDead && enemy.isDead)
            {
                PlayEffect(killEffectPrefab, footPosition, Quaternion.identity,
                    Vector3.one * (bodyHeight / 2f), bodyHeight * 0.6f);
                if (onKillCallback != null)
                {
                    onKillCallback.Invoke();
                }
            }
            hitValidTarget = true;
        }
        else if (other.CompareTag("Wall"))
        {
            BeginImpact();
            hitValidTarget = true;
        }

        if (hitValidTarget && (hitFrames == null || hitFrames.Length == 0))
        {
            ReturnToPool();
        }
    }

    private void BeginImpact()
    {
        isImpact = true;
        elapsed = 0f;
        hitCollider.enabled = false;
        if (hitFrames != null && hitFrames.Length > 0) spriteRenderer.sprite = hitFrames[0];
    }

    private void PlayEffect(SoulEaterVFX prefab, Vector3 position, Quaternion rotation, Vector3 scale, float rise)
    {
        if (prefab == null) return;
        ObjectPoolManager pool = ObjectPoolManager.Instance;
        GameObject instance = pool != null
            ? pool.SpawnFromPool(prefab.gameObject, position, rotation)
            : Instantiate(prefab.gameObject, position, rotation);
        if (instance != null && instance.TryGetComponent(out SoulEaterVFX effect))
            effect.Play(pool, scale, rise);
    }

    private void OnDisable()
    {
        isInitialized = false;
        isImpact = false;
        elapsed = 0f;
        onKillCallback = null;
        if (hitCollider != null) hitCollider.enabled = false;
    }

    private void ReturnToPool()
    {
        isInitialized = false;
        if (ObjectPoolManager.Instance != null && !string.IsNullOrEmpty(poolTag))
        {
            ObjectPoolManager.Instance.ReturnToPool(poolTag, gameObject);
        }
        else
        {
            Destroy(gameObject); 
        }
    }
}
}
