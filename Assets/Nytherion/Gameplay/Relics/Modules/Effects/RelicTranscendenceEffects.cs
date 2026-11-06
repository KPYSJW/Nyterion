using System;
using System.Collections.Generic;
using Nytherion.Core.Enums;
using Nytherion.Core.Interfaces;
using Nytherion.Core.Managers;
using Nytherion.Core.Systems;
using Nytherion.Core.Utils;
using Nytherion.GamePlay.Characters.Enemy;
using Nytherion.GamePlay.Characters.Player;
using Nytherion.GamePlay.Combat;
using UnityEngine;

namespace Nytherion.Gameplay.Relics.Modules
{
    [Serializable, RelicDisplayName("얼음 파편")]
    public sealed class IceSplinterEffect : RelicEffectBase
    {
        public Texture2D projectileTexture;
        [Min(0f)] public float shardDamageRatio = 0.35f;
        [Min(1)] public int maxShardTargets = 2;
        [Min(0.1f)] public float searchRadius = 7f;

        [NonSerialized] private IceSplinterRuntime appliedRuntime;

        public override void ApplyEffect(PlayerManager playerManager, int level)
        {
            RemoveEffect(playerManager, level);
            if (playerManager == null) return;

            appliedRuntime = playerManager.GetComponent<IceSplinterRuntime>();
            if (appliedRuntime == null)
            {
                appliedRuntime = playerManager.gameObject.AddComponent<IceSplinterRuntime>();
            }
            appliedRuntime.SetBonus(this, shardDamageRatio, maxShardTargets, searchRadius);
        }

        public override void RemoveEffect(PlayerManager playerManager, int level)
        {
            if (appliedRuntime == null && playerManager != null)
            {
                appliedRuntime = playerManager.GetComponent<IceSplinterRuntime>();
            }
            appliedRuntime?.RemoveBonus(this);
            appliedRuntime = null;
        }
    }

    public sealed class IceSplinterRuntime : MonoBehaviour
    {
        private readonly Dictionary<IceSplinterEffect, Vector3> bonuses = new Dictionary<IceSplinterEffect, Vector3>();
        private readonly List<IceSplinterProjectile> projectiles = new List<IceSplinterProjectile>();
        private Transform projectileRoot;
        private Sprite[] frames;
        private Texture2D texture;
        public float ShardDamageRatio { get; private set; }
        public bool IsActive => bonuses.Count > 0;

        public void SetBonus(IceSplinterEffect source, float ratio, int targets, float radius)
        {
            if (source == null) return;
            bonuses[source] = new Vector3(ratio, targets, radius);
            Recalculate();
        }

        public void RemoveBonus(IceSplinterEffect source)
        {
            if (source == null || !bonuses.Remove(source)) return;
            Recalculate();
        }

        private void Recalculate()
        {
            ShardDamageRatio = 0f;
            foreach (var bonus in bonuses)
            {
                ShardDamageRatio = Mathf.Max(ShardDamageRatio, bonus.Value.x);
                if (bonus.Key.projectileTexture != null) SetTexture(bonus.Key.projectileTexture);
            }
        }

        private void SetTexture(Texture2D value)
        {
            if (texture == value) return;
            if (frames != null) foreach (Sprite frame in frames) if (frame != null) Destroy(frame);
            texture = value;
            frames = new Sprite[4];
            for (int i = 0; i < frames.Length; i++)
                frames[i] = Sprite.Create(texture, new Rect(i * 32, 0, 32, 32), new Vector2(0.5f, 0.5f), 32f);
        }

