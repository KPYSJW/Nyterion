using Nytherion.Core.Interfaces;
using Nytherion.Core.Managers;
using Nytherion.Core.Enums;
using System.Collections.Generic;
using UnityEngine;
using VContainer;
using Nytherion.GamePlay.Characters.Player;
using Nytherion.Gameplay.Relics.Modules;
using Nytherion.GamePlay.Characters.Enemy;
using Nytherion.GamePlay.Skills;

namespace Nytherion.GamePlay.Combat
{
    public class CollisionObject : MonoBehaviour
    {
        [HideInInspector] public float damage;
        [HideInInspector] public float chargePercent = 0f;
        [HideInInspector] public float effectSizeMultiplier = 1f;

        [Header("Projectile Traits")]
        public List<EquipmentTrait> traits = new List<EquipmentTrait>();
        public GameObject hitEffectPrefab;
        [Tooltip("벽과 충돌할 때도 피격 이펙트를 재생합니다.")]
        public bool playHitEffectOnWall;
        [Tooltip("타격 이펙트의 오른쪽 축이 탄환이 날아온 쪽을 향합니다.")]
        public bool alignHitEffectToIncomingDirection;
        [Tooltip("유물의 추가 피해로 처리해 적중 효과의 재귀 발동을 방지합니다.")]
        public bool isChainDamage;

        [Header("Pool Settings")]
        public string poolTag = "PlayerProj";

        private readonly List<IProjModifier> projModifiers = new List<IProjModifier>();
        private PlayerManager playerManager;
        private PiercingModifier piercingModifier;
        private BounceModifier bounceModifier;
        private bool hasBasePiercingModifier;
        private bool hasBaseBounceModifier;
        private CombatModifierSnapshot modifierSnapshot = CombatModifierSnapshot.Empty;
        private int bounceCount;
        private bool suppressBounce;
        private IDamageable extraProjectileSourceTarget;
        private bool isSecondaryProjectile;
        private LayerMask obstacleCollisionLayers;
        private bool useObstacleCollision;
        private bool hasReturnedToPool;
        private Transform projectileOwner;
        private Vector2 previousGatePosition;
        private List<RaycastHit2D> gateHits;

        public Transform ProjectileOwner => projectileOwner != null ? projectileOwner :
            playerManager != null ? playerManager.transform : null;
        public bool HasPassedProjectileGate { get; private set; }
        internal Vector3 CurrentHitCenter { get; private set; }

        public void SetProjectileOwner(Transform owner) => projectileOwner = owner;

        public void CopyGatePayloadTo(CollisionObject output)
        {
            output.Configure(damage, traits, chargePercent, hitEffectPrefab, ModifierSnapshot, true, suppressBounce);
            output.extraProjectileSourceTarget = extraProjectileSourceTarget;
            output.projectileOwner = ProjectileOwner;
            output.HasPassedProjectileGate = true;
            output.isChainDamage = isChainDamage;
            output.hitEffectPrefab = hitEffectPrefab;
            output.playHitEffectOnWall = playHitEffectOnWall;
            output.alignHitEffectToIncomingDirection = alignHitEffectToIncomingDirection;
            output.useObstacleCollision = useObstacleCollision;
            output.obstacleCollisionLayers = obstacleCollisionLayers;
        }

        public CombatModifierSnapshot ModifierSnapshot => playerManager != null && playerManager.playerRelicManager != null
            ? playerManager.playerRelicManager.CombatModifiers
            : modifierSnapshot;

        [Inject]
        public void Construct(PlayerManager playerManager)
        {
            this.playerManager = playerManager;
        }

        private void Awake()
        {
            piercingModifier = GetComponent<PiercingModifier>();
            hasBasePiercingModifier = piercingModifier != null && piercingModifier.enabled;
            if (piercingModifier == null)
            {
                piercingModifier = gameObject.AddComponent<PiercingModifier>();
                piercingModifier.enabled = false;
            }

            bounceModifier = GetComponent<BounceModifier>();
            hasBaseBounceModifier = bounceModifier != null && bounceModifier.enabled;
            if (bounceModifier == null)
            {
                bounceModifier = gameObject.AddComponent<BounceModifier>();
                bounceModifier.maxBounces = 3;
                bounceModifier.bounceRadius = 5f;
                bounceModifier.enabled = false;
            }

            RefreshProjModifiers();
        }

