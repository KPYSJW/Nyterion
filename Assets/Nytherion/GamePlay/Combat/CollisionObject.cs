using Nytherion.Core.Interfaces;
using Nytherion.Core.Managers;
using Nytherion.Core.Enums;
using System.Collections.Generic;
using UnityEngine;
using VContainer;
using Nytherion.GamePlay.Characters.Player;

namespace Nytherion.GamePlay.Combat
{
    public class CollisionObject : MonoBehaviour
    {
        [HideInInspector] public float damage;
        [HideInInspector] public float chargePercent = 0f;

        [Header("Projectile Traits")]
        public List<EquipmentTrait> traits = new List<EquipmentTrait>();
        public GameObject hitEffectPrefab;

        [Header("Pool Settings")]
        public string poolTag = "PlayerProjectile";

        private readonly List<IProjectileEffect> projectileEffects = new List<IProjectileEffect>();
        private PlayerManager playerManager;
        private PiercingEffect piercingEffect;
        private BounceEffect bounceEffect;
        private bool hasBasePiercingEffect;
        private bool hasBaseBounceEffect;
        private CombatModifierSnapshot modifierSnapshot = CombatModifierSnapshot.Empty;

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
            piercingEffect = GetComponent<PiercingEffect>();
            hasBasePiercingEffect = piercingEffect != null && piercingEffect.enabled;
            if (piercingEffect == null)
            {
                piercingEffect = gameObject.AddComponent<PiercingEffect>();
                piercingEffect.enabled = false;
            }

            bounceEffect = GetComponent<BounceEffect>();
            hasBaseBounceEffect = bounceEffect != null && bounceEffect.enabled;
            if (bounceEffect == null)
            {
                bounceEffect = gameObject.AddComponent<BounceEffect>();
                bounceEffect.maxBounces = 3;
                bounceEffect.bounceRadius = 5f;
                bounceEffect.enabled = false;
            }

            RefreshProjectileEffects();
        }

        private void OnEnable()
        {
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
            CombatModifierSnapshot currentSnapshot)
        {
            damage = projectileDamage;
            traits = projectileTraits;
            chargePercent = projectileChargePercent;
            if (hitEffectPrefab == null)
            {
                hitEffectPrefab = projectileHitEffect;
            }
            ApplyModifierSnapshot(currentSnapshot);
        }

        public void RefreshProjectileEffects()
        {
            projectileEffects.Clear();
            GetComponents(projectileEffects);
        }

        public void DisableAllProjectileEffects()
        {
            for (int i = 0; i < projectileEffects.Count; i++)
            {
                if (projectileEffects[i] is MonoBehaviour effectBehaviour)
                {
                    effectBehaviour.enabled = false;
                }
            }
        }

        private void ApplyModifierSnapshot(CombatModifierSnapshot currentSnapshot)
        {
            modifierSnapshot = currentSnapshot ?? CombatModifierSnapshot.Empty;
            piercingEffect.enabled = hasBasePiercingEffect || modifierSnapshot.HasProjectilePiercing;
            bounceEffect.enabled = hasBaseBounceEffect || modifierSnapshot.HasProjectileBounce;
        }

        private void OnTriggerEnter2D(Collider2D collision)
        {
            bool isEnemy = collision.CompareTag("Enemy");
            bool isWall = collision.CompareTag("Wall");

            if (isEnemy || isWall)
            {
                if (isEnemy)
                {
                    IDamageable target = collision.GetComponent<IDamageable>();
                    target?.TakeDamage(damage);

                    if (target != null && traits != null &&
                        collision.TryGetComponent<StatusEffectManager>(out StatusEffectManager effectManager))
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

                    // 충돌 위치에 피격 이펙트 재생
                    Vector2 hitPoint = collision.ClosestPoint(transform.position);
                    WeaponEffectHelper.PlayHitEffect(hitEffectPrefab, hitPoint, chargePercent);
                }

                bool shouldSurvive = false;
                for (int i = 0; i < projectileEffects.Count; i++)
                {
                    IProjectileEffect effect = projectileEffects[i];
                    if (effect is MonoBehaviour mb && !mb.enabled) continue;

                    if (effect.OnHit(collision))
                    {
                        shouldSurvive = true;
                    }
                }

                if (!shouldSurvive)
                {
                    ReturnToPool();
                }
            }
        }

        public void ReturnToPool()
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
    }
}
