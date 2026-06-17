using Nytherion.Core.Interfaces;
using Nytherion.Core.Managers;
using Nytherion.Core.Enums;
using System.Collections.Generic;
using UnityEngine;
using VContainer;
using Nytherion.GamePlay.Characters.Player;
using Nytherion.Data.ScriptableObjects.Relics;

namespace Nytherion.GamePlay.Combat
{
    public class CollisionObject : MonoBehaviour
    {
        [HideInInspector] public float damage;

        [Header("Projectile Traits")]
        public List<EquipmentTrait> traits = new List<EquipmentTrait>();
        public GameObject hitEffectPrefab;

        [Header("Pool Settings")]
        public string poolTag = "PlayerProjectile";

        private void OnEnable()
        {
            ApplyRelicEffects();
        }

        private void OnDisable()
        {
            ClearRelicEffects();
        }

        private void ApplyRelicEffects()
        {
            // 플레이어 매니저를 찾아 장착된 유물 정보를 가져옵니다.
            PlayerManager player = FindObjectOfType<PlayerManager>();
            if (player != null)
            {
                PlayerRelicManager relicManager = player.GetComponent<PlayerRelicManager>();
                if (relicManager != null)
                {
                    List<RelicData> relics = relicManager.GetCurrentRelics();
                    bool hasPiercing = false;
                    bool hasBounce = false;

                    for (int i = 0; i < relics.Count; i++)
                    {
                        RelicData relic = relics[i];
                        if (relic != null)
                        {
                            if (string.Equals(relic.relicName, "Piercing", System.StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(relic.koreanName, "관통", System.StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(relic.relicName, "TangledYarn", System.StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(relic.koreanName, "꼬인 실타래", System.StringComparison.OrdinalIgnoreCase))
                            {
                                hasPiercing = true;
                            }
                            if (string.Equals(relic.relicName, "Bounce", System.StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(relic.koreanName, "튕김", System.StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(relic.relicName, "SqueakyGear", System.StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(relic.koreanName, "삐걱이는 톱니", System.StringComparison.OrdinalIgnoreCase))
                            {
                                hasBounce = true;
                            }
                        }
                    }

                    if (hasPiercing)
                    {
                        // 중복 부착 방지
                        if (GetComponent<PiercingEffect>() == null)
                        {
                            gameObject.AddComponent<PiercingEffect>();
                        }
                    }
                    if (hasBounce)
                    {
                        // 중복 부착 방지
                        if (GetComponent<BounceEffect>() == null)
                        {
                            BounceEffect bounce = gameObject.AddComponent<BounceEffect>();
                            bounce.maxBounces = 3;
                            bounce.bounceRadius = 5f;
                        }
                    }
                }
            }
        }

        private void ClearRelicEffects()
        {
            // 풀로 반환되어 재사용될 때를 위해 동적으로 추가된 컴포넌트들을 제거합니다.
            PiercingEffect pierce = GetComponent<PiercingEffect>();
            if (pierce != null)
            {
                Destroy(pierce);
            }

            BounceEffect bounce = GetComponent<BounceEffect>();
            if (bounce != null)
            {
                Destroy(bounce);
            }
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

                    if (target != null && traits != null)
                    {
                        StatusEffectManager effectManager = collision.GetComponent<StatusEffectManager>();
                        if (effectManager == null)
                        {
                            effectManager = collision.gameObject.AddComponent<StatusEffectManager>();
                        }

                        if (traits.Contains(EquipmentTrait.Fire))
                        {
                            float burnDamage = Mathf.Max(1f, damage * 0.2f);
                            effectManager.ApplyEffect(new FireEffect(burnDamage, 5f));
                        }
                        if (traits.Contains(EquipmentTrait.Curse))
                        {
                            effectManager.ApplyEffect(new CurseEffect(1.3f, 5f));
                        }
                        if (traits.Contains(EquipmentTrait.Ice))
                        {
                            effectManager.ApplyEffect(new IceEffect(5f));
                        }
                        if (traits.Contains(EquipmentTrait.Lightning))
                        {
                            effectManager.ApplyEffect(new LightningEffect(5f));
                        }
                        if (traits.Contains(EquipmentTrait.Holy))
                        {
                            effectManager.ApplyEffect(new HolyEffect(5f));
                        }
                        if (traits.Contains(EquipmentTrait.Demonic))
                        {
                            effectManager.ApplyEffect(new DemonicEffect(5f));
                        }
                        if (traits.Contains(EquipmentTrait.Poison))
                        {
                            effectManager.ApplyEffect(new PoisonEffect(3f, 5f));
                        }
                    }

                    // 충돌 위치에 피격 이펙트 재생
                    Vector2 hitPoint = collision.ClosestPoint(transform.position);
                    WeaponEffectHelper.PlayHitEffect(hitEffectPrefab, hitPoint);
                }

                bool shouldSurvive = false;
                // 실시간으로 이펙트 목록을 검색합니다.
                IProjectileEffect[] currentEffects = GetComponents<IProjectileEffect>();

                for (int i = 0; i < currentEffects.Length; i++)
                {
                    IProjectileEffect effect = currentEffects[i];
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