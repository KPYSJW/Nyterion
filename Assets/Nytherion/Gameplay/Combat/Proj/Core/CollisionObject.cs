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
        private int trickshotInteractionCount;
        private bool isSecondaryProjectile;
        private bool hasEnemyHit;
        private bool hasSpawnedTrickshotEchoes;
        private Vector3 lastHitPosition;
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
            output.Configure(damage, traits, chargePercent, hitEffectPrefab, ModifierSnapshot, true);
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
            trickshotInteractionCount = 0;
            isSecondaryProjectile = false;
            hasEnemyHit = false;
            hasSpawnedTrickshotEchoes = false;
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
            bool secondaryProjectile = false)
        {
            trickshotInteractionCount = 0;
            isSecondaryProjectile = secondaryProjectile;
            hasEnemyHit = false;
            hasSpawnedTrickshotEchoes = false;
            float damageMultiplier = 1f;
            if (!secondaryProjectile && playerManager != null &&
                playerManager.TryGetComponent(out TrickshotSetBonusRuntime trickshotRuntime))
            {
                damageMultiplier = trickshotRuntime.ProjectileDamageMultiplier;
            }
            damage = projectileDamage * Mathf.Max(0f, damageMultiplier);
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
            bounceModifier.enabled = hasBaseBounceModifier || modifierSnapshot.HasProjectileBounce;
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
                    hasEnemyHit = true;
                    lastHitPosition = hitPoint;
                    IDamageable target = collision.GetComponentInParent<IDamageable>();
                    if (target is EnemyBase enemy) enemy.TakeDamage(damage, isChainDamage, traits);
                    else target?.TakeDamage(damage, isChainDamage);
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
                bool triggeredTrickshotInteraction = false;
                for (int i = 0; i < projModifiers.Count; i++)
                {
                    IProjModifier effect = projModifiers[i];
                    if (effect is MonoBehaviour mb && !mb.enabled) continue;

                    bool modifierSurvives = effect.OnHit(collision);
                    if (modifierSurvives)
                    {
                        shouldSurvive = true;
                    }

                    if ((modifierSurvives && (effect is PiercingModifier || effect is BounceModifier)) ||
                        effect is SplitModifier)
                    {
                        triggeredTrickshotInteraction = true;
                    }
                }

                ApplyTrickshotInteraction(triggeredTrickshotInteraction);

                if (!shouldSurvive)
                {
                    SpawnTrickshotEchoes();
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

        private void ApplyTrickshotInteraction(bool interactionTriggered)
        {
            if (!interactionTriggered || isSecondaryProjectile || playerManager == null ||
                !playerManager.TryGetComponent(out TrickshotSetBonusRuntime trickshotRuntime) ||
                trickshotInteractionCount >= trickshotRuntime.MaxInteractionStacks)
            {
                return;
            }

            trickshotInteractionCount++;
            damage *= 1f + Mathf.Max(0f, trickshotRuntime.InteractionDamageBonus);
            if (trickshotInteractionCount >= trickshotRuntime.MaxInteractionStacks)
            {
                SpawnTrickshotEchoes();
            }
        }

        private void SpawnTrickshotEchoes()
        {
            if (hasSpawnedTrickshotEchoes || isSecondaryProjectile || !hasEnemyHit || playerManager == null ||
                ObjectPoolManager.Instance == null || string.IsNullOrEmpty(poolTag) ||
                !playerManager.TryGetComponent(out TrickshotSetBonusRuntime trickshotRuntime) ||
                trickshotRuntime.EchoProjectileCount <= 0 || trickshotRuntime.EchoDamageRatio <= 0f)
            {
                return;
            }

            hasSpawnedTrickshotEchoes = true;

            Vector2 originalDirection = transform.right;
            float projectileSpeed = 8f;
            if (TryGetComponent(out Rigidbody2D sourceRigidbody) && sourceRigidbody.velocity.sqrMagnitude > 0.01f)
            {
                originalDirection = sourceRigidbody.velocity.normalized;
                projectileSpeed = sourceRigidbody.velocity.magnitude;
            }

            List<Transform> targets = FindEchoTargets(
                lastHitPosition,
                trickshotRuntime.EchoSearchRadius,
                trickshotRuntime.EchoProjectileCount);
            for (int i = 0; i < trickshotRuntime.EchoProjectileCount; i++)
            {
                Vector2 direction = i < targets.Count
                    ? ((Vector2)targets[i].position - (Vector2)lastHitPosition).normalized
                    : (Vector2)(Quaternion.Euler(0f, 0f, (i == 0 ? -1f : 1f) * 18f) * originalDirection);
                GameObject echo = ObjectPoolManager.Instance.SpawnFromPool(
                    poolTag,
                    lastHitPosition,
                    Quaternion.identity);
                if (echo == null) continue;
                echo.transform.localScale *= effectSizeMultiplier;

                float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                echo.transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);
                if (echo.TryGetComponent(out Rigidbody2D echoRigidbody))
                {
                    echoRigidbody.velocity = direction * projectileSpeed;
                }
                if (echo.TryGetComponent(out IProj echoProjectile))
                {
                    echoProjectile.SetSpeed(projectileSpeed);
                }
                if (echo.TryGetComponent(out CollisionObject echoCollision))
                {
                    echoCollision.Configure(
                        damage * trickshotRuntime.EchoDamageRatio,
                        traits,
                        chargePercent,
                        hitEffectPrefab,
                        ModifierSnapshot,
                        true);
                    echoCollision.effectSizeMultiplier = effectSizeMultiplier;
                }
            }
        }

        private static List<Transform> FindEchoTargets(Vector3 center, float radius, int maxTargets)
        {
            Collider2D[] hits = Physics2D.OverlapCircleAll(center, Mathf.Max(0.1f, radius));
            HashSet<Transform> uniqueTargets = new HashSet<Transform>();
            List<Transform> result = new List<Transform>();
            foreach (Collider2D hit in hits)
            {
                if (hit == null || !hit.CompareTag("Enemy") || !uniqueTargets.Add(hit.transform))
                {
                    continue;
                }

                result.Add(hit.transform);
                if (result.Count >= maxTargets) break;
            }
            return result;
        }
    }
}