        public void Fire(Vector3 origin, IDamageable source, float damage, int count, float speed)
        {
            if (!IsActive || frames == null) return;
            if (projectileRoot == null)
            {
                // 플레이어 이동을 물려받지 않도록 같은 씬의 독립된 루트에서 관리합니다.
                GameObject root = new GameObject("서리 파편 투사체 루트");
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, gameObject.scene);
                projectileRoot = root.transform;
                root.SetActive(gameObject.activeInHierarchy);
            }
            for (int i = 0; i < count; i++)
            {
                IceSplinterProjectile projectile = projectiles.Find(item => item != null && !item.gameObject.activeSelf);
                if (projectile == null)
                {
                    GameObject instance = new GameObject("서리 파편 투사체");
                    instance.transform.SetParent(projectileRoot, false);
                    projectile = instance.AddComponent<IceSplinterProjectile>();
                    projectiles.Add(projectile);
                }
                float angle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
                projectile.Launch(origin, new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)), speed,
                    damage, source, GetComponent<PlayerManager>(), frames);
            }
        }

        private void OnEnable()
        {
            if (projectileRoot != null) projectileRoot.gameObject.SetActive(true);
        }

        private void OnDisable()
        {
            if (projectileRoot != null) projectileRoot.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (projectileRoot != null) Destroy(projectileRoot.gameObject);
            if (frames != null) foreach (Sprite frame in frames) if (frame != null) Destroy(frame);
        }
    }

    public sealed class IceSplinterProjectile : MonoBehaviour
    {
        private readonly List<RaycastHit2D> hits = new List<RaycastHit2D>();
        private SpriteRenderer spriteRenderer;
        private Sprite[] frames;
        private Vector2 direction;
        private float speed, damage, elapsed;
        private IDamageable source;
        private PlayerManager player;

        public void Launch(Vector3 origin, Vector2 aim, float flightSpeed, float hitDamage,
            IDamageable sourceTarget, PlayerManager owner, Sprite[] animation)
        {
            direction = aim.normalized;
            speed = Mathf.Max(0.1f, flightSpeed);
            damage = Mathf.Max(0f, hitDamage);
            source = sourceTarget;
            player = owner;
            frames = animation;
            elapsed = 0f;
            transform.SetPositionAndRotation(origin, Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg));
            transform.localScale = Vector3.one * 0.6f;
            if (spriteRenderer == null) spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
            spriteRenderer.sortingOrder = 10;
            spriteRenderer.sprite = frames[0];
            gameObject.SetActive(true);
        }

        private void Update()
        {
            float step = Mathf.Min(Time.deltaTime, 2f - elapsed);
            if (step <= 0f) { gameObject.SetActive(false); return; }
            float distance = speed * step;
            ContactFilter2D filter = new ContactFilter2D { useTriggers = true };
            hits.Clear();
            Physics2D.CircleCast(transform.position, 0.12f, direction, filter, hits, distance);
            foreach (RaycastHit2D hit in hits)
            {
                if (hit.collider == null) continue;
                EnemyBase enemy = hit.collider.GetComponentInParent<EnemyBase>();
                if (enemy == null || ReferenceEquals(enemy, source) || enemy.isDead || !enemy.isActiveAndEnabled ||
                    !CollisionObject.IsEnemyCollider(hit.collider)) continue;
                PlayHitEffect(hit.point);
                if (damage > 0f) enemy.TakeDamage(damage, true);
                if (!enemy.isDead && enemy.TryGetComponent(out StatusEffectManager status))
                {
                    status.ConfigureCombatContext(player);
                    status.ApplyEffect(new IceEffect(5f));
                }
                gameObject.SetActive(false);
                return;
            }
            transform.position += (Vector3)(direction * distance);
            elapsed += step;
            spriteRenderer.sprite = frames[Mathf.FloorToInt(elapsed * 12f) % frames.Length];
        }

        private void PlayHitEffect(Vector2 hitPoint)
        {
            ObjectPoolManager pool = player != null ? player.ObjectPool : null;
            if (pool == null) return;

            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
            GameObject effect = pool.SpawnFromPool("IcicleHitVFX", hitPoint, Quaternion.Euler(0f, 0f, angle));
            if (effect == null) return;

            effect.transform.localScale *= 0.5f;
            WeaponVFXHelper.ApplyHitEffectSorting(effect);
            if (effect.TryGetComponent(out SpriteRenderer effectRenderer))
                effectRenderer.flipX = UnityEngine.Random.value < 0.5f;
        }
    }

    [Serializable, RelicDisplayName("열충격")]
    public sealed class ThermalShockEffect : RelicEffectBase
    {
        [Min(0f)] public float damageRatio = 0.8f;
        [Min(0.1f), InspectorName("기본 폭발 반경")]
        [Tooltip("효과 범위 1배일 때의 기본 크기입니다. 폭발 연출과 피해 반경에 함께 적용합니다.")]
        public float explosionRadius = 0.75f;
        [Min(1)] public int maxTargets = 4;
        [Tooltip("열충격 발동 위치에서 한 번 재생할 폭발 연출")]
        public ThermalShockExplosionVFX explosionEffectPrefab;

        [NonSerialized] private EventManager eventManager;
        [NonSerialized] private ObjectPoolManager effectPool;
        [NonSerialized] private PlayerManager cachedPlayerManager;

        public override void ApplyEffect(PlayerManager playerManager, int level)
        {
            RemoveEffect(playerManager, level);
            cachedPlayerManager = playerManager;
            eventManager = playerManager != null ? playerManager.EventManager : null;
            effectPool = playerManager != null ? playerManager.ObjectPool : null;
            if (eventManager != null)
            {
                eventManager.OnEnemyDamagedByPlayerDetailed += HandleEnemyDamaged;
            }
        }

        public override void RemoveEffect(PlayerManager playerManager, int level)
        {
            if (eventManager != null)
            {
                eventManager.OnEnemyDamagedByPlayerDetailed -= HandleEnemyDamaged;
            }
            eventManager = null;
            effectPool = null;
            cachedPlayerManager = null;
        }

        private void HandleEnemyDamaged(PlayerDamageEventData eventData)
        {
            if (eventData.IsChainDamage || eventData.IsDamageOverTime ||
                eventData.Target == null ||
                !eventData.Target.TryGetComponent(out StatusEffectManager statusManager))
            {
                return;
            }

            // 이번 공격으로 처음 두 속성이 모이는 경우도 같은 타격에서 한 번만 발동합니다.
            bool hasFire = statusManager.TryGetEffect(out FireEffect _) || eventData.HasAttackTrait(EquipmentTrait.Fire);
            bool hasIce = statusManager.TryGetEffect(out IceEffect _) || eventData.HasAttackTrait(EquipmentTrait.Ice);
            if (!hasFire || !hasIce)
            {
                return;
            }

            // 발동 시점의 효과 범위를 연출과 피해 판정에 동일하게 적용합니다.
            float size = cachedPlayerManager != null && cachedPlayerManager.currentPlayerData != null
                ? Mathf.Max(0.01f, cachedPlayerManager.currentPlayerData.projectileSizeMultiplier) : 1f;
            float radius = Mathf.Max(0.1f, explosionRadius) * size;
            PlayExplosion(eventData.Target, radius);
            DamageNearby(eventData.Target, eventData.DamageAmount * damageRatio, radius);
        }

        private void PlayExplosion(EnemyBase origin, float radius)
        {
            if (explosionEffectPrefab == null) return;
            Vector3 point = origin.transform.position;
            GameObject visual = effectPool != null
                ? effectPool.SpawnFromPool(explosionEffectPrefab.gameObject, point, Quaternion.identity, 2)
                : UnityEngine.Object.Instantiate(explosionEffectPrefab.gameObject, point, Quaternion.identity);
            if (visual == null) return;
            visual.GetComponent<ThermalShockExplosionVFX>().Play(effectPool, radius,
                origin.GetComponentInChildren<SpriteRenderer>());
        }

        private void DamageNearby(EnemyBase origin, float damage, float radius)
        {
            Collider2D[] hits = Physics2D.OverlapCircleAll(origin.transform.position, radius);
            HashSet<EnemyBase> uniqueTargets = new HashSet<EnemyBase>();
            int count = 0;
            foreach (Collider2D hit in hits)
            {
                EnemyBase enemy = hit != null ? hit.GetComponentInParent<EnemyBase>() : null;
                if (enemy == null || enemy == origin || enemy.isDead || !uniqueTargets.Add(enemy))
                {
                    continue;
                }

                enemy.TakeDamage(Mathf.Max(0f, damage), true);
                count++;
                if (count >= Mathf.Max(1, maxTargets)) break;
            }
        }
    }

    [Serializable, RelicDisplayName("부식성 화염")]
    public sealed class CorrosiveFlameEffect : RelicEffectBase
    {
        [Min(1f)] public float tickDamageMultiplier = 1.25f;
        [Range(0.1f, 1f)] public float tickIntervalMultiplier = 0.75f;

        [NonSerialized] private CorrosiveFlameRuntime appliedRuntime;

        public override void ApplyEffect(PlayerManager playerManager, int level)
        {
            RemoveEffect(playerManager, level);
            if (playerManager == null) return;

            appliedRuntime = playerManager.GetComponent<CorrosiveFlameRuntime>();
            if (appliedRuntime == null)
            {
                appliedRuntime = playerManager.gameObject.AddComponent<CorrosiveFlameRuntime>();
            }
            appliedRuntime.SetBonus(this, tickDamageMultiplier, tickIntervalMultiplier);
        }

        public override void RemoveEffect(PlayerManager playerManager, int level)
        {
            if (appliedRuntime == null && playerManager != null)
            {
                appliedRuntime = playerManager.GetComponent<CorrosiveFlameRuntime>();
            }
            appliedRuntime?.RemoveBonus(this);
            appliedRuntime = null;
        }
    }

    public sealed class CorrosiveFlameRuntime : MonoBehaviour
    {
        private readonly Dictionary<CorrosiveFlameEffect, Vector2> bonuses =
            new Dictionary<CorrosiveFlameEffect, Vector2>();

        public float TickDamageMultiplier { get; private set; } = 1f;
        public float TickIntervalMultiplier { get; private set; } = 1f;

        public void SetBonus(CorrosiveFlameEffect source, float damageMultiplier, float intervalMultiplier)
        {
            if (source == null) return;
            bonuses[source] = new Vector2(Mathf.Max(1f, damageMultiplier), Mathf.Clamp(intervalMultiplier, 0.1f, 1f));
            Recalculate();
        }

        public void RemoveBonus(CorrosiveFlameEffect source)
        {
            if (source == null || !bonuses.Remove(source)) return;
            Recalculate();
        }

        private void Recalculate()
        {
            TickDamageMultiplier = 1f;
            TickIntervalMultiplier = 1f;
            foreach (Vector2 bonus in bonuses.Values)
            {
                TickDamageMultiplier = Mathf.Max(TickDamageMultiplier, bonus.x);
                TickIntervalMultiplier = Mathf.Min(TickIntervalMultiplier, bonus.y);
            }
        }
    }

    [Serializable, RelicDisplayName("피의 성채")]
    public sealed class BloodFortressEffect : RelicEffectBase
    {
        [Range(0f, 1f)] public float recoveredDamageRatio = 0.15f;

        [NonSerialized] private PlayerHealth playerHealth;

        public override void ApplyEffect(PlayerManager playerManager, int level)
        {
            RemoveEffect(playerManager, level);
            playerHealth = playerManager != null ? playerManager.playerHealth : null;
            if (playerHealth != null)
            {
                PlayerHealth.OnPlayerDamaged += HandlePlayerDamaged;
            }
        }

        public override void RemoveEffect(PlayerManager playerManager, int level)
        {
            PlayerHealth.OnPlayerDamaged -= HandlePlayerDamaged;
            playerHealth = null;
        }

        private void HandlePlayerDamaged(float damage)
        {
            playerHealth?.Heal(damage * Mathf.Clamp01(recoveredDamageRatio));
        }
    }

    // 기존 직렬화 타입과 에셋 ID를 유지해 저장 데이터와 참조를 보존합니다.
    [Serializable, RelicDisplayName("연쇄 폭풍")]
    public sealed class DashChainLightningEffect : RelicEffectBase
    {
        [Range(0f, 1f), InspectorName("번개 발동 확률")] public float triggerChance = 0.25f;
        [Range(0f, 1f), InspectorName("번개 피격 적의 돌풍 생성 확률")] public float windSpawnChance = 0.25f;
        [Min(0.1f), InspectorName("대시 대상 탐색 반경")] public float dashTargetSearchRadius = 8f;
        [Min(0f)] public float damageRatio = 0.45f;
        [Min(0.1f)] public float searchRadius = 6f;
        [Min(1)] public int maxTargets = 4;
        // 이전 에셋의 직렬화 값을 보존하며 적중 대기와 대시 충전은 사용하지 않습니다.
        [HideInInspector] public float hitWaitDuration = 2f;
        [HideInInspector] public int chargesPerDash = 3;
        public WindBlastProjectile projectilePrefab;
        [Tooltip("번개부르미와 공유하는 번개 연결 이펙트")]
        public ChainLightningVFX lightningEffectPrefab;
        [Min(0f), InspectorName("돌풍 투사체 피해 배율")] public float projectileDamageRatio = 0.75f;
        [NonSerialized] private DashStormRuntime runtime;

        public override void ApplyEffect(PlayerManager playerManager, int level)
        {
            RemoveEffect(playerManager, level);
            if (playerManager == null) return;
            runtime = DashStormRuntime.GetOrCreate(playerManager);
            runtime.AddStorm(this);
        }

        public override void RemoveEffect(PlayerManager playerManager, int level)
        {
            if (runtime != null) runtime.RemoveStorm(this);
            runtime = null;
        }
    }
    [Serializable, RelicDisplayName("골드 러쉬")]
    public sealed class GoldenRushEffect : RelicEffectBase
    {
        [Min(0.1f)] public float goldWindow = 1.5f;
        [Min(0f)] public float goldGainBonus = 0.25f;
        [Min(0f)] public float dashCooldownRefund = 0.15f;

        [NonSerialized] private EventManager eventManager;
        [NonSerialized] private CurrencyDataManager currencyDataManager;
        [NonSerialized] private PlayerController playerController;
        [NonSerialized] private float goldWindowEnd;
        [NonSerialized] private bool isGrantingBonus;
        [NonSerialized] private bool refundedThisDash;

        public override void ApplyEffect(PlayerManager playerManager, int level)
        {
            RemoveEffect(playerManager, level);
            if (playerManager == null) return;

            eventManager = playerManager.EventManager;
            playerController = playerManager.GetComponent<PlayerController>();
            currencyDataManager = DataLifetimeScope.Instance != null
                ? DataLifetimeScope.Instance.GetDataManager<CurrencyDataManager>()
                : null;

            if (eventManager != null)
            {
                eventManager.OnPlayerDashStarted += HandleDashStarted;
            }
            if (currencyDataManager != null)
            {
                currencyDataManager.OnDataChanged += HandleCurrencyChanged;
            }
        }

        public override void RemoveEffect(PlayerManager playerManager, int level)
        {
            if (eventManager != null)
            {
                eventManager.OnPlayerDashStarted -= HandleDashStarted;
            }
            if (currencyDataManager != null)
            {
                currencyDataManager.OnDataChanged -= HandleCurrencyChanged;
            }
            eventManager = null;
            currencyDataManager = null;
            playerController = null;
        }

        private void HandleDashStarted()
        {
            goldWindowEnd = Time.time + Mathf.Max(0.1f, goldWindow);
            refundedThisDash = false;
        }

        private void HandleCurrencyChanged(CurrencyChangeData data)
        {
            if (isGrantingBonus || Time.time > goldWindowEnd || data.isSilent ||
                data.currencyType != CurrencyType.Gold || data.changeAmount <= 0 ||
                currencyDataManager == null)
            {
                return;
            }

            int bonus = Mathf.RoundToInt(data.changeAmount * Mathf.Max(0f, goldGainBonus));
            if (bonus > 0)
            {
                try
                {
                    isGrantingBonus = true;
                    currencyDataManager.AddUnmodifiedCurrency(CurrencyType.Gold, bonus);
                }
                finally
                {
                    isGrantingBonus = false;
                }
            }

            if (!refundedThisDash && playerController != null)
            {
                playerController.LastDashTime -= Mathf.Max(0f, dashCooldownRefund);
                refundedThisDash = true;
            }
        }
    }

    [Serializable, RelicDisplayName("군단 소환수 강화")]
    public sealed class LegionCompanionEffect : RelicEffectBase
    {
        [Min(0f)] public float damageMultiplier = 1.25f;
        [Range(0.1f, 1f)] public float attackIntervalMultiplier = 0.85f;

        [NonSerialized] private LegionCompanionRuntime appliedRuntime;

        public override void ApplyEffect(PlayerManager playerManager, int level)
        {
            RemoveEffect(playerManager, level);
            if (playerManager == null) return;

            appliedRuntime = playerManager.GetComponent<LegionCompanionRuntime>();
            if (appliedRuntime == null)
            {
                appliedRuntime = playerManager.gameObject.AddComponent<LegionCompanionRuntime>();
            }
            appliedRuntime.SetBonus(this, damageMultiplier, attackIntervalMultiplier);
        }

        public override void RemoveEffect(PlayerManager playerManager, int level)
        {
            if (appliedRuntime == null && playerManager != null)
            {
                appliedRuntime = playerManager.GetComponent<LegionCompanionRuntime>();
            }
            appliedRuntime?.RemoveBonus(this);
            appliedRuntime = null;
        }
    }

    public sealed class LegionCompanionRuntime : MonoBehaviour
    {
        private readonly Dictionary<LegionCompanionEffect, Vector2> bonuses =
            new Dictionary<LegionCompanionEffect, Vector2>();

        public float DamageMultiplier { get; private set; } = 1f;
        public float AttackIntervalMultiplier { get; private set; } = 1f;

        public void SetBonus(LegionCompanionEffect source, float damage, float interval)
        {
            if (source == null) return;
            bonuses[source] = new Vector2(damage, interval);
            Recalculate();
        }

        public void RemoveBonus(LegionCompanionEffect source)
        {
            if (source == null || !bonuses.Remove(source)) return;
            Recalculate();
        }

        private void Recalculate()
        {
            DamageMultiplier = 1f;
            AttackIntervalMultiplier = 1f;
            foreach (Vector2 bonus in bonuses.Values)
            {
                DamageMultiplier = Mathf.Max(DamageMultiplier, bonus.x);
                AttackIntervalMultiplier = Mathf.Min(AttackIntervalMultiplier, bonus.y);
            }
        }
    }
}