        private void OnEnable()
        {
            projectileOwner = null;
            HasPassedProjectileGate = false;
            previousGatePosition = transform.position;
            effectSizeMultiplier = 1f;
            hasReturnedToPool = false;
            useObstacleCollision = false;
            obstacleCollisionLayers = 0;
            bounceCount = 0;
            suppressBounce = false;
            extraProjectileSourceTarget = null;
            isSecondaryProjectile = false;
            CombatModifierSnapshot currentSnapshot = playerManager != null && playerManager.playerRelicManager != null
                ? playerManager.playerRelicManager.CombatModifiers
                : CombatModifierSnapshot.Empty;
            ApplyModifierSnapshot(currentSnapshot);
        }

        public void Configure(
            float projectileDamage,
            List<EquipmentTrait> projectileTraits,
            float projectileChargePercent,
            GameObject projectileHitEffect,
            CombatModifierSnapshot currentSnapshot,
            bool secondaryProjectile = false,
            bool disableBounce = false)
        {
            bounceCount = 0;
            suppressBounce = disableBounce;
            extraProjectileSourceTarget = null;
            isSecondaryProjectile = secondaryProjectile;
            damage = projectileDamage;
            traits = projectileTraits;
            chargePercent = projectileChargePercent;
            if (hitEffectPrefab == null)
            {
                hitEffectPrefab = projectileHitEffect;
            }
            ApplyModifierSnapshot(currentSnapshot);
        }

        public void RefreshProjModifiers()
        {
            projModifiers.Clear();
            GetComponents(projModifiers);
        }

        public void DisableAllProjModifiers()
        {
            for (int i = 0; i < projModifiers.Count; i++)
            {
                if (projModifiers[i] is MonoBehaviour effectBehaviour)
                {
                    effectBehaviour.enabled = false;
                }
            }
        }

        private void ApplyModifierSnapshot(CombatModifierSnapshot currentSnapshot)
        {
            modifierSnapshot = currentSnapshot ?? CombatModifierSnapshot.Empty;
            piercingModifier.enabled = hasBasePiercingModifier || modifierSnapshot.HasProjectilePiercing;
            bounceModifier.enabled = !suppressBounce &&
                (hasBaseBounceModifier || modifierSnapshot.HasProjectileBounce);
        }

        public void ConfigureObstacleCollision(LayerMask layers)
        {
            useObstacleCollision = true;
            obstacleCollisionLayers = layers;
        }

        private bool IsObstacleCollision(Collider2D other)
        {
            if (!useObstacleCollision || other.isTrigger) return false;
            for (Transform current = other.transform; current != null; current = current.parent)
            {
                if (current.CompareTag("Wall") || (obstacleCollisionLayers.value & (1 << current.gameObject.layer)) != 0)
                    return true;
            }
            return false;
        }

        private void FixedUpdate()
        {
            Vector2 position = transform.position;
            if (!hasReturnedToPool && !HasPassedProjectileGate && ProjectileOwner != null &&
                (position - previousGatePosition).sqrMagnitude > 0.000001f)
            {
                // 트리거보다 빠르게 이동한 탄도 이전 물리 위치부터 경로를 검사합니다.
                if (gateHits == null) gateHits = new List<RaycastHit2D>(8);
                ContactFilter2D filter = new ContactFilter2D { useTriggers = true };
                filter.SetLayerMask(1 << 0);
                Physics2D.Linecast(previousGatePosition, position, filter, gateHits);
                for (int i = 0; i < gateHits.Count; i++)
                {
                    if (gateHits[i].collider.TryGetComponent(out ProjectileGateController gate) && gate.TryConvert(this))
                        break;
                }
                gateHits.Clear();
            }
            previousGatePosition = position;
        }

        private void OnTriggerEnter2D(Collider2D collision)
        {
            // 같은 물리 틱에 여러 충돌체가 진입해도 이미 반환된 탄은 피해를 주지 않습니다.
            if (hasReturnedToPool || !gameObject.activeInHierarchy) return;
            if (collision.TryGetComponent(out ProjectileGateController gate))
            {
                gate.TryConvert(this);
                return;
            }
            bool isEnemy = IsEnemyCollider(collision);
            bool isWall = HasTagInParents(collision.transform, "Wall") || IsObstacleCollision(collision);
            if (isEnemy && extraProjectileSourceTarget != null &&
                ReferenceEquals(collision.GetComponentInParent<IDamageable>(), extraProjectileSourceTarget)) return;
            if (isEnemy && bounceModifier.enabled && bounceModifier.ShouldIgnoreHit(collision)) return;

            if (isEnemy || isWall)
            {
                // 처치로 충돌체가 비활성화되기 전에 타격 연출 기준점을 보관합니다.
                CurrentHitCenter = collision.bounds.center;
                Vector2 hitPoint = collision.ClosestPoint(transform.position);
                Vector2 incomingDirection = -transform.right;
                if (alignHitEffectToIncomingDirection && TryGetComponent(out Rigidbody2D hitRigidbody) &&
                    hitRigidbody.velocity.sqrMagnitude > 0.000001f)
                    incomingDirection = -hitRigidbody.velocity.normalized;
                if (isEnemy)
                {
                    IDamageable target = collision.GetComponentInParent<IDamageable>();
                    if (target is EnemyBase enemy) enemy.TakeDamage(damage, isChainDamage, traits);
                    else target?.TakeDamage(damage, isChainDamage);
                    if (!isSecondaryProjectile && bounceModifier.enabled)
                        SpawnRicochetExtraProjectiles(hitPoint, target);
                    if (TryGetComponent(out HomingProj homingProjectile))
                        homingProjectile.StopTrackingHitTarget(target);

                    if (target != null && traits != null &&
                        collision.GetComponentInParent<StatusEffectManager>() is StatusEffectManager effectManager)
                    {
                        for (int i = 0; i < traits.Count; i++)
                        {
                            switch (traits[i])
                            {
                                case EquipmentTrait.Fire:
                                    effectManager.ApplyEffect(new FireEffect(Mathf.Max(1f, damage * 0.2f), 5f));
                                    break;
                                case EquipmentTrait.Curse:
                                    effectManager.ApplyEffect(new CurseEffect(1.1f, 5f));
                                    break;
                                case EquipmentTrait.Ice:
                                    effectManager.ApplyEffect(new IceEffect(5f));
                                    break;
                                case EquipmentTrait.Lightning:
                                    effectManager.ApplyEffect(new LightningEffect(5f));
                                    break;
                                case EquipmentTrait.Holy:
                                    effectManager.ApplyEffect(new HolyEffect(5f));
                                    break;
                                case EquipmentTrait.Demonic:
                                    effectManager.ApplyEffect(new DemonicEffect(5f));
                                    break;
                                case EquipmentTrait.Poison:
                                    effectManager.ApplyEffect(new PoisonEffect(3f, 5f));
                                    break;
                            }
                        }
                    }

                }

                if (isEnemy || (isWall && playHitEffectOnWall))
                {
                    // 적 또는 설정된 벽 충돌 위치에 피격 이펙트를 재생합니다.
                    WeaponVFXHelper.PlayHitEffect(hitEffectPrefab, hitPoint, chargePercent,
                        incomingDirection, alignHitEffectToIncomingDirection);
                }

                bool shouldSurvive = false;
                bool bounced = false;
                for (int i = 0; i < projModifiers.Count; i++)
                {
                    IProjModifier effect = projModifiers[i];
                    if (effect is MonoBehaviour mb && !mb.enabled) continue;

                    bool modifierSurvives = effect.OnHit(collision);
                    if (modifierSurvives)
                    {
                        shouldSurvive = true;
                    }

                    if (modifierSurvives && effect is BounceModifier)
                    {
                        bounced = true;
                    }
                }

                if (bounced && !isSecondaryProjectile) ApplyBounceBonus();

                if (!shouldSurvive)
                {
                    ReturnToPool();
                }
            }
        }

        public static bool IsEnemyCollider(Collider2D collision)
        {
            if (collision == null) return false;
            IDamageable damageable = collision.GetComponentInParent<IDamageable>();
            // 자식 감지용 트리거는 적 몸체로 취급하지 않습니다. 명시적인 Enemy 피격 영역은 유지합니다.
            if (collision.isTrigger && !collision.CompareTag("Enemy") &&
                damageable is MonoBehaviour owner && owner.transform != collision.transform) return false;
            return damageable is EnemyBase || HasTagInParents(collision.transform, "Enemy");
        }

        private static bool HasTagInParents(Transform origin, string tag)
        {
            for (Transform current = origin; current != null; current = current.parent)
                if (current.CompareTag(tag)) return true;
            return false;
        }

        public void ReturnToPool()
        {
            if (hasReturnedToPool) return;
            hasReturnedToPool = true;
            if (ObjectPoolManager.Instance != null && !string.IsNullOrEmpty(poolTag))
            {
                ObjectPoolManager.Instance.ReturnToPool(poolTag, gameObject);
            }
            else
            {
                gameObject.SetActive(false);
            }
        }

        private void ApplyBounceBonus()
        {
            TrickshotSetBonusRuntime bonus = null;
            if (playerManager != null) playerManager.TryGetComponent(out bonus);
            float damageMultiplier = bonus != null
                ? (bounceCount == 0 ? bonus.FirstBounceDamageMultiplier : bonus.LaterBounceDamageMultiplier)
                : (bounceCount == 0 ? 0.5f : 0.8f);
            if (bonus != null) damageMultiplier *= bonus.BounceDamageIncreaseMultiplier;
            // 양수 피해는 0.5에서 올림하여 도탄할 때마다 정수 피해를 적용합니다.
            damage = Mathf.Floor(Mathf.Max(0f, damage * damageMultiplier) + 0.5f);
            bounceCount++;

            if (bonus == null || bonus.BounceSpeedMultiplier <= 1f ||
                !TryGetComponent(out Rigidbody2D body)) return;
            body.velocity *= bonus.BounceSpeedMultiplier;
            if (TryGetComponent(out HomingProj homing)) homing.SetFlightSpeed(body.velocity.magnitude);
        }

        private void SpawnRicochetExtraProjectiles(Vector2 origin, IDamageable hitTarget)
        {
            if (playerManager == null || ObjectPoolManager.Instance == null || string.IsNullOrEmpty(poolTag) ||
                !playerManager.TryGetComponent(out TrickshotSetBonusRuntime bonus) ||
                bonus.ExtraProjectileCount <= 0) return;

            float projectileSpeed = TryGetComponent(out Rigidbody2D sourceBody) && sourceBody.velocity.sqrMagnitude > 0.01f
                ? sourceBody.velocity.magnitude : 8f;
            if (playerManager.TryGetComponent(out IceSplinterRuntime splinter) && splinter.IsActive)
            {
                splinter.Fire(origin, hitTarget, damage * bonus.ExtraProjectileDamageRatio,
                    bonus.ExtraProjectileCount, projectileSpeed);
                return;
            }
            if (bonus.ExtraProjectileDamageRatio <= 0f) return;
            for (int i = 0; i < bonus.ExtraProjectileCount; i++)
            {
                float angle = Random.Range(0f, 360f);
                float radians = angle * Mathf.Deg2Rad;
                Vector2 direction = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
                GameObject extra = ObjectPoolManager.Instance.SpawnFromPool(poolTag, origin, Quaternion.Euler(0f, 0f, angle));
                if (extra == null) continue;
                // 난반사 추가 탄환은 원본 탄환과 구분되도록 작게 표시합니다.
                extra.transform.localScale *= effectSizeMultiplier * 0.6f;
                if (extra.TryGetComponent(out IProj projectile)) projectile.SetSpeed(projectileSpeed);
                if (extra.TryGetComponent(out Rigidbody2D body)) body.velocity = direction * projectileSpeed;
                if (extra.TryGetComponent(out CollisionObject extraCollision))
                {
                    float extraDamage = Mathf.Floor(Mathf.Max(0f, damage * bonus.ExtraProjectileDamageRatio) + 0.5f);
                    extraCollision.Configure(extraDamage, traits, chargePercent,
                        hitEffectPrefab, ModifierSnapshot, true, true);
                    extraCollision.effectSizeMultiplier = effectSizeMultiplier;
                    extraCollision.SetProjectileOwner(ProjectileOwner);
                    extraCollision.isChainDamage = isChainDamage;
                    extraCollision.extraProjectileSourceTarget = hitTarget;
                }
            }
        }
    }
}
